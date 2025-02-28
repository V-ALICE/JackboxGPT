using System.Linq;
using System;
using System.Collections.Generic;
using JackboxGPT.Games.Common.Models;
using JackboxGPT.Games.JokeBoat;
using JackboxGPT.Games.JokeBoat.Models;
using JackboxGPT.Services;
using Serilog;
using System.Threading.Tasks;
using JackboxGPT.Extensions;
using static JackboxGPT.Services.ICompletionService;
using System.Web;

namespace JackboxGPT.Engines;

public class JokeBoatEngine : BaseJackboxEngine<JokeBoatClient>
{
    protected override string Tag => "jokeboat";

    // How many topics to generate at the beginning of a game (per AI player)
    // More topics = more GPT tokens used
    private int _topicsCount;

    public JokeBoatEngine(ICompletionService completionService, ILogger logger, JokeBoatClient client, ManagedConfigFile configFile, int instance, uint coinFlip)
        : base(completionService, logger, client, configFile, instance)
    {
        UseChatEngine = configFile.JokeBoat.EnginePreference == ManagedConfigFile.EnginePreference.Chat
                        || (configFile.JokeBoat.EnginePreference == ManagedConfigFile.EnginePreference.Mix && instance % 2 == coinFlip);
        LogDebug($"Using {(UseChatEngine ? "Chat" : "Completion")} engine");
        CompletionService.ResetAll();

        JackboxClient.OnSelfUpdate += OnSelfUpdate;
        JackboxClient.Connect();
    }

    private void OnSelfUpdate(object sender, Revision<JokeBoatPlayer> revision)
    {
        if (revision.New.Error != null)
        {
            if (revision.New.Error == "Your entry was already submitted, please try another")
            {
                // duplicate topic idea, doesn't matter
            }
            else if (revision.New.Error == "Your entry was too long, please try again.")
            {
                // will try again automatically, not sure why this happens though 
            }
            else
            {
                LogWarning($"Received error from game: \"{revision.New.Error}\"");
            }
        }

        if (revision.New.State == State.MakeSingleChoice)
        {
            if (revision.New.ChoiceId == "ChooseCatchphrase")
            {
                ChooseRandomCatchphrase(revision.New);
            }
            else switch (revision.New.ChoiceType)
            {
                case ChoiceType.ChooseSetup:
                    ChooseRandomSetup(revision.New);
                    break;
                case ChoiceType.ChooseTopic:
                    ChooseRandomTopic(revision.New);
                    break;
                case ChoiceType.ChooseAuthorReady:
                    JackboxClient.ChooseIndex(1); // Assuming "Perform the joke for me" is always index 1
                    break;
                case ChoiceType.ChooseJoke when revision.New.Chosen == 0: // Used for both normal rounds and punch up rounds
                    ChooseFavorite(revision.New);
                    break;
                case ChoiceType.ChoosePunchUpJoke:
                    ChooseRandomPunchUp(revision.New);
                    break;
            }
        }
        else if (revision.New.State == State.EnterSingleText)
        {
            if (revision.New.EntryId.StartsWith("Topic"))
            {
                SubmitTopic(revision.New);
            }
            else if (revision.New.EntryId.StartsWith("Punchline"))
            {
                SubmitPunchline(revision.New, true);
            }
            else if (revision.New.EntryId.StartsWith("PunchedUpLine"))
            {
                SubmitPunchline(revision.New, false);
            }
        }
    }

    private void ChooseRandomCatchphrase(JokeBoatPlayer self)
    {
        var choices = self.Choices;
        var catchphrase = choices.RandomIndex();
        LogDebug($"Choosing catchphrase \"{choices[catchphrase].Html.StripHtml()}\".");

        JackboxClient.ChooseIndex(catchphrase);
    }

    private void ChooseRandomSetup(JokeBoatPlayer self)
    {
        var choices = self.Choices;
        var setup = choices.RandomIndex();
        LogDebug($"Choosing setup \"{choices[setup].Text}\".");

        JackboxClient.ChooseIndex(setup);
    }

    private void ChooseRandomTopic(JokeBoatPlayer self)
    {
        var choices = self.Choices;
        var topic = new Random().Next(choices.Count - 1); // Last entry in topics list is the reset box
        LogDebug($"Choosing topic \"{choices[topic].Text}\".");

        JackboxClient.ChooseIndex(topic);
    }

    private void ChooseRandomPunchUp(JokeBoatPlayer self)
    {
        var choices = self.Choices;
        var punchUp = choices.RandomIndex();
        LogDebug($"Choosing PunchUp joke \"{choices[punchUp].Html.StripHtml()}\".");

        JackboxClient.ChooseIndex(punchUp);
    }

    private async void ChooseFavorite(JokeBoatPlayer self)
    {
        var choices = self.Choices.Select(choice => HttpUtility.HtmlDecode(choice.Html.StripHtml())).ToList();
        var prompt = self.Classes.Count == 0 ? CleanJokePromptForEntry(self.Prompt.Html) : "";

        var favorite = await ProvideFavorite(choices, prompt);
        LogDebug($"Choosing \"{choices[favorite]}\"");

        JackboxClient.ChooseIndex(favorite);
    }

    private async void SubmitTopic(JokeBoatPlayer self)
    {
        if (_topicsCount >= Config.JokeBoat.MaxTopicGenCount) return;
        await Task.Delay(30000 / Config.JokeBoat.MaxTopicGenCount); // Don't spam topics

        var topic = await ProvideTopic(self.Placeholder, self.MaxLength);
        if (topic == "") return; // If topic generation fails 5 times in a row just give up

        LogInfo($"GPT submitted {self.Placeholder}: \"{topic}\"");
        JackboxClient.SubmitEntry(topic);
        _topicsCount++;
    }

    private async void SubmitPunchline(JokeBoatPlayer self, bool allowHelp)
    {
        var prompt = CleanJokePromptForEntry(self.Prompt.Html);
        var punchline = await ProvidePunchline(prompt, self.MaxLength);

        if (allowHelp && punchline == "")
        {
            LogDebug("Submitting a default answer because there were too many generation failures.");
            JackboxClient.RequestJokeForMe();
        }
        else if (!allowHelp && punchline == "")
        {
            LogDebug("Submitting a non-answer because there were too many generation failures.");
            JackboxClient.SubmitEntry("NO RESPONSE");
        }
        else
        {
            LogInfo($"GPT submitted \"{punchline}\" as the punchline for \"{prompt}\"");
            JackboxClient.SubmitEntry(punchline);
        }
    }

    private string CleanResultStrict(string input, bool logChanges = false)
    {
        var clipped = input;//.ToUpper();

        // Characters that often indicate that the answer will be unreasonable to try to use
        var badMarkers = new[] { '(', ')', '/', '[', ']', '{', '}' };
        if (badMarkers.Any(c => input.Contains(c)))
        {
            return "";
        }

        // Characters that shouldn't be in a submitted answer
        var removals = new[] { "\n", "\r", "\t", "...", "`", "\\", "\"", "“", "”", "?", "!", ",", ";", ":" };
        foreach (var r in removals)
            clipped = clipped.Replace(r, null);

        // Characters that shouldn't be on the front or back of a submitted answer
        var endRemovals = new[] { '.', ' ' };
        clipped = clipped.Trim(endRemovals);

        // Remove any double spaces that previous changes may have created (again)
        clipped = clipped.Trim().Replace("  ", " ");

        if (logChanges && input.Length != clipped.Length)
            LogDebug($"Edited AI response from \"{input}\" to \"{clipped}\"");
        return clipped;
    }

    private string CleanResult(string input, string prompt = "", bool logChanges = false)
    {
        //input = input.ToUpper();
        //prompt = prompt.ToUpper();

        // Don't accept results that are entirely contained in the prompt
        if (prompt.Length > 0 && prompt.Contains(input))
            return "";

        // Characters that often indicate that the answer will be unreasonable to try to use
        var badMarkers = new[] { ';', ':' };
        if (badMarkers.Any(c => input.Contains(c)))
        {
            return "";
        }

        // Characters that mark the end of a reasonable answer
        var clipMarkers = new[] { '?', '!' };
        var clipIdx = input.IndexOfAny(clipMarkers)+1;
        var clipped = clipIdx > 0 ? input[..clipIdx] : input;

        // An odd amount of quotes implies that something got cut off, in which case this answer isn't reasonable anymore
        clipped = clipped.Replace('“', '"').Replace('”', '"');
        if (input.Contains('"') && input.Split('"').Length % 2 == 0)
            return "";

        // Characters that shouldn't be in a submitted answer
        var removals = new[] { "\n", "\r", "\t", "[", "]", "{", "}", "`", "\\" };
        foreach (var r in removals)
            clipped = clipped.Replace(r, null);

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

    // TODO: make this generate a list like blather round does? Then repeated types wouldn't have to use another generation
    private async Task<string> ProvideTopic(string topicPrompt, int maxLength)
    {
        var prompt = new TextInput
        {
            ChatSystemMessage = "You are a player in a game called Joke Boat, in which players attempt to form silly jokes. You will be given a prompt for a topic of a certain type, please respond with just your topic.",
            ChatStylePrompt = $"How about {topicPrompt}",
            CompletionStylePrompt = $@"Here are some prompts for specific types of words/phrases.

Q: a person’s name
A: Franklin Reynolds

Q: a brand
A: McDonalds

Q: a plural noun
A: French bread pizzas

Q: a drink
A: Milk

Q: {topicPrompt}
A:",
        };
        LogVerbose($"Prompt:\n{(UseChatEngine ? prompt.ChatStylePrompt : prompt.CompletionStylePrompt)}");

        var result = await CompletionService.CompletePrompt(prompt, UseChatEngine, new CompletionParameters
            {
                Temperature = Config.JokeBoat.GenTemp,
                MaxTokens = 16,
                TopP = 1,
                FrequencyPenalty = 0.2,
                StopSequences = new[] { "\n" }
            },
            completion =>
            {
                var cleanText = CleanResultStrict(completion.Text.Trim());
                if (cleanText.Length > 0 && cleanText.Length <= maxLength && !cleanText.Contains("__")) return true;

                LogDebug($"Received unusable ProvideTopic response: \"{completion.Text.Trim()}\"");
                return false;
            },
            maxTries: Config.JokeBoat.MaxRetries,
            defaultResponse: "");

        return CleanResultStrict(result.Text.Trim(), true);
    }

    private async Task<string> ProvidePunchline(string jokePrompt, int maxLength)
    {
        // The person writing this code (and the prompts) is not a comedian
        var prompt = new TextInput
        {
            ChatSystemMessage = "You are a player in a game called Joke Boat, in which players attempt to form silly jokes. You'll be going up against another player, so try to be original. You will be given the first half of a joke, please respond with only a punchline.",
            ChatStylePrompt = $"Here's a new prompt: {jokePrompt.Replace("_", null)}", // Chat talks to much when given blanks for some reason
            CompletionStylePrompt = $@"Below are some joke setups and outlandish, funny, ridiculous punchlines for them.

Q: I like my pants like I like my CATS: _______
A: Rubbing up against my legs

Q: Why are they called LIGHTBULBS... and not _______
A: magical orbs

Q: What’s the difference between the majority of people and PIZZAS: _______
A: People usually aren't quite as cheesy

Q: {jokePrompt}
A:",
        };
        LogVerbose($"Prompt:\n{(UseChatEngine ? prompt.ChatStylePrompt : prompt.CompletionStylePrompt)}");

        var result = await CompletionService.CompletePrompt(prompt, UseChatEngine, new CompletionParameters
            {
                Temperature = Config.JokeBoat.GenTemp,
                MaxTokens = 24,
                TopP = 1,
                FrequencyPenalty = 0.2,
                StopSequences = new[] { "\n" }
            },
            completion =>
            {
                var cleanText = CleanResult(completion.Text.Trim(), jokePrompt);
                if (cleanText.Length > 0 && cleanText.Length <= maxLength && !cleanText.Contains("__")) return true;

                LogDebug($"Received unusable ProvidePunchline response: \"{completion.Text.Trim()}\"");
                return false;
            },
            maxTries: Config.JokeBoat.MaxRetries,
            defaultResponse: "");

        return CleanResult(result.Text.Trim(), jokePrompt, logChanges: true);
    }

    protected async Task<int> ProvideFavorite(IReadOnlyList<string> quips, string punchUpPrompt = "")
    {
        var options = "";

        for (var i = 0; i < quips.Count; i++)
            options += $"{i + 1}. {quips[i]}\n";

        TextInput prompt;
        if (punchUpPrompt.Length > 0)
        {
            prompt = new TextInput
            {
                ChatSystemMessage = $"You are a player in a game called Joke Boat, in which players attempt to form silly jokes. Please respond with the number corresponding to the option that you think is funniest punchline for the prompt \"{punchUpPrompt}\"",
                ChatStylePrompt = options,
                CompletionStylePrompt = $@"I was playing a game of Jackbox Joke Boat, and the joke was ""{punchUpPrompt}"". My options were:

{options}
The funniest was punchline number: ",
            };
        }
        else
        {
            prompt = new TextInput
            {
                ChatSystemMessage = "You are a player in a game called Joke Boat, in which players attempt to form silly jokes. You will be given two jokes, please respond with the number corresponding to the option that you think is the funniest.",
                ChatStylePrompt = options,
                CompletionStylePrompt = $@"I was playing a game of Jackbox Joke Boat, and needed to choose the best joke. My options were:

{options}
The funniest was joke number: ",
            };
        }
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
            Temperature = Config.JokeBoat.VoteTemp,
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
        maxTries: Config.JokeBoat.MaxRetries,
        defaultResponse: "");

        CompletionService.ResetOne(prompt.ChatStylePrompt);
        if (result.Text != "")
            return IntParseExt(result.Text.Trim()) - 1;

        LogDebug("Received only unusable ProvideFavorite responses. Choice will be chosen randomly");
        return new Random().Next(quips.Count);
    }

    private static string CleanJokePromptForEntry(string prompt)
    {
        const string marker = "\"marker\">";
        const string markerAlt = "'marker'>";
        var idx = prompt.IndexOf(marker, StringComparison.Ordinal);
        if (idx == -1) idx = prompt.IndexOf(markerAlt, StringComparison.Ordinal);
        if (idx == -1) return prompt.StripHtml(); // This shouldn't happen

        var promptStartIdx = idx + marker.Length; // Both markers are the same length so this fine
        var cleanedPrompt = prompt[promptStartIdx..].StripHtml();

        var removals = new[] { "“", "”", "\"", "?" }; // GPT seems to get confused by certain joke formattings
        foreach (var r in removals)
            cleanedPrompt = cleanedPrompt.Replace(r, null);
        return cleanedPrompt;
    }
}