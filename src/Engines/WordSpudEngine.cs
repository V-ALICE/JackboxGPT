using System;
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
        
        public WordSpudEngine(ICompletionService completionService, ILogger logger, WordSpudClient client, ManagedConfigFile configFile, int instance)
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
            if (revision.New.State == RoomState.GameplayEnter &&
                (JackboxClient.GameState.Room.Spud != null && JackboxClient.GameState.Room.Spud == ""))
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
            LogVerbose("Voting.");
            
            await Task.Delay(Config.WordSpud.VoteDelayMs);
            JackboxClient.Vote(1);
        }

        private async Task<string> ProvideSpud(string currentWord)
        {
            var prompt = $@"The game Word Spud is played by continuing a word or phrase with a funny related word or phrase. For example:

- jelly|fish
- deal| with it
- fish|sticks
- beat| saber
- tailor| made
- real| life
- how| do you do
- {currentWord}|";

            LogVerbose($"GPT-3 Prompt: {prompt}");

            var result = await CompletionService.CompletePrompt(prompt, new CompletionParameters
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
                    var text = completion.Text.Trim();
                    if (string.Equals(text, currentWord, StringComparison.CurrentCultureIgnoreCase)) return false;
                    text = new string(text.Where(char.IsLetter).ToArray());

                    if (text != "" && text.Length <= 32) return true;

                    LogDebug($"Received unusable ProvideSpud response: {completion.Text.Trim()}");
                    return false;
                },
                maxTries: Config.WordSpud.MaxRetries,
                defaultResponse: "no response");

            return new string(result.Text.TrimEnd().Where(char.IsLetter).ToArray());
        }
    }
}