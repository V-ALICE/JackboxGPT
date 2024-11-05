using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using JackboxGPT3.Extensions;
using JackboxGPT3.Games.Common;
using JackboxGPT3.Services;
using Serilog;

namespace JackboxGPT3.Engines
{
    public abstract class BaseQuiplashEngine<TClient> : BaseJackboxEngine<TClient>
        where TClient : IJackboxClient
    {
        protected BaseQuiplashEngine(ICompletionService completionService, ILogger logger, TClient client, ManagedConfigFile configFile, int instance)
            : base(completionService, logger, client, configFile, instance)
        {
        }

        protected string CleanResult(string input, bool logChanges = false)
        {
            // AI likes to self censor sometimes
            if (input.Contains("**")) return "";

            var clipped = input.ToUpper();

            // Characters that shouldn't be in a submitted answer
            var removals = new[] { "\n", "\r", "\t" };
            foreach (var r in removals)
                clipped = clipped.Replace(r, null);

            // Characters that shouldn't be on the front or back of a submitted answer
            var endRemovals = new[] { '.', ' ', ',' };
            clipped = clipped.Trim(endRemovals);

            // Remove any double spaces that previous changes may have created
            clipped = clipped.Trim().Replace("  ", " ");

            if (logChanges && input.Length != clipped.Length)
                LogDebug($"Edited AI response from \"{input.ToUpper()}\" to \"{clipped}\"");
            return clipped;
        }

        private async Task<string> ProvideQuip(string qlPrompt, int maxLength)
        {
            var prompt = $@"Below are some prompts and outlandish, funny, ridiculous answers to them.

Prompt: Something you can never have too many of
Funny Answer: Reasons to stay inside

Prompt: You know a restaurant is bad when the waiter says ""_______""
Funny Answer: ""I don't know, but we can find out!""

Prompt: What would you call your ANTI-social network
Funny Answer: Slankbook

Prompt: Your fish are bored! You should put a _______ in their tank to amuse them
Funny Answer: Shark

Prompt: {qlPrompt}
Funny Answer:";

            var result = await CompletionService.CompletePrompt(prompt, new ICompletionService.CompletionParameters
                {
                    Temperature = Config.Quiplash.GenTemp,
                    MaxTokens = 16,
                    TopP = 1,
                    FrequencyPenalty = 0.2,
                    PresencePenalty = 0.1,
                    StopSequences = new[] { "\n" }
                },
                completion =>
                {
                    var cleanText = CleanResult(completion.Text.Trim());
                    if (cleanText.Length > 0
                        && cleanText.Length <= maxLength
                        && !completion.Text.Contains("__")
                        && !qlPrompt.ToUpper().Contains(cleanText))
                        return true;

                    LogDebug($"Received unusable ProvideQuip response: \"{completion.Text.Trim()}\"");
                    return false;
                },
                maxTries: Config.Quiplash.MaxRetries,
                defaultResponse: "");

            return CleanResult(result.Text.Trim(), true);
        }

        protected async Task<int> ProvideFavorite(string qlPrompt, IReadOnlyList<string> quips)
        {
            var options = "";

            for(var i = 0; i < quips.Count; i++)
                options += $"{i + 1}. {quips[i]}\n";

            var prompt = $@"I was playing a game of Quiplash, and the prompt was ""{qlPrompt}"". My options were:

{options}
The funniest was prompt number: ";

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
                    "EIGHT" => 8, // Game should have a max of eight options to choose from (in quiplash 1/2)
                    _ => throw new FormatException() // Response was something unhandled here
                };
            }

            var result = await CompletionService.CompletePrompt(prompt, new ICompletionService.CompletionParameters
                {
                    Temperature = Config.Quiplash.VoteTemp,
                    MaxTokens = 1,
                    TopP = 1,
                    StopSequences = new[] { "\n" }
                }, completion =>
                {
                    try
                    {
                        var answer = IntParseExt(completion.Text.Trim());
                        if (0 < answer && answer <= quips.Count) return true;
                    }
                    catch (FormatException)
                    {
                        // pass
                    }

                    LogDebug($"Received unusable ProvideFavorite response: {completion.Text.Trim()}");
                    return false;
                },
                maxTries: Config.Quiplash.MaxRetries,
                defaultResponse: "");

            if (result.Text != "")
                return IntParseExt(result.Text.Trim()) - 1;

            LogDebug("Received only unusable ProvideFavorite responses. Choice will be chosen randomly");
            return new Random().Next(quips.Count);
        }

        protected async Task<string> FormQuip(string prompt, int maxLength = 45)
        {
            var cleanedPrompt = CleanPromptForEntry(prompt);

            var quip = await ProvideQuip(cleanedPrompt, maxLength);
            if (quip != "")
                LogInfo($"GPT responded to \"{cleanedPrompt}\" with \"{quip}\"");

            return quip;
        }

        protected async Task<int> FormVote(string prompt, IReadOnlyList<string> choices)
        {
            var cleanedPrompt = CleanPromptForSelection(prompt);
            var cleanedChoices = choices.Select(CleanQuipForSelection).ToList();

            var favorite = await ProvideFavorite(cleanedPrompt, cleanedChoices);
            LogDebug($"Choosing \"{cleanedChoices[favorite]}\"");

            return favorite;
        }

        protected virtual string CleanPromptForEntry(string prompt)
        {
            return prompt.Replace("<BLANK>", "_______").StripHtml();
        }

        protected virtual string CleanPromptForSelection(string prompt)
        {
            return prompt.StripHtml().Replace("<BLANK>", "_______");
        }

        protected virtual string CleanQuipForSelection(string quip)
        {
            return HttpUtility.HtmlDecode(quip);
        }
    }
}
