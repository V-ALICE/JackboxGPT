using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JackboxGPT.Extensions;
using JackboxGPT.Games.Common;
using JackboxGPT.Games.Common.Models;
using JackboxGPT.Services;
using Serilog;
using static JackboxGPT.Services.ICompletionService;

namespace JackboxGPT.Engines
{
    public abstract class BaseFibbageEngine<TClient> : BaseJackboxEngine<TClient>
        where TClient : IJackboxClient
    {
        protected bool LieLock;
        protected bool TruthLock;

        // Limit the amount of "too close to the answer" error retries
        protected int RetryCount;

        // Used solely for logging, since they submitted answer is not returned with the list of choices
        private string _myLastAnswer = "";

        protected BaseFibbageEngine(ICompletionService completionService, ILogger logger, TClient client, ManagedConfigFile configFile, int instance, uint coinFlip)
            : base(completionService, logger, client, configFile, instance)
        {
            UseChatEngine = configFile.Fibbage.EnginePreference == ManagedConfigFile.EnginePreference.Chat
                            || (configFile.Fibbage.EnginePreference == ManagedConfigFile.EnginePreference.Mix && instance % 2 == coinFlip);
            LogDebug($"Using {(UseChatEngine ? "Chat" : "Completion")} engine");
            CompletionService.ResetAll();
        }

        protected string CleanResult(string input, string prompt = "", bool logChanges = false)
        {
            input = input.ToUpper();
            prompt = prompt.ToUpper();

            // Characters that often indicate that the answer will be unreasonable to try to use
            var badMarkers = new[] { ';', ':' };
            if (badMarkers.Any(c => input.Contains(c)))
            {
                return "";
            }

            // Characters that mark the end of a reasonable answer
            var clipMarkers = new[] { '?', '!', '?' };
            var clipIdx = input.IndexOfAny(clipMarkers);
            var clipped = clipIdx >= 0 ? input[..clipIdx] : input;

            // An odd amount of quotes implies that something got cut off, in which case this answer isn't reasonable anymore
            if (input.Contains('"') && input.Split('"').Length % 2 == 0)
                return "";

            // Characters that shouldn't be in a submitted answer
            var removals = new[] { "\n", "\r", "\t", "...", "[", "]", "{", "}", "`", "(", ")", "\\" };
            foreach (var r in removals)
                clipped = clipped.Replace(r, null);

            // Quotes shouldn't be on both ends of a submitted answer, but are allowed within a submitted answer
            if (clipped.StartsWith('"') && clipped.EndsWith('"'))
                clipped = clipped.TrimQuotes();
            if (clipped.StartsWith('“') && clipped.EndsWith('”'))
                clipped = clipped.TrimQuotes();

            // Characters that shouldn't be on the front or back of a submitted answer
            var endRemovals = new[] { '.', ' ', ',' };
            clipped = clipped.Trim(endRemovals);

            // Remove any double spaces that previous changes may have created
            clipped = clipped.Trim().Replace("  ", " "); 

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

            // Remove any double spaces that previous changes may have created (again)
            clipped = clipped.Trim().Replace("  ", " ");

            if (logChanges && input.Length != clipped.Length)
                LogDebug($"Edited AI response from \"{input}\" to \"{clipped}\"");
            return clipped;
        }

        private async Task<string> ProvideLie(string fibPrompt, int maxLength)
        {
            var prompt = new TextInput
            {
                ChatSystemMessage = "You are a player in a game called Fibbage, in which players attempt to write convincing lies to trick others. Since this is a game about tricking other players, please do not respond with the correct answer. Please respond to the prompt with just your answer, do not repeat the prompt.",
                ChatStylePrompt = $"Here's a new prompt: {fibPrompt}",
                CompletionStylePrompt = 
                $@"Here are some prompts from the game Fibbage, in which players attempt to write convincing lies to trick others.

Q: In the mid-1800s, Queen Victoria employed a man named Jack Black, whose official job title was Royal _______.
A: Flute player

Q: In 2016, KFC announced it created a _______ that smells like fried chicken.
A: Scratch 'n' sniff menu

Q: Due to a habit he had while roaming the halls of the White House, President Lyndon B. Johnson earned the nickname ""_______ Johnson.""
A: Desk Butt

Q: {fibPrompt}
A:",
            };
            LogVerbose($"Prompt:\n{(UseChatEngine ? prompt.ChatStylePrompt : prompt.CompletionStylePrompt)}", true);

            var result = await CompletionService.CompletePrompt(prompt, UseChatEngine, new CompletionParameters
                {
                    Temperature = Config.Fibbage.GenTemp,
                    MaxTokens = 16,
                    TopP = 1,
                    FrequencyPenalty = 0.2,
                    StopSequences = new[] { "\n" }
                },
                completion =>
                {
                    var cleanText = CleanResult(completion.Text.Trim(), fibPrompt);
                    if (cleanText.Length > 0 && cleanText.Length <= maxLength && !cleanText.Contains("__")) return true;

                    LogDebug($"Received unusable ProvideLie response: \"{completion.Text.Trim()}\"");
                    return false;
                },
                maxTries: Config.Fibbage.MaxRetries,
                defaultResponse: "");

            if (result.Text.Length == 0)
                return GetDefaultLie();

            return CleanResult(result.Text.Trim(), fibPrompt, true);
        }
        
        private async Task<Tuple<string, string>> ProvideDoubleLie(string fibPrompt, int maxLength)
        {
            var prompt = new TextInput
            {
                ChatSystemMessage = "You are a player in a game called Fibbage, in which players attempt to write convincing lies to trick others. Since this is a game about tricking other players, please do not respond with the correct answer. This prompt requires two responses, so please respond to the prompt with only your answers separated by the | character.",
                ChatStylePrompt = $"Here's a new prompt: {fibPrompt}",
                CompletionStylePrompt = $@"Here are some prompts from the game Fibbage, in which players attempt to write convincing lies to trick others. These prompts require two responses, separated by the | character.

Q: Researchers at Aalto and Oxford universities studied the phone records of over 3.2 million Europeans and found that people have the most _______ when they _______.
A: friends|are 25 years old

Q: The controversial Supreme Court case Nix v. Hedden upset more than a few people when the court ruled that _______ are _______.
A: tomatoes|vegetables

Q: In an attempt to teach kids an important lesson, Bernie Karl of Alaska wants to put a _______ of _______ in every public school.
A: box|handguns

Q: {fibPrompt}
A:",
            };
            LogVerbose($"Prompt:\n{(UseChatEngine ? prompt.ChatStylePrompt : prompt.CompletionStylePrompt)}", true);

            var result = await CompletionService.CompletePrompt(prompt, UseChatEngine, new CompletionParameters
                {
                    Temperature = Config.Fibbage.GenTemp,
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

                    LogDebug($"Received unusable ProvideDoubleLie response: \"{completion.Text.Trim()}\"");
                    return false;
                },
                maxTries: Config.Fibbage.MaxRetries,
                defaultResponse: "");

            if (result.Text.Length == 0)
                return GetDefaultDoubleLie();

            var split = result.Text.Trim().Split('|');
            return new Tuple<string, string>(CleanResult(split[0], logChanges: true), CleanResult(split[1], fibPrompt, true));
        }

        private async Task<int> ProvideTruth<T>(string fibPrompt, IReadOnlyList<T> lies) where T : ISelectionChoice
        {
            var options = "";

            for(var i = 0; i < lies.Count; i++)
                options += $"{i + 1}. {lies[i].SelectionText}\n";

            var prompt = new TextInput
            {
                ChatSystemMessage = $"You are a player in a game called Fibbage, in which players attempt to write convincing lies to trick others. Please respond with only the number corresponding with the option that you think is correct for the prompt \"{fibPrompt}\"",
                ChatStylePrompt = options,
                CompletionStylePrompt = $@"I was given a list of lies and one truth for the prompt ""{fibPrompt}"". These were my options:

{options}
I think the truth is answer number: ",
            };
            LogVerbose($"Prompt:\n{(Config.Model.UseChatEngineForVoting ? prompt.ChatStylePrompt : prompt.CompletionStylePrompt)}");

            int IntParseExt(string input)
            {
                if (input.Length < 1) throw new FormatException();

                // Assume the response is int-parsable if it starts with a digit character
                if (char.IsDigit(input[0])) return int.Parse(new string(input.TakeWhile(char.IsDigit).ToArray()));
                
                // GPT-3 responds in English sometimes, so this (manually) tries to check for that
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

            const string defaultResp = "__NORESPONSE";
            var result = await CompletionService.CompletePrompt(prompt, Config.Model.UseChatEngineForVoting, new CompletionParameters
                {
                    Temperature = Config.Fibbage.VoteTemp,
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
                    catch (FormatException)
                    {
                        // pass
                    }

                    LogDebug($"Received unusable ProvideTruth response: {completion.Text.Trim()}");
                    return false;
                },
                maxTries: Config.Fibbage.MaxRetries,
                defaultResponse: defaultResp);

            CompletionService.ResetOne(prompt.ChatSystemMessage);
            if (result.Text != defaultResp)
                return IntParseExt(result.Text.Trim()) - 1;

            LogDebug("Received only unusable ProvideTruth responses. Choice will be chosen randomly");
            return new Random().Next(lies.Count);
        }

        protected async Task<string> FormLie(string question, int maxLength = 45)
        {
            LieLock = true;

            string lie;
            if (RetryCount > Config.Fibbage.SubmissionRetries)
            {
                RetryCount = 0;
                LogInfo("Submitting a default answer because there were too many submission errors.");

                lie = GetDefaultLie();
            }
            else
            {
                var prompt = CleanPromptForEntry(question);
                if (RetryCount == 0)
                    LogInfo($"Asking GPT-3 for lie in response to \"{prompt}\"", true, prefix: "\n\n\n");

                lie = await ProvideLie(prompt, maxLength);
            }

            LogInfo($"Submitting lie \"{lie}\"");
            _myLastAnswer = lie;
            return lie;
        }

        protected async Task<Tuple<string, string>> FormDoubleLie(string question, string answerDelim, int maxLength = 45)
        {
            LieLock = true;

            Tuple<string, string> lie;
            if (RetryCount > Config.Fibbage.SubmissionRetries)
            {
                RetryCount = 0;
                LogInfo("Submitting a default answer because there were too many submission errors.");

                lie = GetDefaultDoubleLie();
            }
            else
            {
                var prompt = CleanPromptForEntry(question);
                if (RetryCount == 0)
                    LogInfo($"Asking GPT-3 for double lie in response to \"{prompt}\"", true, prefix: "\n\n\n");

                lie = await ProvideDoubleLie(prompt, maxLength);
            }

            LogInfo($"Submitting double lie \"{lie.Item1}{answerDelim}{lie.Item2}\"");
            _myLastAnswer = string.Join(answerDelim, lie.Item1, lie.Item2);
            return lie;
        }

        protected async Task<int> FormTruth<T>(string question, IReadOnlyList<T> choices) where T : ISelectionChoice
        {
            TruthLock = true;

            var prompt = CleanPromptForEntry(question);
            var choicesStr = choices.Aggregate("", (current, a) => current + ("\"" + a.SelectionText + "\", "));
            choicesStr += _myLastAnswer;
            LogInfo($"Asking GPT-3 to choose truth out of these options [{choicesStr}]", true, prefix: "\n");

            var truth = await ProvideTruth(prompt, choices);
            LogInfo($"Submitting truth {truth} (\"{choices[truth].SelectionText}\")");
            return truth;
        }

        protected virtual string CleanPromptForEntry(string prompt)
        {
            return prompt.StripHtml();
        }

        protected virtual string GetDefaultLie()
        {
            return "Default Response";
        }

        protected virtual Tuple<string, string> GetDefaultDoubleLie()
        {
            return new Tuple<string, string>("Default", "Response");
        }
    }
}
