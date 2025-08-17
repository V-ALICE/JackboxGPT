using JackboxGPT.Games.Common.Models;
using JackboxGPT.Games.Bracketeering;
using JackboxGPT.Games.Bracketeering.Models;
using JackboxGPT.Services;
using Serilog;
using static JackboxGPT.Services.ICompletionService;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using JackboxGPT.Extensions;

namespace JackboxGPT.Engines;

public class BracketeeringEngine : BaseJackboxEngine<BracketeeringClient>
{
    protected override string Tag => "bracketeering";
    protected override ManagedConfigFile.EnginePreference EnginePref => Config.Bracketeering.EnginePreference;

    protected string LastVote = "";
    protected string LastPrompt = "";
    protected string LastResponse = "";

    public BracketeeringEngine(ICompletionService completionService, ILogger logger, BracketeeringClient client, ManagedConfigFile configFile, int instance)
        : base(completionService, logger, client, configFile, instance)
    {
        // Custom personality setup needed because names are short in bracketeer
        if (configFile.Bracketeering.EnginePreference != ManagedConfigFile.EnginePreference.Completion &&
            configFile.Bracketeering.ChatPersonalityChance > RandGen.NextDouble())
        {
            var choice = Config.Model.ChatPersonalityTypes[Config.Model.ChatPersonalityTypes.RandomIndex()].ToLower();
            CompletionService.ApplyPersonalityType(choice);

            var split = choice.Split('`');
            var name = (split.Length == 1) ? choice.ToUpper() : split[1].ToUpper();

            var instanceName = $"{name[0]}{Instance}";
            InstanceName = instanceName.PadLeft(12);
            JackboxClient.SetName(instanceName);
            LogInfo($"Applying personality \"{choice}\"");
        }
        else
        {
            var instanceName = $"{configFile.General.PlayerName[0]}{Instance}";
            InstanceName = instanceName.PadLeft(12);
            JackboxClient.SetName(instanceName);
        }

        JackboxClient.OnSelfUpdate += OnSelfUpdate;
        JackboxClient.Connect();
    }

    private void OnSelfUpdate(object sender, Revision<BracketeeringPlayer> revision)
    {
        if (revision.New.Error != null)
            LogInfo(revision.New.Error);

        if (revision.New.State == State.EnterSingleText)
        {
            FormEntry(revision.New);
        }
        else if (revision.New.State == State.MakeSingleChoice)
        {
            ChooseFavorite(revision.New);
        }
    }

    private async void FormEntry(BracketeeringPlayer self)
    {
        var prompt = CleanPromptForEntry(self.Text);
        if (self.Error == null && prompt == LastPrompt && self.EntryId != "entry2")
        {
            // Allow retries because API is finicky sometimes
            JackboxClient.WriteEntry(LastResponse);
            await Task.Delay(2*Config.Bracketeering.VoteDelayMaxMs);
            return;
        }

        LogInfo($"Asking GPT for response to \"{prompt}\"");
        var response = await ProvideResponse(prompt, self.MaxLength);
        LogInfo($"Submitting response \"{response}\"");
        JackboxClient.WriteEntry(response);

        LastVote = "";
        LastPrompt = prompt;
        LastResponse = response;
    }

    private async void ChooseFavorite(BracketeeringPlayer self)
    {
        var choices = self.Choices.Select(choice => choice.Text).ToList();
        if (choices.Count == 0) // Tie breaker between things that this player didn't vote for
            return;

        if (choices.Count == 1) // Skip tutorial or tie breaker
        {
            await Task.Delay(RandGen.Next(Config.Bracketeering.VoteDelayMinMs, Config.Bracketeering.VoteDelayMaxMs));
            JackboxClient.ChooseIndex(0);
            return;
        }

        if (self.Error == null && choices.Contains(LastVote))
        {
            // Allow retries because API is finicky
            JackboxClient.ChooseIndex(choices.IndexOf(LastVote));
            await Task.Delay(2 * Config.Bracketeering.VoteDelayMaxMs);
            return;
        }

        await Task.Delay(RandGen.Next(2*Config.Bracketeering.VoteDelayMinMs, 2*Config.Bracketeering.VoteDelayMaxMs));
        var prompt = CleanPromptForEntry(self.Text);
        var favorite = await ProvideFavorite(prompt, choices);
        LogDebug($"Choosing \"{choices[favorite]}\"");
        JackboxClient.ChooseIndex(favorite);

        LastVote = choices[favorite];
        LastPrompt = "";
        LastResponse = "";
    }

    private string CleanResult(string input, bool logChanges = false)
    {
        var clipped = input;//.ToUpper();

        // Characters that often indicate that the answer will be unreasonable to try to use
        var badMarkers = new[] { '[', ']', '{', '}' };
        if (badMarkers.Any(c => input.Contains(c)))
            return "";

        // Characters that shouldn't be in a submitted answer
        var removals = new[] { "\n", "\r", "\t", "...", "`", "\\", "\"", "“", "”", "?", "!" };
        foreach (var r in removals)
            clipped = clipped.Replace(r, null);

        // Characters that mark the end of a reasonable answer in this case
        var splitPoints = new[] { ';', ':', '–', '—', '(' };
        foreach (var s in splitPoints)
            clipped = clipped.Split(s)[0];

        // Characters that shouldn't be on the front or back of a submitted answer
        var endRemovals = new[] { '.', ' ' };
        clipped = clipped.Trim(endRemovals);

        // Remove any double spaces that previous changes may have created (again)
        clipped = clipped.Trim().Replace("  ", " ");

        if (logChanges && input.Length != clipped.Length)
            LogDebug($"Edited AI response from \"{input}\" to \"{clipped}\"");
        return clipped;
    }

    private async Task<string> ProvideResponse(string entryPrompt, int maxLength)
    {
        var prompt = new TextInput
        {
            ChatSystemMessage = "You are a player in a game called Bracketeering, in which players attempt to write silly responses to prompts. Please respond to the prompt with just your very short answer, do not repeat the prompt.",
            ChatStylePrompt = $"Here's a new prompt: {entryPrompt}",
            CompletionStylePrompt =
                $@"Here are some prompts from the game Bracketeering, in which players attempt to write silly responses to prompts.

Prompt: MOST SATISFYING THING TO ANGRILY THROW INTO THE MOUTH OF A VOLCANO
Response: Middle School Bully

Prompt: BEST JOB TO STEAL SUPPLIES FROM
Response: Diamond Inspector

Prompt: {entryPrompt}
Response:",
        };
        var useChatEngine = UsingChatEngine;
        LogVerbose($"Prompt:\n{(useChatEngine ? prompt.ChatStylePrompt : prompt.CompletionStylePrompt)}", true);

        var result = await CompletionService.CompletePrompt(prompt, useChatEngine, new CompletionParameters
        {
            Temperature = Config.Bracketeering.GenTemp,
            MaxTokens = 16,
            FrequencyPenalty = 0.2,
            StopSequences = new[] { "\n" }
        },
            completion =>
            {
                var cleanText = CleanResult(completion.Text.Trim());
                if (cleanText.Length > 0 && cleanText.Length <= maxLength) return true;

                LogDebug($"Received unusable ProvideResponse response: \"{completion.Text.Trim()}\"");
                return false;
            },
            maxTries: Config.Bracketeering.MaxRetries,
            defaultResponse: "");

        return CleanResult(result.Text.Trim(), true);
    }

    protected async Task<int> ProvideFavorite(string choicePrompt, IReadOnlyList<string> entries)
    {
        if (RandGen.NextDouble() > Config.Model.VotingStrayChance)
            return new Random().Next(entries.Count);

        var options = "";

        for (var i = 0; i < entries.Count; i++)
            options += $"{i + 1}. {entries[i]}\n";

        var prompt = new TextInput
        {
            ChatSystemMessage = $"You are a player in a game called Bracketeering, in which players attempt to come up with silly responses to prompts. You will be given two responses for the prompt \"{choicePrompt}\", please respond with only the number corresponding to the option that you think is the best.",
            ChatStylePrompt = options,
            CompletionStylePrompt = $@"I was given a list of responses to the prompt ""{choicePrompt}"". These were my options:

{options}
I think the best response is answer number: ",
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
                "TWO" => 2, // Only ever two options
                _ => throw new FormatException() // Response was something unhandled here
            };
        }

        var result = await CompletionService.CompletePrompt(prompt, Config.Model.UseChatEngineForVoting, new CompletionParameters
        {
            Temperature = 0.5,
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

            LogDebug($"Received unusable ProvideFavorite response: {completion.Text.Trim()}");
            return false;
        },
        maxTries: Config.Bracketeering.MaxRetries,
        defaultResponse: "");

        CompletionService.ResetOne(prompt.ChatStylePrompt);
        if (result.Text != "")
            return IntParseExt(result.Text.Trim()) - 1;

        LogDebug("Received only unusable ProvideFavorite responses. Choice will be chosen randomly");
        return new Random().Next(entries.Count);
    }

    private static string CleanPromptForEntry(string prompt)
    {
        const string marker = "</div>";
        var idx = prompt.IndexOf(marker, StringComparison.Ordinal);
        if (idx == -1) return prompt.StripHtml(); // This shouldn't happen
        
        return prompt[..idx].StripHtml();
    }
}