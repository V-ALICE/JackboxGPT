using System;
using System.Collections.Generic;
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
    public class Fibbage4Engine : BaseJackboxEngine<Fibbage4Client>
    {
        protected override string Tag => "fourbage";

        private bool _lieLock;
        private bool _truthLock;

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
                    // if the error is because of being too close to the truth, use a suggestion? (AI likes to spam the same answer sometimes)
                }

                _lieLock = _truthLock = false;
            }

            if (self.Context == RoomContext.PickCategory)
                ChooseRandomCategory(self);

            if (self.State == RoomState.Writing && !_lieLock)
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
            if (choosingTruth && !_truthLock)
                SubmitTruth(self);
        }

        #region Game Actions
        private async void SubmitLie(Fibbage4Room self)
        {
            _lieLock = true;
            _previousQuestion = self.Question;

            var prompt = CleanPromptForEntry(self.Question);
            LogInfo($"Asking GPT-3 for lie in response to \"{prompt}\".", true);

            var lie = await ProvideLie(prompt, self.MaxLength);
            LogInfo($"Submitting lie \"{lie}\"");

            JackboxClient.SubmitLie(lie);
        }
        
        private async void SubmitDoubleLie(Fibbage4Room self)
        {
            _lieLock = true;
            _previousQuestion = self.Question;

            var prompt = CleanPromptForEntry(self.Question);
            LogInfo($"Asking GPT-3 for double lie in response to \"{prompt}\".", true);

            var lie = await ProvideDoubleLie(prompt, self.JoiningPhrase, self.MaxLength);
            LogInfo($"Submitting double lie \"{lie.Item1}{self.JoiningPhrase}{lie.Item2}\"");

            JackboxClient.SubmitDoubleLie(lie.Item1, lie.Item2);
        }

        private async void SubmitMutualLie(Fibbage4Room self)
        {
            _lieLock = true;
            _previousQuestion = self.Question;

            var prompt1 = CleanPromptForEntry(self.Question);
            var prompt2 = CleanPromptForEntry(self.Question2 ?? throw new InvalidOperationException());
            LogInfo($"Asking GPT-3 for lie in response to \"{prompt1}\" and \"{prompt2}\".", true);
            
            var lie = await ProvideMutualLie(prompt1, prompt2, self.MaxLength);
            LogInfo($"Submitting lie \"{lie}\"");

            JackboxClient.SubmitLie(lie);
        }

        private async void SubmitTruth(Fibbage4Room self)
        {
            _truthLock = true;

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
            LogInfo("Time to choose a category.");
            await Task.Delay(3000);

            var choices = self.CategoryChoices;
            var category = choices.RandomIndex();
            LogInfo($"Choosing category \"{choices[category].Trim()}\".");

            JackboxClient.ChooseCategory(category);
        }
        #endregion

        #region GPT-3 Prompts

        private string CleanResult(string input, string prompt = "", bool logChanges = false)
        {
            input = input.ToUpper();
            prompt = prompt.ToUpper();

            // Characters that mark the end of a reasonable answer
            var clipMarkers = new[] { '?', '!', ';', ':' };
            var clipIdx = input.IndexOfAny(clipMarkers);
            var clipped = clipIdx >= 0 ? input[..clipIdx] : input;

            // Characters that shouldn't be in a submitted answer
            var removals = new[] { "\"", "\n", "\r", "\t", "..." };
            clipped = removals.Aggregate(clipped, (current, r) => current.Replace(r, null));

            // Characters that shouldn't be on the front or back of a submitted answer
            var endRemovals = new[] { '.', ' ', ',' };
            clipped = clipped.Trim(endRemovals).Replace("  ", " "); // Additionally remove any double spaces that previous changes may have created

            // Sometimes the AI likes to include pieces of the prompt at the end of its answer (i.e. "at the _______ exhibit." -> "art exhibit")
            // Removing these might not always be correct since there are (probably) instances where such duplication makes sense
            if (prompt.Length > 0)
            {
                var promptEnding = prompt[(prompt.LastIndexOf("__", StringComparison.Ordinal) + 2)..].Trim(endRemovals);
                var words = clipped.Split(' ');
                var allWordSets = words.Select((_, i) => string.Join(' ', words[i..])).ToList(); // Ordered longest to shortest
                var overlap = allWordSets.FirstOrDefault(promptEnding.StartsWith) ?? "";
                clipped = clipped[..^overlap.Length].Trim(endRemovals);
            }

            if (logChanges && input.Length != clipped.Length)
                LogInfo($"Edited AI response from \"{input}\" to \"{clipped}\"");
            return clipped;
        }

        private async Task<string> ProvideLie(string fibPrompt, int maxLength)
        {
            var prompt = $@"Here are some prompts from the game Fibbage, in which players attempt to write convincing lies to trick others.

Q: In the mid-1800s, Queen Victoria employed a man named Jack Black, whose official job title was Royal _______.
A: Flute player

Q: In 2016, KFC announced it created a _______ that smells like fried chicken.
A: Scratch 'n' sniff menu

Q: Due to a habit he had while roaming the halls of the White House, President Lyndon B. Johnson earned the nickname ""_______ Johnson.""
A: Desk Butt

Q: {fibPrompt}
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
                var cleanText = CleanResult(completion.Text.Trim(), fibPrompt);
                if (cleanText.Length > 0 && cleanText.Length <= maxLength && !cleanText.Contains("__")) return true;
                LogDebug($"Received unusable ProvideLie response: {completion.Text.Trim()}");
                return false;
            },
            defaultResponse: "Default Response");

            return CleanResult(result.Text.Trim(), fibPrompt, true);
        }
        
        private async Task<Tuple<string, string>> ProvideDoubleLie(string fibPrompt, string delim, int maxLength)
        {
            var prompt = $@"Here are some prompts from the game Fibbage, in which players attempt to write convincing lies to trick others. These prompts require two responses, separated by the | character.

Q: Researchers at Aalto and Oxfort universities studied the phone records of over 3.2 million Europeans and found that people have the most _______ when they _______.
A: friends|are 25 years old

Q: The controversial Supreme Court case Nix v. Hedden upset more than a few people when the court ruled that _______ are _______.
A: tomatoes|vegetables

Q: In an attempt to teach kids an important lesson, Bernie Karl of Alaska wants to put a _______ of _______ in every public school.
A: box|handguns

Q: {fibPrompt}
A:";

            var result = await CompletionService.CompletePrompt(prompt, new CompletionParameters
                {
                    Temperature = 0.8,
                    MaxTokens = 16,
                    TopP = 1,
                    FrequencyPenalty = 0.2,
                    StopSequences = new[] { "\n" }
                }, completion =>
                {
                    try
                    {
                        var lies = completion.Text.Trim().Split('|');
                        var p1 = CleanResult(lies[0]);
                        var p2 = CleanResult(lies[1], fibPrompt);
                        if (lies.Length >= 2
                            && p1.Length > 0 && p1.Length <= maxLength && !p1.Contains("__")
                            && p2.Length > 0 && p2.Length <= maxLength && !p2.Contains("__"))
                            return true;
                    }
                    catch
                    {
                        // pass
                    }

                    LogDebug($"Received unusable ProvideDoubleLie response: {completion.Text.Trim()}");
                    return false;
                },
                defaultResponse: "default|response");

            var split = result.Text.Trim().Split('|');
            return new Tuple<string, string>(CleanResult(split[0], logChanges: true), CleanResult(split[1], fibPrompt, true));
        }

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

        private async Task<int> ProvideTruth(string fibPrompt, IReadOnlyList<Choice> lies)
        {
            var options = "";

            for(var i = 0; i < lies.Count; i++)
                options += $"{i + 1}. {lies[i].Text}\n";

            var prompt = $@"I was given a list of lies and one truth for the prompt ""${fibPrompt}"". These were my options:

${options}
I think the truth is answer number";

            int IntParseExt(string input)
            {
                if (input.Length < 1) throw new FormatException();

                // Assume the response is int-parsable if it starts with a digit character
                if (char.IsDigit(input[0])) return int.Parse(input);
                
                // GPT likes to respond in English sometimes, so this (manually) tries to check for that
                return input.ToUpper() switch
                {
                    "ONE" => 1,
                    "TWO" => 2,
                    "THREE" => 3,
                    "FOUR" => 4,
                    "FIVE" => 5,
                    "SIX" => 6,
                    "SEVEN" => 7,
                    "EIGHT" => 8, // Game should have a max of eight options to choose from
                    _ => throw new FormatException() // Response was something unhandled here
                };
            }

            var result = await CompletionService.CompletePrompt(prompt, new CompletionParameters
            {
                Temperature = 1,
                MaxTokens = 1,
                TopP = 1,
                StopSequences = new[] { "\n" }
            }, completion =>
            {
                try
                {
                    var answer = IntParseExt(completion.Text.Trim());
                    if (0 < answer && answer <= lies.Count) return true;
                }
                catch(FormatException)
                {
                    // pass
                }

                LogDebug($"Received unusable ProvideTruth response: {completion.Text.Trim()}");
                return false;
            }, 
            defaultResponse: new Random().Next(1, lies.Count+1).ToString());
            
            return IntParseExt(result.Text.Trim()) - 1;
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
