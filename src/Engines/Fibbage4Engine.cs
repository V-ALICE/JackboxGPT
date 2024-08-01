using System;
using System.Linq;
using System.Threading.Tasks;
using JackboxGPT3.Extensions;
using JackboxGPT3.Games.Common.Models;
using JackboxGPT3.Games.Fibbage4;
using JackboxGPT3.Games.Fibbage4.Models;
using JackboxGPT3.Services;
using Serilog;
using static JackboxGPT3.Services.ICompletionService;

namespace JackboxGPT3.Engines
{
    public class Fibbage4Engine : BaseFibbageEngine<Fibbage4Client>
    {
        protected override string Tag => "fourbage";

        private string _previousQuestion; // Fibbage 4 doesn't send the original prompt when it sends choices

        public Fibbage4Engine(ICompletionService completionService, ILogger logger, Fibbage4Client client, int instance)
            : base(completionService, logger, client, instance)
        {
            JackboxClient.OnRoomUpdate += OnRoomUpdate;
            JackboxClient.Connect();
        }

        private void OnRoomUpdate(object sender, Revision<Fibbage4Room> revision)
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
                    LogWarning($"Received submission error from game: {self.Error}");
                    if (self.Error.Contains("Too close to the truth"))
                        FailureCounter += 1;
                }
                else
                {
                    FailureCounter = 0;
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

        #region Game Actions
        private async void SubmitLie(Fibbage4Room self)
        {
            LieLock = true;
            _previousQuestion = self.Question;

            if (FailureCounter > MaxFailures)
            {
                LogInfo("Submitting default answer because there were too many submission errors.");
                JackboxClient.SubmitLie("NO ANSWER"); // TODO: use suggestion
                FailureCounter = 0;
                return;
            }

            var prompt = CleanPromptForEntry(self.Question);
            LogInfo($"Asking GPT-3 for lie in response to \"{prompt}\".", true);

            var lie = await ProvideLie(prompt, self.MaxLength);
            LogInfo($"Submitting lie \"{lie}\"");

            JackboxClient.SubmitLie(lie);
        }
        
        private async void SubmitDoubleLie(Fibbage4Room self)
        {
            LieLock = true;
            _previousQuestion = self.Question;

            if (FailureCounter > MaxFailures)
            {
                LogInfo("Submitting default answer because there were too many submission errors.");
                JackboxClient.SubmitDoubleLie("NO", "ANSWER");
                FailureCounter = 0;
                return;
            }

            var prompt = CleanPromptForEntry(self.Question);
            LogInfo($"Asking GPT-3 for double lie in response to \"{prompt}\".", true);

            var lie = await ProvideDoubleLie(prompt, self.JoiningPhrase, self.MaxLength);
            LogInfo($"Submitting double lie \"{lie.Item1}{self.JoiningPhrase}{lie.Item2}\"");

            JackboxClient.SubmitDoubleLie(lie.Item1, lie.Item2);
        }

        private async void SubmitMutualLie(Fibbage4Room self)
        {
            LieLock = true;
            _previousQuestion = self.Question;

            if (FailureCounter > MaxFailures)
            {
                LogInfo("Submitting default answer because there were too many submission errors.");
                JackboxClient.SubmitLie("NO ANSWER");
                FailureCounter = 0;
                return;
            }

            var prompt1 = CleanPromptForEntry(self.Question);
            var prompt2 = CleanPromptForEntry(self.Question2 ?? throw new InvalidOperationException());
            LogInfo($"Asking GPT-3 for lie in response to \"{prompt1}\" and \"{prompt2}\".", true);

            // GPT3 doesn't really have the wherewithal to get the context of this type of question
            // So the questions are given in a random order in order to give AI players more variety
            // when there are multiple in the same game
            string lie;
            if (new Random().Next(2) == 0)
                lie = await ProvideMutualLie(prompt1, prompt2, self.MaxLength);
            else
                lie = await ProvideMutualLie(prompt1, prompt2, self.MaxLength);

            LogInfo($"Submitting lie \"{lie}\"");
            JackboxClient.SubmitLie(lie);
        }

        private async void SubmitTruth(Fibbage4Room self)
        {
            TruthLock = true;

            var prompt = "";
            switch (self.Context)
            {
                case RoomContext.PickTruth:
                    prompt = CleanPromptForEntry(_previousQuestion);
                    break;
                case RoomContext.FinalRound1:
                    prompt = CleanPromptForEntry(self.Prompt);
                    break;
                case RoomContext.FinalRound2:
                    prompt = CleanPromptForEntry(self.Prompt);
                    break;
            }

            var choices = self.LieChoices;
            var choicesStr = choices.Aggregate("", (current, a) => current + (a.Text + ", "))[..^2];
            LogInfo($"Asking GPT-3 to choose truth out of these options [{choicesStr}].", true);
            var truth = await ProvideTruth(prompt, choices);
            LogInfo($"Submitting truth {truth} (\"{choices[truth].Text}\")");

            JackboxClient.ChooseTruth(truth);
        }

        private async void ChooseRandomCategory(Fibbage4Room self)
        {
            LogInfo("Time to choose a category.", prefix: "\n\n");
            await Task.Delay(3000);

            var choices = self.CategoryChoices;
            var category = choices.RandomIndex();
            LogInfo($"Choosing category \"{choices[category].Trim()}\".");

            JackboxClient.ChooseCategory(category);
        }
        #endregion

        #region GPT-3 Prompts
        private async Task<string> ProvideMutualLie(string fibPrompt1, string fibPrompt2, int maxLength)
        {
            var prompt = $@"Here are some double prompts from the game Fibbage, in which players attempt to write convincing lies to trick others. These prompts require a single response which answers both prompt.

Q1: In 2008, as part of an investigation, The New York Daily News managed to steal _______.
Q2: Allie Tarantino of New York was surprised to find he owned a rare trading card featuring _______.
A: Clooney's pig

Q1: In his youth, French president Charles de Gaulle was weirdly nicknamed _______.
Q2: ""Science created him. Now Chuck Norris must destroy him"" is the tagline for the movie _______.
A: The human dynamo

Q1: {fibPrompt1}
Q2: {fibPrompt2}
A:";

            var result = await CompletionService.CompletePrompt(prompt, new CompletionParameters
                {
                    Temperature = 0.8,
                    MaxTokens = 16,
                    TopP = 1,
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
                defaultResponse: "Default Response");

            var cleanText = CleanResult(result.Text.Trim(), fibPrompt1);
            return CleanResult(cleanText, fibPrompt2);
        }
        #endregion

        #region Prompt Cleanup
        private static string CleanPromptForEntry(string prompt)
        {
            return prompt.Replace("[blank][/blank]", "_______").StripTags();
        }
        #endregion
    }
}
