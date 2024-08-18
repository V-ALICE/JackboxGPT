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

            if (self.ShowError)
            {
                // Fibbage 1 and 2 don't seem to have error messages, just a flag
                // Maybe because this is the only type of error that is reported back?
                LogInfo("The submitted lie was too close to the truth. Generating a new lie...");
                RetryCount += 1;
                LieLock = TruthLock = false;
            }

            if (self.State == RoomState.Lobby_PickBloop && !self.HasBloop)
                ChooseRandomBloop(self);

            if (JackboxClient.GameState.Room.State == RoomState.Gameplay_CategorySelection && self.IsChoosing)
                ChooseRandomCategory();

            if (JackboxClient.GameState.Room.State == RoomState.Gameplay_EnterLie && !LieLock)
                SubmitLie();

            if (JackboxClient.GameState.Room.State == RoomState.Gameplay_ChooseLie && !TruthLock)
                SubmitTruth(self);
        }

        private void OnRoomUpdate(object sender, Revision<Fibbage2Room> revision)
        {
            var room = revision.New;
            if (revision.Old.State != revision.New.State)
                LogDebug($"New room state: {room.State}", true);

            if (room.State == RoomState.Gameplay_CategorySelection || room.State == RoomState.Gameplay_Round)
            {
                RetryCount = 0;
                LieLock = TruthLock = false;
            }
        }
        
        #region Game Actions
        private async void SubmitLie()
        {
            var lie = await FormLie(JackboxClient.GameState.Room.Question);
            JackboxClient.SubmitLie(lie, RetryCount > MAX_SUBMISSION_RETRIES);
        }

        private async void SubmitTruth(Fibbage2Player self)
        {
            var truth = await FormTruth(JackboxClient.GameState.Room.Question, self.LieChoices);
            JackboxClient.ChooseTruth(self.LieChoices[truth].Text);
        }

        private async void ChooseRandomCategory()
        {
            var room = JackboxClient.GameState.Room;

            LogInfo("Time to choose a category.", prefix: "\n");
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
        protected override string GetDefaultLie()
        {
            var choices = JackboxClient.GameState.Self.SuggestionChoices;
            if (choices.Count == 0)
            {
                LogDebug("No suggestions were available when trying to get a default answer. Submitting base default answer");
                return base.GetDefaultLie();
            }

            return choices[choices.RandomIndex()];
        }
        #endregion
    }
}
