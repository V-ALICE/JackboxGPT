using JackboxGPT.Games.Common.Models;
using JackboxGPT.Games.SurveyScramble;
using JackboxGPT.Games.SurveyScramble.Models;
using JackboxGPT.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JackboxGPT.Extensions;

namespace JackboxGPT.Engines;

public class SurveyScrambleEngine : BaseJackboxEngine<SurveyScrambleClient>
{
    protected override string Tag => "bigsurvey";

    private readonly Random _rand = new();

    private bool _dead; // Keeps the engine alive if an unsupported mode is selected
    private bool _promptShown; // Prevents the prompt from being spammed in some modes

    private SurveyScrambleRoundInfo _promptInfo; // Contains the prompt for the current game
    private readonly HashSet<string> _previouslyGuessed = new(); // Guesses thus far this game (resets every new game)

    // Guesses already generated for this game, use these before generating more
    private readonly Dictionary<PositionType, Queue<string>> _queuedGuesses = new()
    {
        {PositionType.High, new Queue<string>()},
        {PositionType.Low, new Queue<string>()},
        {PositionType.All, new Queue<string>()}
    };

    // Where on the list a response should be from
    private enum PositionType
    {
        High,
        Low,
        All
    }

    // How to split up AI for team modes
    private enum TeamSelectionMethod
    {
        Default,
        Split,
        Left,
        Right
    }
    private readonly TeamSelectionMethod _teamSelectionMethod;

    public SurveyScrambleEngine(ICompletionService completionService, ILogger logger, SurveyScrambleClient client, ManagedConfigFile configFile, int instance)
        : base(completionService, logger, client, configFile, instance)
    {
        switch (Config.SurveyScramble.TeamSelectionMethod)
        {
            case "DEFAULT":
                _teamSelectionMethod = TeamSelectionMethod.Default;
                break;
            case "SPLIT":
                _teamSelectionMethod = TeamSelectionMethod.Split;
                break;
            case "LEFT":
                _teamSelectionMethod = TeamSelectionMethod.Left;
                break;
            case "RIGHT":
                _teamSelectionMethod = TeamSelectionMethod.Right;
                break;
            default:
                LogWarning($"{Config.SurveyScramble.TeamSelectionMethod} is not a valid value for `survey_scramble.team_selection_method`", true);
                _teamSelectionMethod = TeamSelectionMethod.Default;
                break;
        }

        JackboxClient.OnSelfUpdate += OnSelfUpdate;
        JackboxClient.OnRoundInfoReceived += OnRoundInfoReceived;
        JackboxClient.Connect();
    }

    private void OnSelfUpdate(object sender, Revision<SurveyScramblePlayer> revision)
    {
        var self = revision.New;
        if (self.Kind == Kind.Choices && self.ResponseKey.StartsWith("voteResponse"))
        {
            // New game, reset everything
            LogDebug("Resetting everything for new game...", true, prefix: "\n");
            _dead = _promptShown = false;
            _previouslyGuessed.Clear();
            _queuedGuesses[PositionType.High].Clear();
            _queuedGuesses[PositionType.Low].Clear();
            _queuedGuesses[PositionType.All].Clear();
            _promptInfo = new SurveyScrambleRoundInfo();

            // Reply
            ChooseRandomTopic(self);
        }

        if (_dead) return;

        switch (self.Kind)
        {
            case Kind.Bounce:
                HandleBounceMode(self);
                break;
            case Kind.HighLow:
                HandleHighLowMode(self);
                break;
            case Kind.Speed:
                HandleSpeedMode(revision.New);
                break;
            case Kind.TicTacToe:
                HandleTicTacToeMode(self);
                break;
            case Kind.Choices:
                if (self.ResponseKey.StartsWith("objectGuess"))
                    HighLowChooseMostPopular(self);
                break;
            case Kind.TeamChoice:
                LockTeam(self);
                break;
        }
    }

    private void OnRoundInfoReceived(object sender, SurveyScrambleRoundInfo info)
    {
        _promptInfo = info;
        if (!_promptShown)
        {
            LogInfo($"The prompt for this round is \"{_promptInfo.LongPrompt}\"", true);
            _promptShown = true;
        }
    }

    private async void LockTeam(SurveyScramblePlayer self)
    {
        await Task.Delay(Config.SurveyScramble.TeamSelectionDelayMs);
        switch (_teamSelectionMethod)
        {
            case TeamSelectionMethod.Default: // Do nothing
                break;
            case TeamSelectionMethod.Split:
                JackboxClient.ChooseTeam(self.CurrentTeamName, Instance % 2 == 0);
                break;
            case TeamSelectionMethod.Left:
                JackboxClient.ChooseTeam(self.CurrentTeamName, true);
                break;
            case TeamSelectionMethod.Right:
                JackboxClient.ChooseTeam(self.CurrentTeamName, false);
                break;
        }
        await Task.Delay(Config.SurveyScramble.TeamLockDelayMs);
        JackboxClient.LockInTeam();
    }

    private void HandleBounceMode(SurveyScramblePlayer self)
    {
        LogError("SurveyScramble mode \"Bounce\" is not supported", true); // No way to know where the ball is
        _dead = true;
    }

    private async void HandleHighLowMode(SurveyScramblePlayer self)
    {
        if (_promptInfo.LongPrompt.Length == 0) return;

        // Determine round type
        var position = self.Goal == "Low" ? PositionType.Low : PositionType.High;

        // Generate more responses if needed
        if (_queuedGuesses[position].Count == 0)
        {
            LogDebug($"Requesting more responses for goal {self.Goal}...");
            var responses = await ProvideAnswers(_promptInfo.LongPrompt, self.Instructions, position);
            foreach (var response in responses) _queuedGuesses[position].Enqueue(response);

            // If no new responses are created (out of a theoretical 24 attempts), try taking from previous queue
            if (_queuedGuesses[position].Count == 0 && position == PositionType.Low)
            {
                while (_queuedGuesses[PositionType.High].Count > 0)
                {
                    var next = _queuedGuesses[PositionType.High].Dequeue();
                    if (!_previouslyGuessed.Contains(next))
                        _queuedGuesses[position].Enqueue(next);
                }
            }

            // If still no responses just skip this round
            if (_queuedGuesses[position].Count == 0)
            {
                LogWarning("GPT failed to come up with any responses, this round will be skipped");
                return;
            }
        }

        // Avoids spamming answers when none are working
        await Task.Delay(Config.SurveyScramble.ResponseMinDelayMs);

        // Send next response
        var guess = _queuedGuesses[position].Dequeue();
        LogInfo($"Submitting response \"{guess}\"");
        JackboxClient.Guess(guess);
        _previouslyGuessed.Add(guess);
    }

    private async void HandleSpeedMode(SurveyScramblePlayer self)
    {
        if (_promptInfo.LongPrompt.Length == 0) return;

        const PositionType position = PositionType.All;

        // Generate more responses if needed
        if (_queuedGuesses[position].Count == 0)
        {
            LogDebug("Requesting more responses...");
            var responses = await ProvideAnswers(_promptInfo.LongPrompt, self.Instructions, position);
            foreach (var response in responses) _queuedGuesses[position].Enqueue(response);

            // If no new responses are created (out of a theoretical 48 attempts), wait a while before trying again
            if (_queuedGuesses[position].Count == 0)
            {
                LogInfo("GPT failed to come up with any responses, delaying further generation");
                await Task.Delay(Config.SurveyScramble.SpeedGenFailDelayMs);
                _queuedGuesses[position].Enqueue("DEFAULTRESPONSE"); // This will trigger the server to request again
            }
        }

        // Don't let the AI be overpowered 
        await Task.Delay(_rand.Next(Config.SurveyScramble.ResponseMinDelayMs, Config.SurveyScramble.SpeedResponseMaxDelayMs));

        // Send next response
        var guess = _queuedGuesses[position].Dequeue();
        LogInfo($"Submitting response \"{guess}\"");
        JackboxClient.Guess(guess);
        _previouslyGuessed.Add(guess);
    }

    private async void HandleTicTacToeMode(SurveyScramblePlayer self)
    {
        if (_promptInfo.LongPrompt.Length == 0) return;
        if (self.Instructions != "YOU'RE UP!") return; // Not my turn, doesn't seem to a better way to determine this

        // Add any new team responses to history
        foreach (var entry in self.History)
            _previouslyGuessed.Add(entry.Text);

        // TODO: make smart? As in attempt to make three in a row rather than just random (would use self.History.Rank)
        const PositionType position = PositionType.All;

        // Use suggestions (only from human players) sometimes
        var suggestions = self.Suggestions.Where(suggestion => !_previouslyGuessed.Contains(suggestion.Text)).ToList();
        if (suggestions.Count > 0 && Config.SurveyScramble.TeamUseSuggestionChance > _rand.NextDouble())
        {
            // Avoids spamming answers when none are working
            await Task.Delay(Config.SurveyScramble.ResponseMinDelayMs);

            // Send next response
            var suggestion = suggestions[suggestions.RandomIndex()].Text;
            LogInfo($"Submitting suggested response \"{suggestion}\"");
            JackboxClient.GuessSuggestion(suggestion);
            _previouslyGuessed.Add(suggestion);
            return;
        }

        // Generate more responses if needed
        if (_queuedGuesses[position].Count == 0)
        {
            LogDebug("Requesting more responses...");
            var responses = await ProvideAnswers(_promptInfo.LongPrompt, self.Instructions, position);
            foreach (var response in responses) _queuedGuesses[position].Enqueue(response);

            // If no new responses are created (out of a theoretical 48 attempts), just skip this round
            if (_queuedGuesses[position].Count == 0)
            {
                LogWarning("GPT failed to come up with any responses, this round will be skipped");
                return;
            }
        }

        // Avoids spamming answers when none are working
        await Task.Delay(Config.SurveyScramble.ResponseMinDelayMs);

        // Send next response
        var guess = _queuedGuesses[position].Dequeue();
        LogInfo($"Submitting response \"{guess}\"");
        JackboxClient.Guess(guess);
        _previouslyGuessed.Add(guess);
    }

    private void ChooseRandomTopic(SurveyScramblePlayer self)
    {
        var choices = self.CategoryChoices;
        var category = _rand.Next(choices.Count);
        LogDebug($"Choosing category \"{choices[category]}\"");

        JackboxClient.ChooseCategory(category);
    }

    private async void HighLowChooseMostPopular(SurveyScramblePlayer self)
    {
        if (_promptInfo.LongPrompt.Length == 0) return;

        var choices = self.CategoryChoices;
        var category = await ProvideBest(_promptInfo.LongPrompt, choices);
        LogInfo($"Choosing most popular category \"{choices[category]}\"");

        JackboxClient.GuessCategory(category);
    }

    protected List<string> FilterResults(string input, int maxLen, bool logChanges = false)
    {
        // Split up result
        var inputs = input.Split(";");
        if (inputs.Length == 1)
        {
            inputs = input.Split(","); // GPT isn't supposed to do this but does sometimes anyway
            if (inputs.Length == 1) inputs = input.Split(" "); // Last-ditch effort
        }

        var cleaned = new List<string>();
        foreach (var entry in inputs)
        {
            // Remove everything besides letters and numbers
            var clipped = new string(entry.Where(char.IsLetterOrDigit).ToArray());
            if (clipped.Length == 0 || clipped.Length > maxLen) continue;
            if (_previouslyGuessed.Contains(clipped) || _queuedGuesses.Any(keyval => keyval.Value.Contains(clipped))) continue;

            if (logChanges && entry.Trim().Length != clipped.Length)
                LogDebug($"Edited AI response from \"{entry.Trim()}\" to \"{clipped}\"");
            cleaned.Add(clipped);
        }
        return cleaned;
    }

    private async Task<List<string>> ProvideAnswers(string surveyPrompt, string instructions, PositionType pos, int maxLength = 25)
    {
        // Prep example responses depending on prompt type
        string q1_high = "Sopranos; Office; BreakingBad; FireFly; Seinfeld; Friends", q1_low = "Shameless; Wentworth; Spartacus; Ezel; Yellowstone; Primal";
        string q2_high = "Waiter; Service; Check; Please; Refill; Menu", q2_low = "Pie; Burnt; Bug; Dash; Drunk; Register";
        string q3_high = "Alarm; Police; Gun; DashCam; Lock; Dog", q3_low = "Raccoon; Disgust; Bodyguard; Boot; Broken; Spikes";
        var inputs = new List<string>();
        switch (pos)
        {
            case PositionType.High:
                inputs.Add(q1_high);
                inputs.Add(q2_high);
                inputs.Add(q3_high);
                break;
            case PositionType.Low:
                inputs.Add(q1_low);
                inputs.Add(q2_low);
                inputs.Add(q3_low);
                break;
            case PositionType.All:
                inputs.Add($"{q1_high}; {q1_low}");
                inputs.Add($"{q2_high}; {q2_low}");
                inputs.Add($"{q3_high}; {q3_low}");
                break;
        }

        var prompt = $@"Below are some survey questions with reasonable responses to them.

Survey: What's a good single word TV show title? {instructions}
Responses: {inputs[0]}

Survey: What's a word you might often hear in a restaurant? {instructions}
Responses: {inputs[1]}

Survey: In a word, how would you stop someone from stealing your car? {instructions}
Responses: {inputs[2]}

Survey: {surveyPrompt} {instructions}
Responses:";

        var result = await CompletionService.CompletePrompt(prompt, new ICompletionService.CompletionParameters
            {
                Temperature = Config.SurveyScramble.GenTemp,
                MaxTokens = 32,
                TopP = 1,
                FrequencyPenalty = 0.2,
                StopSequences = new[] { "\n" }
            },
            completion =>
            {
                LogVerbose($"Received: {completion.Text.Trim()}");
                var cleanText = FilterResults(completion.Text.Trim(), maxLength);
                if (cleanText.Count > 0) return true;

                LogDebug($"Received unusable ProvideAnswer response: \"{completion.Text.Trim()}\"");
                return false;
            },
            maxTries: Config.SurveyScramble.MaxRetries,
            defaultResponse: "");

        return FilterResults(result.Text.Trim(), maxLength, true);
    }
    
    protected async Task<int> ProvideBest(string surveyPrompt, List<string> opts)
    {
        var options = "";

        for (var i = 0; i < opts.Count; i++)
            options += $"{i + 1}. {opts[i]}\n";

        string prompt = $@"I was taking a survey, and was asked ""{surveyPrompt}"" My options were:

{options}
I think the most popular of those is number: ";

        int IntParseExt(string input)
        {
            if (input.Length < 1) throw new FormatException();

            // Check if response is one of the entries instead of a number
            var idx = opts.FindIndex(a => a == input);
            if (idx != -1) return idx + 1;

            // Assume the response is int-parsable if it starts with a digit character
            if (char.IsDigit(input[0])) return int.Parse(input);

            // GPT likes to respond in English sometimes, so this (manually) tries to check for that
            return input.ToUpper() switch
            {
                "ONE" => 1,
                "TWO" => 2, // Only ever two options
                _ => throw new FormatException() // Response was something unhandled here
            };
        }

        var result = await CompletionService.CompletePrompt(prompt, new ICompletionService.CompletionParameters
            {
                Temperature = Config.SurveyScramble.VoteTemp,
                MaxTokens = 1,
                TopP = 1,
                StopSequences = new[] { "\n" }
            }, completion =>
            {
                try
                {
                    var answer = IntParseExt(completion.Text.Trim());
                    if (0 < answer && answer <= opts.Count) return true;
                }
                catch (FormatException)
                {
                    // pass
                }

                LogDebug($"Received unusable ProvideBest response: {completion.Text.Trim()}");
                return false;
            },
            maxTries: Config.SurveyScramble.MaxRetries,
            defaultResponse: "");

        if (result.Text != "")
            return IntParseExt(result.Text.Trim()) - 1;

        LogDebug("Received only unusable ProvideBest responses. Choice will be chosen randomly");
        return new Random().Next(opts.Count);
    }
}