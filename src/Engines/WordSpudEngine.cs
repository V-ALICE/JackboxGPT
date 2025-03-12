using System.Linq;
using System.Threading.Tasks;
using JackboxGPT.Games.Common.Models;
using JackboxGPT.Games.WordSpud;
using JackboxGPT.Games.WordSpud.Models;
using JackboxGPT.Services;
using Serilog;
using static JackboxGPT.Services.ICompletionService;

namespace JackboxGPT.Engines
{
    public class WordSpudEngine : BaseJackboxEngine<WordSpudClient>
    {
        protected override string Tag => "wordspud";
        protected override ManagedConfigFile.EnginePreference EnginePref => Config.WordSpud.EnginePreference;

        public WordSpudEngine(ICompletionService completionService, ILogger logger, WordSpudClient client, ManagedConfigFile configFile, int instance, uint coinFlip)
            : base(completionService, logger, client, configFile, instance)
        {
            JackboxClient.OnSelfUpdate += OnSelfUpdate;
            JackboxClient.OnRoomUpdate += OnRoomUpdate;
            JackboxClient.Connect();
        }

        private void OnRoomUpdate(object sender, Revision<WordSpudRoom> revision)
        {
            if (revision.Old.State != revision.New.State &&
                revision.New.State == RoomState.Vote)
                VoteSpud();
        }

        private void OnSelfUpdate(object sender, Revision<WordSpudPlayer> revision)
        {
            if (revision.New.State == RoomState.GameplayEnter
                && JackboxClient.GameState.Room.Spud != null
                && JackboxClient.GameState.Room.Spud == ""
            )
                SubmitSpud();
        }

        private async void SubmitSpud()
        {
            var currentWord = JackboxClient.GameState.Room.CurrentWord.Trim().Split(" ").Last();
            LogInfo($"Getting a spud for \"{currentWord}\".");

            var spud = await ProvideSpud(currentWord);
            LogInfo($"Submitting \"{spud}\".");
            
            JackboxClient.SubmitSpud(spud);
        }
        
        private async void VoteSpud()
        {
            if (JackboxClient.GameState.Self.State == RoomState.GameplayEnter) return;

            await Task.Delay(Config.WordSpud.VoteDelayMs);
            var approve = true;
            if (Config.Model.UseChatEngineForVoting && Config.WordSpud.AllowAiVotes)
            {
                var splits = JackboxClient.GameState.Room.CurrentWord.Split(' ');
                var lastBlock = splits[^1].Length > 0 ? splits[^1] : $"{splits[^2]} ";
                approve = await ProvideApproval($"{lastBlock}{JackboxClient.GameState.Room.Spud}");
                LogDebug($"Voting {(approve ? "positively" : "negatively")}");
            }
            JackboxClient.Vote(approve ? 1 : -1);
        }

        private string CleanResult(string input, string currentWord, bool logChanges = false)
        {
            var clipped = input;

            // Sometimes the AI likes to include pieces of the previous word at the start of its answer
            var biggestOverlap = 0;
            for (var i = 0; i < currentWord.Length; i++)
            {
                var pre = currentWord[i..].ToLower();
                var post = clipped[..pre.Length].ToLower();
                if (pre == post)
                {
                    biggestOverlap = pre.Length;
                    break;
                }
            }
            if (biggestOverlap > currentWord.Length / 2)
            {
                clipped = clipped[biggestOverlap..];
                if (clipped.StartsWith(' ') && !input.StartsWith(' '))
                    clipped.TrimStart();
            }

            clipped = new string(clipped.Where(c => char.IsWhiteSpace(c) || char.IsLetter(c)).ToArray());
            if (logChanges && input.Length != clipped.Length)
                LogDebug($"Edited AI response from \"{input}\" to \"{clipped}\"");
            return clipped;
        }

        private async Task<string> ProvideSpud(string currentWord)
        {
            var prompt = new TextInput
            {
                ChatSystemMessage = "You are a player in a game called Word Spud, in which players attempt to use part of a word or phrase to make a new one. You will be given a word, please finish the word or turn it into a short phrase. Your answer will come after the word given, so do not include the word in your response.",
                ChatStylePrompt = $"Here's the next word: {currentWord}",
                CompletionStylePrompt = $@"The game Word Spud is played by continuing a word or phrase with a funny related word or phrase. For example:

- jelly|fish
- deal| with it
- fish|sticks
- beat| saber
- tailor| made
- real| life
- how| do you do
- {currentWord}|",
            };
            var useChatEngine = UsingChatEngine;
            LogVerbose($"Prompt:\n{(useChatEngine ? prompt.ChatStylePrompt : prompt.CompletionStylePrompt)}");

            var result = await CompletionService.CompletePrompt(prompt, useChatEngine, new CompletionParameters
                {
                    Temperature = Config.WordSpud.GenTemp,
                    MaxTokens = 16,
                    TopP = 1,
                    FrequencyPenalty = 0.3,
                    PresencePenalty = 0.3,
                    StopSequences = new[] { "\n" }
                },
                completion =>
                {
                    var cleanText = CleanResult(completion.Text.Trim(), currentWord);
                    if (cleanText.Length is > 0 and <= 32) return true;

                    LogDebug($"Received unusable ProvideSpud response: {completion.Text.Trim()}");
                    return false;
                },
                maxTries: Config.WordSpud.MaxRetries,
                defaultResponse: "no response");

            return CleanResult(result.Text.Trim(), currentWord, true);
        }

        private async Task<bool> ProvideApproval(string combo)
        {
            const string good = "GOOD";
            const string bad = "BAD";

            var prompt = new TextInput
            {
                ChatSystemMessage = $"You are a player in a game called Word Spud, in which players attempt to use part of a word or phrase to make a new one. You will be given a word or phrase and need to evaluate if it's reasonable or not. Since this is just for fun, you can be generally positive as long as the response makes sense. Please respond with {good} or {bad}",
                ChatStylePrompt = $"How about this one: \"{combo}\"",
                CompletionStylePrompt = ""
            };
            LogVerbose($"Prompt:\n{prompt.ChatStylePrompt}");

            var result = await CompletionService.CompletePrompt(prompt, true, new CompletionParameters
            {
                Temperature = Config.WordSpud.VoteTemp,
                MaxTokens = 12,
                TopP = 1,
                StopSequences = new[] { "\n" }
            }, completion =>
            {
                LogVerbose(completion.Text.Trim());
                if (completion.Text.ToUpper().Trim().Contains(good) ||
                    completion.Text.ToUpper().Trim().Contains(bad)) return true;

                LogDebug($"Received unusable ProvideApproval response: {completion.Text.Trim()}");
                return false;
            },
            maxTries: Config.WordSpud.MaxRetries,
            defaultResponse: "");

            if (result.Text.ToUpper().Trim().Contains(good)) return true;
            if (result.Text.ToUpper().Trim().Contains(bad)) return false;

            LogDebug("Received only unusable ProvideApproval responses. Defaulting to voting positively");
            return true;
        }
    }
}