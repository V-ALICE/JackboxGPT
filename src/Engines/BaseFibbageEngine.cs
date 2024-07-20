using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JackboxGPT3.Games.Common;
using JackboxGPT3.Games.Common.Models;
using JackboxGPT3.Services;
using Serilog;
using static JackboxGPT3.Services.ICompletionService;

namespace JackboxGPT3.Engines
{
    public abstract class BaseFibbageEngine<TClient> : BaseJackboxEngine<TClient>
        where TClient : IJackboxClient
    {
        protected bool LieLock;
        protected bool TruthLock;
        protected int FailureCounter = 0;
        protected readonly int MaxFailures = 5;

        protected BaseFibbageEngine(ICompletionService completionService, ILogger logger, TClient client, int instance)
            : base(completionService, logger, client, instance)
        {
        }

        protected string CleanResult(string input, string prompt = "", bool logChanges = false)
        {
            input = input.ToUpper();
            prompt = prompt.ToUpper();

            // Characters that mark the end of a reasonable answer
            var clipMarkers = new[] { '?', '!', ';', ':', '?' };
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

        protected async Task<string> ProvideLie(string fibPrompt, int maxLength)
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
        
        protected async Task<Tuple<string, string>> ProvideDoubleLie(string fibPrompt, string delim, int maxLength)
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

        protected async Task<int> ProvideTruth<T>(string fibPrompt, IReadOnlyList<T> lies) where T : ISelectionChoice
        {
            var options = "";

            for(var i = 0; i < lies.Count; i++)
                options += $"{i + 1}. {lies[i].SelectionText}\n";

            var prompt = $@"I was given a list of lies and one truth for the prompt ""{fibPrompt}"". These were my options:

{options}
I think the truth is answer number: ";

            int IntParseExt(string input)
            {
                if (input.Length < 1) throw new FormatException();

                // Assume the response is int-parsable if it starts with a digit character
                if (char.IsDigit(input[0])) return int.Parse(input);
                
                // GPT responds in English sometimes, so this (manually) tries to check for that
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
            defaultResponse: defaultResp);

            if (result.Text != defaultResp)
                return IntParseExt(result.Text.Trim()) - 1;

            LogDebug("Received only unusable ProvideTruth responses. Choice will be chosen randomly");
            return new Random().Next(lies.Count);
        }

    }
}
