using System;
using System.Threading.Tasks;
using JackboxGPT.Extensions;
using JackboxGPT.Games.Common.Models;
using JackboxGPT.Games.Fibbage4;
using JackboxGPT.Games.Fibbage4.Models;
using JackboxGPT.Services;
using Serilog;
using static JackboxGPT.Services.ICompletionService;

namespace JackboxGPT.Engines
{
    public class Fibbage4Engine : BaseFibbageEngine<Fibbage4Client>
    {
        protected override string Tag => "fourbage";

        // Fibbage 4 doesn't send the original prompt when it sends lie choices, so this keeps track of it
        private string _previousQuestion; 

        public Fibbage4Engine(ICompletionService completionService, ILogger logger, Fibbage4Client client, ManagedConfigFile configFile, int instance)
            : base(completionService, logger, client, configFile, instance)
        {
            JackboxClient.OnSelfUpdate += OnSelfUpdate;
            JackboxClient.Connect();
        }

        private void OnSelfUpdate(object sender, Revision<Fibbage4Player> revision)
        {
            var self = revision.New;
            if (revision.Old.State != revision.New.State)
                LogDebug($"New room state: {self.State}", true);
            if (revision.Old.Context != revision.New.Context)
                LogDebug($"New room context: {self.Context}", true);

            if (self.State == RoomState.Waiting || self.Error != null)
            {
                if (self.Error != null)
                {
                    if (self.Error.Contains("Too close to the truth"))
                    {
                        LogInfo("The submitted lie was too close to the truth. Generating a new lie...");
                        RetryCount += 1;
                    }
                    else
                    {
                        LogWarning($"Received submission error from game: \"{self.Error}\"");
                    }
                }
                else
                {
                    RetryCount = 0;
                }

                LieLock = TruthLock = false;
            }

            if (self.Context == RoomContext.PickCategory)
                ChooseRandomCategory(self);

            if (self.State == RoomState.Writing && !LieLock)
            {
                switch (self.Context)
                {
                    case RoomContext.Blankie:
                        SubmitLie(self);
                        break;
                    case RoomContext.DoubleBlankie:
                        SubmitDoubleLie(self);
                        break;
                    case RoomContext.FinalRound:
                        SubmitMutualLie(self);
                        break;
                }
            }

            var choosingTruth = self.Context == RoomContext.PickTruth ||
                                self.Context == RoomContext.FinalRound1 ||
                                self.Context == RoomContext.FinalRound2;
            if (choosingTruth && !TruthLock)
                SubmitTruth(self);
        }

        private async void SubmitLie(Fibbage4Player self)
        {
            _previousQuestion = self.Question;

            var lie = await FormLie(self.Question, self.MaxLength);
            JackboxClient.SubmitLie(lie);
        }
        
        private async void SubmitDoubleLie(Fibbage4Player self)
        {
            _previousQuestion = self.Question;

            var lie = await FormDoubleLie(self.Question, self.JoiningPhrase, self.MaxLength);
            JackboxClient.SubmitDoubleLie(lie.Item1, lie.Item2);
        }

        private async void SubmitMutualLie(Fibbage4Player self)
        {
            LieLock = true;
            _previousQuestion = self.Question;

            string lie;
            if (RetryCount > Config.Fibbage.SubmissionRetries)
            {
                RetryCount = 0;
                LogInfo("Submitting a default answer because there were too many submission errors.");

                lie = GetDefaultLie();
            }
            else
            {
                var prompt1 = CleanPromptForEntry(self.Question);
                var prompt2 = CleanPromptForEntry(self.Question2);
                LogInfo($"Asking GPT-3 for lie in response to \"{prompt1}\" and \"{prompt2}\"", true, prefix: "\n\n\n");

                // GPT3 doesn't really have the wherewithal to get the context of this type of question,
                // so the questions are given in a random order in order to give AI players more variety
                if (new Random().Next(2) == 0)
                    lie = await ProvideMutualLie(prompt1, prompt2, self.MaxLength);
                else
                    lie = await ProvideMutualLie(prompt1, prompt2, self.MaxLength);
            }

            LogInfo($"Submitting lie \"{lie}\"");
            JackboxClient.SubmitLie(lie);
        }

        private async void SubmitTruth(Fibbage4Player self)
        {
            int truth = 0;
            switch (self.Context)
            {
                case RoomContext.PickTruth:
                    truth = await FormTruth(_previousQuestion, self.LieChoices);
                    break;
                case RoomContext.FinalRound1:
                case RoomContext.FinalRound2:
                    truth = await FormTruth(self.Prompt, self.LieChoices);
                    break;
            }
            
            JackboxClient.ChooseTruth(truth);
        }

        private async void ChooseRandomCategory(Fibbage4Player self)
        {
            LogInfo("Time to choose a category.", prefix: "\n");
            await Task.Delay(Config.Fibbage.CategoryChoiceDelayMs);

            var choices = self.CategoryChoices;
            var category = choices.RandomIndex();
            LogInfo($"Choosing category \"{choices[category].Trim()}\".");

            JackboxClient.ChooseCategory(category);
        }

        private async Task<string> ProvideMutualLie(string fibPrompt1, string fibPrompt2, int maxLength)
        {
            var prompt = new TextInput
            {
                ChatSystemMessage = "You are a player in a game called Fibbage, in which players attempt to write convincing lies to trick others. Since this is a game about tricking other players, please do not respond with the correct answer. There will be two prompts, please respond with only one concise answer that makes sense for both prompts.",
                ChatStylePrompt = $"Here's a new pair of prompts:\n{fibPrompt1}\n{fibPrompt2}",
                CompletionStylePrompt = $@"Here are some double prompts from the game Fibbage, in which players attempt to write convincing lies to trick others. These prompts require a single response which answers both prompt.

Q1: In 2008, as part of an investigation, The New York Daily News managed to steal _______.
Q2: Allie Tarantino of New York was surprised to find he owned a rare trading card featuring _______.
A: Clooney's pig

Q1: In his youth, French president Charles de Gaulle was weirdly nicknamed _______.
Q2: ""Science created him. Now Chuck Norris must destroy him"" is the tagline for the movie _______.
A: The human dynamo

Q1: {fibPrompt1}
Q2: {fibPrompt2}
A:",
            };
            var useChatEngine = EnginePref != ManagedConfigFile.EnginePreference.Completion; // Forcing this for mixed mode since Completion is terrible at this sort of question
            LogVerbose($"Prompt:\n{(useChatEngine ? prompt.ChatStylePrompt : prompt.CompletionStylePrompt)}");

            var result = await CompletionService.CompletePrompt(prompt, useChatEngine, new CompletionParameters
                {
                    Temperature = Config.Fibbage.GenTemp,
                    MaxTokens = 16,
                    FrequencyPenalty = 0.2,
                    StopSequences = new[] { "\n" }
                },
                completion =>
                {
                    var cleanText = CleanResult(completion.Text.Trim(), fibPrompt1);
                    cleanText = CleanResult(cleanText, fibPrompt2);
                    if (cleanText.Length > 0 && cleanText.Length <= maxLength && !cleanText.Contains("__")) return true;
                    LogDebug($"Received unusable ProvideLie response: {completion.Text.Trim()}");
                    return false;
                },
                maxTries: Config.Fibbage.MaxRetries,
                defaultResponse: "");

            if (result.Text.Length == 0)
                return GetDefaultLie();

            var cleanText = CleanResult(result.Text.Trim(), fibPrompt1);
            return CleanResult(cleanText, fibPrompt2);
        }

        protected override string CleanPromptForEntry(string prompt)
        {
            return prompt.Replace("[blank][/blank]", "_______").StripTags();
        }

        protected override string GetDefaultLie()
        {
            var choices = JackboxClient.GameState.Self.SuggestionChoices;
            if (choices.Count == 0)
            {
                LogDebug("No suggestions were available when trying to get a default answer. Submitting base default answer");
                return base.GetDefaultLie();
            }

            return choices[choices.RandomIndex()];
        }

        protected override Tuple<string, string> GetDefaultDoubleLie()
        {
            var choices = JackboxClient.GameState.Self.SuggestionChoices;
            if (choices.Count == 0)
            {
                LogDebug("No suggestions were available when trying to get a default answer. Submitting base default answer");
                return base.GetDefaultDoubleLie();
            }

            var parts = choices[choices.RandomIndex()].Split('|');
            return new Tuple<string, string>(parts[0], parts[1]);
        }
    }
}
