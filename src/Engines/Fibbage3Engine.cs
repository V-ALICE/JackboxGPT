using System;
using System.Linq;
using System.Threading.Tasks;
using JackboxGPT3.Extensions;
using JackboxGPT3.Games.Common.Models;
using JackboxGPT3.Games.Fibbage3;
using JackboxGPT3.Games.Fibbage3.Models;
using JackboxGPT3.Services;
using Serilog;

namespace JackboxGPT3.Engines
{
    public class Fibbage3Engine : BaseFibbageEngine<Fibbage3Client>
    {
        protected override string Tag => "fibbage3";
        
        public Fibbage3Engine(ICompletionService completionService, ILogger logger, Fibbage3Client client, int instance)
            : base(completionService, logger, client, instance)
        {
            JackboxClient.OnRoomUpdate += OnRoomUpdate;
            JackboxClient.OnSelfUpdate += OnSelfUpdate;
            JackboxClient.Connect();
        }

        private void OnSelfUpdate(object sender, Revision<Fibbage3Player> revision)
        {
            var self = revision.New;
            
            if (JackboxClient.GameState.Room.State == RoomState.EndShortie || self.Error != null)
            {
                if (self.Error != null)
                {
                    LogWarning($"Received submission error from game: {self.Error}");
                    if (self.Error.Contains("too close to the truth"))
                        FailureCounter += 1;
                }

                LieLock = TruthLock = false;
            }

            if (JackboxClient.GameState.Room.State == RoomState.CategorySelection && self.IsChoosing)
                ChooseRandomCategory();

            if (JackboxClient.GameState.Room.State == RoomState.EnterText && !LieLock)
                if (self.DoubleInput)
                    SubmitDoubleLie(self);
                else
                    SubmitLie(self);

            if (JackboxClient.GameState.Room.State == RoomState.ChooseLie && !TruthLock)
                SubmitTruth(self);
        }

        private void OnRoomUpdate(object sender, Revision<Fibbage3Room> revision)
        {
            var room = revision.New;
            LogDebug($"New room state: {room.State}", true);
        }
        
        #region Game Actions
        private async void SubmitLie(Fibbage3Player self)
        {
            LieLock = true;

            if (FailureCounter > MaxFailures)
            {
                LogInfo("Submitting default answer because there were too many submission errors.");
                JackboxClient.SubmitLie("NO ANSWER");
                FailureCounter = 0;
                return;
            }

            var prompt = CleanPromptForEntry(self.Question);
            LogInfo($"Asking GPT-3 for lie in response to \"{prompt}\".", true);

            var lie = await ProvideLie(prompt, 45);
            LogInfo($"Submitting lie \"{lie}\"");

            JackboxClient.SubmitLie(lie);
        }
        
        private async void SubmitDoubleLie(Fibbage3Player self)
        {
            LieLock = true;

            if (FailureCounter > MaxFailures)
            {
                LogInfo("Submitting default answer because there were too many submission errors.");
                JackboxClient.SubmitLie(string.Join(self.AnswerDelim, "NO", "ANSWER"));
                FailureCounter = 0;
                return;
            }

            var prompt = CleanPromptForEntry(self.Question);
            LogInfo($"Asking GPT-3 for double lie in response to \"{prompt}\".", true);

            var lieParts = await ProvideDoubleLie(prompt, self.AnswerDelim, self.MaxLength);
            var lie = string.Join(self.AnswerDelim, lieParts.Item1, lieParts.Item2);
            LogInfo($"Submitting double lie \"{lie}\"");

            JackboxClient.SubmitLie(lie);
        }

        private async void SubmitTruth(Fibbage3Player self)
        {
            TruthLock = true;

            var prompt = CleanPromptForEntry(JackboxClient.GameState.Room.Question);
            LogInfo("Asking GPT-3 to choose truth.", true);

            var choices = self.LieChoices;
            var choicesStr = choices.Aggregate("", (current, a) => current + (a.Text + ", "))[..^2];
            LogInfo($"Asking GPT-3 to choose truth out of these options [{choicesStr}].", true);
            var truth = await ProvideTruth(prompt, choices);
            LogInfo($"Submitting truth {truth} (\"{choices[truth].Text}\")");

            JackboxClient.ChooseTruth(truth, choices[truth].Text);
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
        #endregion

        #region Prompt Cleanup
        private static string CleanPromptForEntry(string prompt)
        {
            return prompt.StripHtml();
        }
        #endregion
    }
}
