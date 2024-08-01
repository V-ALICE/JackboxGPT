using System.Linq;
using System.Threading.Tasks;
using JackboxGPT3.Extensions;
using JackboxGPT3.Games.Common.Models;
using JackboxGPT3.Games.Fibbage2;
using JackboxGPT3.Games.Fibbage2.Models;
using JackboxGPT3.Services;
using Serilog;

namespace JackboxGPT3.Engines
{
    public class Fibbage2Engine : BaseFibbageEngine<Fibbage2Client>
    {
        protected override string Tag => "fibbage2";
        
        public Fibbage2Engine(ICompletionService completionService, ILogger logger, Fibbage2Client client, int instance)
            : base(completionService, logger, client, instance)
        {
            JackboxClient.OnRoomUpdate += OnRoomUpdate;
            JackboxClient.OnSelfUpdate += OnSelfUpdate;
            JackboxClient.Connect();
        }

        private void OnSelfUpdate(object sender, Revision<Fibbage2Player> revision)
        {
            var self = revision.New;

            if (JackboxClient.GameState.Room.State == RoomState.Gameplay_CategorySelection
                || JackboxClient.GameState.Room.State == RoomState.Gameplay_Round
                || self.Error != null)
            {
                if (self.ShowError)
                {
                    LogWarning($"Received submission error from game: {self.Error}");
                    if (self.Error.Contains("too close to the truth")) // TODO: find out what the fibbage 1/2 wording is
                        FailureCounter += 1;
                }
                else
                {
                    FailureCounter = 0;
                }

                LieLock = TruthLock = false;
            }

            if (self.State == RoomState.Lobby_PickBloop && !self.HasBloop)
                ChooseRandomBloop(self);

            if (JackboxClient.GameState.Room.State == RoomState.Gameplay_CategorySelection && self.IsChoosing)
                ChooseRandomCategory();

            if (JackboxClient.GameState.Room.State == RoomState.Gameplay_EnterLie && !LieLock)
                SubmitLie(self);

            if (JackboxClient.GameState.Room.State == RoomState.Gameplay_ChooseLie && !TruthLock)
                SubmitTruth(self);
        }

        private void OnRoomUpdate(object sender, Revision<Fibbage2Room> revision)
        {
            var room = revision.New;
            LogDebug($"New room state: {room.State}", true);
        }
        
        #region Game Actions
        private async void SubmitLie(Fibbage2Player self)
        {
            LieLock = true;

            if (FailureCounter > MaxFailures)
            {
                LogInfo("Submitting default answer because there were too many submission errors.");
                JackboxClient.SubmitLie("NO ANSWER", false); // TODO: use suggestion
                FailureCounter = 0;
                return;
            }

            var prompt = CleanPromptForEntry(JackboxClient.GameState.Room.Question);
            LogInfo($"Asking GPT-3 for lie in response to \"{prompt}\".", true);

            var lie = await ProvideLie(prompt, 45);
            LogInfo($"Submitting lie \"{lie}\"");

            JackboxClient.SubmitLie(lie, false);
        }

        private async void SubmitTruth(Fibbage2Player self)
        {
            TruthLock = true;

            var prompt = CleanPromptForEntry(JackboxClient.GameState.Room.Question);
            LogInfo("Asking GPT-3 to choose truth.", true);

            var choices = self.LieChoices;
            var choicesStr = choices.Aggregate("", (current, a) => current + (a.Text + ", "))[..^2];
            LogInfo($"Asking GPT-3 to choose truth out of these options [{choicesStr}].", true);
            var truth = await ProvideTruth(prompt, choices);
            LogInfo($"Submitting truth {truth} (\"{choices[truth].Text}\")");

            JackboxClient.ChooseTruth(choices[truth].Text);
        }

        private async void ChooseRandomCategory()
        {
            var room = JackboxClient.GameState.Room;
            
            LogInfo("Time to choose a category.", prefix: "\n\n");
            await Task.Delay(3000);

            var choices = room.CategoryChoices;
            var category = choices.RandomIndex();
            LogInfo($"Choosing category \"{choices[category].Trim()}\".");

            JackboxClient.ChooseCategory(category);
        }

        private void ChooseRandomBloop(Fibbage2Player self)
        {
            var choices = self.BloopChoices;
            var category = choices.RandomIndex();
            LogDebug($"Choosing bloop \"{choices[category].Name}\".");

            JackboxClient.ChooseBloop(choices[category].Id);
        }
        #endregion

        #region Prompt Cleanup
        private static string CleanPromptForEntry(string prompt)
        {
            return prompt.StripHtml();
        }
        #endregion
    }
}
