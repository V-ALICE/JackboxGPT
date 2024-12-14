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
using static JackboxGPT.Services.ManagedConfigFile.SurveyScrambleBlock;

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

    // Contains extra information specifically for the Dash game mode
    private Dictionary<string, HorseRacePlayerInfo> _raceInfo;
    private HorseRacePlayerInfo MyHorseRaceStatus => _raceInfo[JackboxClient.GetPlayerId().ToString()];

    // Rank of previous dare entry on the board. Used for rank guessing range in final round
    private int _previousDareResult;

    // Where on the list a response should be from
    private enum PositionType
    {
        High,
        Low,
        All
    }

    public SurveyScrambleEngine(ICompletionService completionService, ILogger logger, SurveyScrambleClient client, ManagedConfigFile configFile, int instance)
        : base(completionService, logger, client, configFile, instance)
    {
        JackboxClient.OnSelfUpdate += OnSelfUpdate;
        JackboxClient.OnTextDescriptionsReceived += OnTextDescriptionsReceived;
        JackboxClient.OnRoundInfoReceived += OnRoundInfoReceived;
        JackboxClient.OnHorseRaceInfoReceived += OnHorseRaceInfoReceived;
        JackboxClient.Connect();
    }

    private void OnSelfUpdate(object sender, Revision<SurveyScramblePlayer> revision)
    {
        var self = revision.New;
        var continueRequest = self.Kind == Kind.Choices && self.ResponseKey.StartsWith("voteResponse") && self.CategoryChoices.Contains("KEEP GOING");
        var dareRequest = self.Kind == Kind.Choices && self.ResponseKey.StartsWith("objectGuess") && self.CategoryChoices.Contains("MORE");

        if (self.Kind == Kind.Choices && self.ResponseKey.StartsWith("voteResponse") && !continueRequest && !dareRequest)
        {
            // New game, reset everything
            LogInfo("Resetting everything for new game...", true, prefix: "\n");
            _dead = _promptShown = false;
            _previouslyGuessed.Clear();
            _queuedGuesses[PositionType.High].Clear();
            _queuedGuesses[PositionType.Low].Clear();
            _queuedGuesses[PositionType.All].Clear();
            _promptInfo = new SurveyScrambleRoundInfo();

            // Reply
            ChooseRandomTopic(self);
            return;
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
                HandleSpeedMode(self);
                break;
            case Kind.TicTacToe:
                HandleTicTacToeMode(self);
                break;
            case Kind.HorseRace:
                HandleHorseRaceMode(self);
                break;
            case Kind.Dare:
                HandleDareMode(self);
                break;
            case Kind.DareText:
                HandleDareModeGuess(self);
                break;
            case Kind.Choices:
                if (continueRequest)
                    VoteContinue(self);
                else if (dareRequest)
                    DareChooseDirection(self);
                else if (self.ResponseKey.StartsWith("objectGuess"))
                    HighLowChooseMostPopular(self);
                break;
            case Kind.TeamChoice:
                LockTeam(self);
                break;
        }
    }

    private void OnTextDescriptionsReceived(object sender, SurveyScrambleTextDescriptions info)
    {
        foreach (var entry in info.LatestDescriptions)
        {
            if (entry.Category != "TD_HIGHLOW_REVEAL") continue;

            var data = entry.Text.Split(' ');
            try
            {
                _previousDareResult = int.Parse(data[^1]);
            }
            catch (FormatException)
            {
                LogWarning("TD_HIGHLOW_REVEAL did not end in a numeric value unexpectedly");
            }
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

    private void OnHorseRaceInfoReceived(object sender, Dictionary<string, HorseRacePlayerInfo> info)
    {
        _raceInfo = info;
    }

    private async void LockTeam(SurveyScramblePlayer self)
    {
        await Task.Delay(Config.SurveyScramble.TeamSelectionDelayMs);
        switch (Config.SurveyScramble.TeamSelectionMethod)
        {
            case TeamSelectionMethodType.Default: // Do nothing
                break;
            case TeamSelectionMethodType.Split:
                JackboxClient.ChooseTeam(self.CurrentTeamName, Instance % 2 == 0);
                break;
            case TeamSelectionMethodType.Left:
                JackboxClient.ChooseTeam(self.CurrentTeamName, true);
                break;
            case TeamSelectionMethodType.Right:
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

    private async void HandleHorseRaceMode(SurveyScramblePlayer self)
    {
        if (_promptInfo.LongPrompt.Length == 0 || self.Options.Count == 0 || _raceInfo.Count == 0) return;

        // Determine if a sabotage can/should be used
        if (MyHorseRaceStatus.CanSabotage && Config.SurveyScramble.DashSabotageChance > _rand.NextDouble())
        {
            var otherPlayers = _raceInfo.Where(entry => entry.Key != JackboxClient.GetPlayerId().ToString()).ToList();
            var cap = otherPlayers.Count;
            if (Config.SurveyScramble.DashSabotageMethod ==  DashSabotageMethodType.Leaders)
            {
                otherPlayers = otherPlayers.OrderByDescending(entry => entry.Value.Score).ToList();
                cap = Math.Max(otherPlayers.Count / 2, 1);
            }
            var sabotageId = otherPlayers[_rand.Next(cap)].Key;
            LogDebug($"Using sabotage on player with ID {sabotageId}");
            JackboxClient.SabotagePlayer(int.Parse(sabotageId));
        }

        // Determine if a double-down should be used
        var doubleDown = false;
        if (Config.SurveyScramble.DashDoubledownChance > _rand.NextDouble())
        {
            var myScore = MyHorseRaceStatus.Score;
            switch (Config.SurveyScramble.DashDoubledownMethod)
            {
                case DashDoubledownMethodType.Winning:
                    doubleDown = true;
                    foreach (var entry in _raceInfo)
                    {
                        if (entry.Value.Score > myScore) doubleDown = false;
                        break;
                    }
                    break;
                case DashDoubledownMethodType.Losing:
                    doubleDown = true;
                    foreach (var entry in _raceInfo)
                    {
                        if (entry.Value.Score < myScore) doubleDown = false;
                        break;
                    }
                    break;
                case DashDoubledownMethodType.Close:
                    doubleDown = Math.Abs(self.ScoreToWin - myScore - 2) < 1e-5;
                    break;
                case DashDoubledownMethodType.Random:
                    doubleDown = true;
                    break;
            }
        }

        // Determine round type
        var position = self.Goal == "Low" ? PositionType.Low : PositionType.High;

        // Submit guess
        var choice = await ProvideBest(_promptInfo.LongPrompt, self.Options, position);
        LogInfo($"{(doubleDown ? "Doubling down on" : "Choosing")} {(position == PositionType.High ? "most" : "least")} popular option \"{self.Options[choice]}\"");
        JackboxClient.ChooseOption(choice, doubleDown);
    }

    private void HandleDareMode(SurveyScramblePlayer self)
    {
        if (_promptInfo.LongPrompt.Length == 0) return;

        var dir = "";
        switch (Config.SurveyScramble.DareSelectionMethod)
        {
            case DareSelectionMethodType.Random:
                dir = _rand.Next(2) == 0 ? "Higher" : "Lower";
                break;
            case DareSelectionMethodType.Hardest:
                dir = self.HighDifficulty > self.LowDifficulty ? "Higher" : "Lower";
                break;
            case DareSelectionMethodType.Easiest:
                dir = self.HighDifficulty < self.LowDifficulty ? "Higher" : "Lower";
                break;
        }
        JackboxClient.ChooseDirection(dir); // TODO: what is points supposed to be?
    }

    private async void HandleDareModeGuess(SurveyScramblePlayer self)
    {
        if (_promptInfo.LongPrompt.Length == 0) return;

        // Determine round type
        var position = self.Goal == "Low" ? PositionType.Low : PositionType.High;

        // Guessing position for previously guessed reponse
        if (self.SuccessfulGuess?.Length > 0 && self.ShowCallShot)
        {
            int rank;
            if (position == PositionType.High)
                rank = await ProvideRank(_promptInfo.LongPrompt, self.SuccessfulGuess, 1, _previousDareResult - 1);
            else
                rank = await ProvideRank(_promptInfo.LongPrompt, self.SuccessfulGuess, _previousDareResult + 1, _promptInfo.SurveyLength);

            LogInfo($"Guessing that {self.SuccessfulGuess} is ranked {rank} on the list");
            JackboxClient.GuessRank(rank);
            return;
        }

        // Generate more responses if needed
        if (_queuedGuesses[position].Count == 0)
        {
            LogDebug($"Requesting more responses for goal {self.Goal}...");
            var responses = await ProvideAnswers(_promptInfo.LongPrompt, self.Prompt, position);
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

    private void ChooseRandomTopic(SurveyScramblePlayer self)
    {
        var choices = self.CategoryChoices;
        var category = _rand.Next(choices.Count);
        LogDebug($"Choosing category \"{choices[category]}\"");

        JackboxClient.ChooseCategory(category);
    }

    private void VoteContinue(SurveyScramblePlayer self)
    {
        switch (Config.SurveyScramble.ContinueSelectionMethod)
        {
            case ContinueSelectionMethodType.Split:
                JackboxClient.ChooseCategory(Instance % 2);
                break;
            case ContinueSelectionMethodType.Continue:
                JackboxClient.ChooseCategory(self.CategoryChoices.IndexOf("KEEP GOING"));
                break;
            case ContinueSelectionMethodType.End:
                JackboxClient.ChooseCategory(self.CategoryChoices.IndexOf("FINAL ROUND"));
                break;
        }
    }

    private async void HighLowChooseMostPopular(SurveyScramblePlayer self)
    {
        if (_promptInfo.LongPrompt.Length == 0) return;

        var choices = self.CategoryChoices;
        var option = await ProvideBest(_promptInfo.LongPrompt, choices, PositionType.High);
        LogInfo($"Choosing most popular option \"{choices[option]}\"");

        JackboxClient.GuessCategory(option);
    }

    private async void DareChooseDirection(SurveyScramblePlayer self)
    {
        if (_promptInfo.ShortPrompt.Length == 0) return;

        var choices = self.CategoryChoices;
        var direction = await ProvideDirection(_promptInfo.LongPrompt, self.Prompt, choices);
        LogInfo($"AI guessed {choices[direction]} for \"{self.Prompt}\"");

        JackboxClient.GuessCategory(direction);
    }

    private List<string> FilterResults(string input, int maxLen, bool logChanges = false)
    {
        // Split up result
        var inputs = input.Split(";");
        if (inputs.Length == 1)
        {
            inputs = input.Split(","); // AI isn't supposed to do this but does sometimes anyway
            if (inputs.Length == 1) inputs = input.Split(" "); // Last-ditch effort
        }

        var cleaned = new List<string>();
        foreach (var entry in inputs)
        {
            // Remove everything besides letters and numbers
            var clipped = new string(entry.Where(char.IsLetterOrDigit).ToArray());
            if (clipped.Length == 0 || clipped.Length > maxLen || clipped.Length < entry.Length / 2) continue;
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

    private async Task<int> ProvideBest(string surveyPrompt, List<string> opts, PositionType type)
    {
        var options = "";

        for (var i = 0; i < opts.Count; i++)
            options += $"{i + 1}. {opts[i]}\n";

        string prompt = $@"I was taking a survey, and was asked ""{surveyPrompt}"" My options were:

{options}
I think the {(type == PositionType.Low ? "least" : "most")} popular of those is number: ";

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
                "TWO" => 2,
                "THREE" => 3,
                "FOUR" => 4,
                "FIVE" => 5,
                "SIX" => 6,
                "SEVEN" => 7,
                "EIGHT" => 8,
                "NINE" => 9, // TODO: can horse race have more than nine options?
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
        return _rand.Next(opts.Count);
    }

    private async Task<int> ProvideRank(string surveyPrompt, string choice, int min, int max)
    {
        string prompt = $@"I was taking a survey, and was asked how popular I thought ""{choice}"" would be on a list of responses to ""{surveyPrompt}""
I think ""{choice}"" would be ranked ({min}-{max}): ";
        LogVerbose(prompt);

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
                var answer = int.Parse(completion.Text.Trim());
                if (min <= answer && answer <= max) return true;
            }
            catch (FormatException)
            {
                // pass
            }

            LogDebug($"Received unusable ProvideRank({min}-{max}) response: {completion.Text.Trim()}");
            return false;
        },
        maxTries: Config.SurveyScramble.MaxRetries,
        defaultResponse: "");

        if (result.Text != "")
            return int.Parse(result.Text.Trim());

        LogDebug("Received only unusable ProvideBest responses. Choice will be chosen randomly");
        return _rand.Next(min, max+1);
    }

    private async Task<int> ProvideDirection(string promptEntry, string currentPrompt, IList<string> allowed)
    {
        // GPT loves to respond with "Neither"
        string prompt = $@"I was taking a survey where the prompt was ""{promptEntry}"" and was asked ""{currentPrompt}"" One of the answers is more popular.
I think the answer is: ";
        LogVerbose(prompt);

        var result = await CompletionService.CompletePrompt(prompt, new ICompletionService.CompletionParameters
        {
            Temperature = Config.SurveyScramble.VoteTemp,
            MaxTokens = 12,
            TopP = 1,
            StopSequences = new[] { "\n" }
        }, completion =>
        {   
            LogVerbose(completion.Text.Trim());
            var matches = allowed.Count(item => completion.Text.ToUpper().Trim().Contains(item.ToUpper()));
            if (matches == 1) return true;

            LogDebug($"Received unusable ProvideDirection response: {completion.Text.Trim()}");
            return false;
        },
        maxTries: Config.SurveyScramble.MaxRetries,
        defaultResponse: "");

        if (result.Text != "")
        {
            for (int i = 0; i < allowed.Count; i++)
            {
                if (result.Text.ToUpper().Trim().Contains(allowed[i].ToUpper())) return i;
            }
        }

        LogDebug("Received only unusable ProvideBest responses. Choice will be chosen randomly");
        return new Random().Next(allowed.Count);
    }
}