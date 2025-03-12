using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JackboxGPT.Extensions;
using JackboxGPT.Games.Common;
using JackboxGPT.Games.Common.Models;
using JackboxGPT.Games.SurviveTheInternet;
using JackboxGPT.Games.SurviveTheInternet.Models;
using JackboxGPT.Services;
using Serilog;
using static JackboxGPT.Services.ICompletionService;

namespace JackboxGPT.Engines
{
    public class SurviveTheInternetEngine : BaseJackboxEngine<SurviveTheInternetClient>
    {
        protected override string Tag => "survivetheinternet";
        protected override ManagedConfigFile.EnginePreference EnginePref => Config.SurviveTheInternet.EnginePreference;

        private readonly ImageDescriptionProvider _descriptionProvider;

        public SurviveTheInternetEngine(ICompletionService completionService, ILogger logger, SurviveTheInternetClient client, ManagedConfigFile configFile, int instance, uint coinFlip)
            : base(completionService, logger, client, configFile, instance)
        {
            _descriptionProvider = new ImageDescriptionProvider("sti_image_descriptions.json");

            JackboxClient.OnSelfUpdate += OnSelfUpdate;
            JackboxClient.Connect();
        }

        private void OnSelfUpdate(object sender, Revision<SurviveTheInternetPlayer> revision)
        {
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (revision.New.State)
            {
                case RoomState.EnterSingleText when !revision.New.Entry:
                    SubmitEntry(revision.New);
                    break;
                case RoomState.Voting when revision.New.Chosen == null:
                    SubmitVote(revision.New);
                    break;
                case RoomState.MakeSingleChoice when !revision.New.Entry:
                    SubmitImageChoice(revision.New);
                    break;
            }
        }

        private void SubmitImageChoice(SurviveTheInternetPlayer player)
        {
            var choice = player.ImageChoices.RandomIndex();
            JackboxClient.ChooseIndex(choice);
        }

        private async void SubmitVote(SurviveTheInternetPlayer player)
        {
            var entries = player.EntryChoices.Select(c => $"{c.Header}\n{c.Body}\n{c.Footer}").ToList();
            var vote = await ProvideVote(entries);

            LogDebug($"Choosing \"{entries[vote]}\"");
            JackboxClient.ChooseIndex(vote);
        }

        private async void SubmitEntry(SurviveTheInternetPlayer player)
        {
            string entry;

            if (player.Text.AboveBlackBox != null && player.Text.AboveBlackBox.StartsWith("<img"))
                entry = await ProvideImageTwist(player.Text, player.MaxLength);
            else
                entry = player.EntryId switch
                {
                    "response" => await ProvideResponse(player.Text.BelowBlackBox, player.MaxLength),
                    "twist" => await ProvideTwist(player.Text, player.MaxLength),
                    _ => "I don't know how to respond to that"
                };

            LogInfo($"Submitting entry \"{entry}\"");

            JackboxClient.SendEntry(entry);
        }

        private string CleanResult(string input, bool logChanges = false)
        {
            // AI likes to self censor sometimes
            if (input.Contains("**")) return "";

            var clipped = input.ToUpper();
            if (input.Contains('#'))
                clipped = input[..clipped.IndexOf('#')];

            // Characters that shouldn't be in a submitted answer
            var removals = new[] { "\n", "\r", "\t"};
            foreach (var r in removals)
                clipped = clipped.Replace(r, null);

            // Characters that shouldn't be on the front or back of a submitted answer  (AI really likes using !)
            var endRemovals = new[] { '.', ' ', ',', '"', '!' };
            clipped = clipped.Trim(endRemovals);

            // Remove any double spaces that previous changes may have created
            clipped = clipped.Trim().Replace("  ", " ");

            if (logChanges && input.Length != clipped.Length)
                LogDebug($"Edited AI response from \"{input.ToUpper()}\" to \"{clipped}\"");
            return clipped;
        }

        private async Task<string> ProvideResponse(string stiPrompt, int maxLength)
        {
            var prompt = new TextInput
            {
                ChatSystemMessage = "You are a player in a game called Survive the Internet, in which players attempt to create silly posts that might appear on the internet. Please respond to the prompt with only your concise answer.",
                ChatStylePrompt = $"Here's a new prompt: {stiPrompt}",
                CompletionStylePrompt = $@"In the first part of the game Survive the Internet, players are asked questions which they should answer short and concisely. For example:

Q: How's your retirement fund doing?
A: It's nonexistant.

Q: What are your thoughts on professional wrestling?
A: It's all so fake.

Q: Describe an attitude you admire.
A: I love positive people.

Q: {stiPrompt}
A:",
            };
            var useChatEngine = UsingChatEngine;
            LogVerbose($"Prompt:\n{(useChatEngine ? prompt.ChatStylePrompt : prompt.CompletionStylePrompt)}");

            var result = await CompletionService.CompletePrompt(prompt, useChatEngine, new CompletionParameters
                {
                    Temperature = Config.SurviveTheInternet.GenTemp,
                    MaxTokens = 32,
                    FrequencyPenalty = 0.3,
                    PresencePenalty = 0.2,
                    StopSequences = new[] { "\n" }
                },
                completion =>
                {
                    var cleanText = CleanResult(completion.Text.Trim());
                    if (cleanText.Length > 0 && cleanText.Length <= maxLength && !cleanText.Contains(stiPrompt.ToUpper().Trim()))
                        return true;

                    LogDebug($"Received unusable ProvideResponse response: \"{completion.Text.Trim()}\"");
                    return false;
                },
                maxTries: Config.SurviveTheInternet.MaxRetries,
                defaultResponse: "Default response");

            return CleanResult(result.Text.Trim());
        }
        
        private async Task<string> ProvideTwist(TextPrompt stiPrompt, int maxLength)
        {
            var prompt = new TextInput
            {
                ChatSystemMessage = "You are a player in a game called Survive the Internet, in which players attempt to create silly posts that might appear on the internet. Please respond to the prompt with only your concise answer.",
                ChatStylePrompt = $"Here's a new prompt: \"{stiPrompt.BlackBox}\" {stiPrompt.BelowBlackBox.ToLower().Trim()}",
                CompletionStylePrompt = $@"Below are some responses from the party game Survive the Internet. The goal of this game is to take another player's words and twist them to make the other player look ridiculous.

""I'm skeptical"" would be a ridiculous response to this comment: She said yes!
""Too much nudity"" would be a ridiculous comment to a video titled: How to Play Guitar
""Yawn"" would be a terrible comment in response to this news headline: Bank Robber on the Loose
""The bathroom"" would be a ridiculous answer to this question: Where do you cry the most?
""Let's hunt him down"" would be a terrible comment in response to this news headline: Local Man Wins Lottery
""Not that impressive tbh"" would be a ridiculous comment to a video titled: Johnny Learns How to Ride a Bike!
""It's not the most comfortable thing to sit on"" would be a ridiculous review for a product called: 18-inch Wooden Spoon
""{stiPrompt.BlackBox}"" {stiPrompt.BelowBlackBox.ToLower().Trim()}",
            };
            var useChatEngine = UsingChatEngine;
            LogVerbose($"Prompt:\n{(useChatEngine ? prompt.ChatStylePrompt : prompt.CompletionStylePrompt)}");

            var result = await CompletionService.CompletePrompt(prompt, useChatEngine, new CompletionParameters
                {
                    Temperature = Config.SurviveTheInternet.GenTemp,
                    MaxTokens = 32,
                    FrequencyPenalty = 0.3,
                    PresencePenalty = 0.2,
                    StopSequences = new[] { "\n" }
                },
                completion =>
                {
                    var cleanText = CleanResult(completion.Text.Trim());
                    if (cleanText.Length > 0 && cleanText.Length <= maxLength && !cleanText.Contains(stiPrompt.BelowBlackBox))
                        return true;

                    LogDebug($"Received unusable ProvideTwist response: \"{completion.Text.Trim()}\"");
                    return false;
                },
                maxTries: Config.SurviveTheInternet.MaxRetries,
                defaultResponse: "Default response");

            return CleanResult(result.Text.Trim());
        }
        
        private async Task<string> ProvideImageTwist(TextPrompt stiPrompt, int maxLength)
        {
            var description = _descriptionProvider.ProvideDescriptionForImageId(GetImageId(stiPrompt));

            var prompt = new TextInput
            {
                ChatSystemMessage = "You are a player in a game called Survive the Internet, in which players attempt to create silly posts that might appear on the internet. Please respond to the prompt with only your concise answer.",
                ChatStylePrompt = $"Here's a new prompt: An absurd and ridiculous Instagram caption for a photo of {description}:",
                CompletionStylePrompt = $@"Below are some responses from the party game Survive the Internet. In the final round, each player takes an image and tries to come up with a caption that would make the other players look crazy or ridiculous.

An absurd and ridiculous Instagram caption for a photo of a group of mailboxes, with one open: Learned how to lock pick earlier. Score!
An absurd and ridiculous Instagram caption for a photo of people's legs through bathroom stalls: Just asked these guys how they were doing. They didn't respond.
An absurd and ridiculous Instagram caption for a photo of a group of people posing for a photo at a funeral: Funeral? I thought this was a party.
An absurd and ridiculous Instagram caption for a photo of {description}:",
            };
            var useChatEngine = UsingChatEngine;
            LogVerbose($"Prompt:\n{(useChatEngine ? prompt.ChatStylePrompt : prompt.CompletionStylePrompt)}");

            var result = await CompletionService.CompletePrompt(prompt, useChatEngine, new CompletionParameters
                {
                    Temperature = Config.SurviveTheInternet.GenTemp,
                    MaxTokens = 32,
                    FrequencyPenalty = 0.3,
                    PresencePenalty = 0.2,
                    StopSequences = new[] { "\n" }
                },
                completion =>
                {
                    var cleanText = CleanResult(completion.Text.Trim());
                    if (cleanText.Length > 0 && cleanText.Length <= maxLength && !cleanText.Contains(description.ToUpper()))
                        return true;

                    LogDebug($"Received unusable ProvideImageTwist response: \"{completion.Text.Trim()}\"");
                    return false;
                },
                maxTries: Config.SurviveTheInternet.MaxRetries,
                defaultResponse: "Default response");

            return CleanResult(result.Text.Trim());
        }

        private async Task<int> ProvideVote(IReadOnlyList<string> entries)
        {
            var options = "";

            for (var i = 0; i < entries.Count; i++)
                options += $"{i + 1}. {entries[i]}\n";

            var prompt = new TextInput
            {
                ChatSystemMessage = "You are a player in a game called Survive the Internet, in which players attempt to create silly posts that might appear on the internet. Please respond with only the number corresponding with the option that you think is the funniest post.",
                ChatStylePrompt = options,
                CompletionStylePrompt = $@"I was playing a game of Survive the Internet, and needed to choose my favorite funny post. My options were:

{options}
The funniest was post number: ",
            };
            LogVerbose($"Prompt:\n{(Config.Model.UseChatEngineForVoting ? prompt.ChatStylePrompt : prompt.CompletionStylePrompt)}");

            int IntParseExt(string input)
            {
                if (input.Length < 1) throw new FormatException();

                // Assume the response is int-parsable if it starts with a digit character
                if (char.IsDigit(input[0])) return int.Parse(new string(input.TakeWhile(char.IsDigit).ToArray()));

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
                    "EIGHT" => 8, // I don't know max options for StI
                    _ => throw new FormatException() // Response was something unhandled here
                };
            }

            var result = await CompletionService.CompletePrompt(prompt, Config.Model.UseChatEngineForVoting, new CompletionParameters
            {
                Temperature = Config.SurviveTheInternet.VoteTemp,
                MaxTokens = 1,
                StopSequences = new[] { "\n" }
            }, completion =>
            {
                try
                {
                    var answer = IntParseExt(completion.Text.Trim());
                    if (0 < answer && answer <= entries.Count) return true;
                }
                catch (FormatException)
                {
                    // pass
                }

                LogDebug($"Received unusable ProvideVote response: {completion.Text.Trim()}");
                return false;
            },
                maxTries: Config.SurviveTheInternet.MaxRetries,
                defaultResponse: "");

            CompletionService.ResetOne(prompt.ChatSystemMessage);
            if (result.Text != "")
                return IntParseExt(result.Text.Trim()) - 1;

            LogDebug("Received only unusable ProvideVote responses. Choice will be chosen randomly");
            return new Random().Next(entries.Count);
        }

        private static string GetImageId(TextPrompt stiPrompt)
        {
            const string pattern = @"[A-z]+\.jpg";
            var match = Regex.Match(stiPrompt.AboveBlackBox, pattern);
            return match.Success ? match.Value : "Baseball.jpg"; // why not
        }
    }
}