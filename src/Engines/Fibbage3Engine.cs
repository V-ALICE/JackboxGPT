using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JackboxGPT.Extensions;
using JackboxGPT.Games.Common.Models;
using JackboxGPT.Games.Fibbage3;
using JackboxGPT.Games.Fibbage3.Models;
using JackboxGPT.Services;
using Serilog;

namespace JackboxGPT.Engines
{
    public class Fibbage3Engine : BaseFibbageEngine<Fibbage3Client>
    {
        protected override string Tag => "fibbage3";

        // Only used for a very specific edge case in GetDefaultDoubleLie
        private const string IGNORE_MARKER = "_INPUT_UNUSED";

        // Fibbage 3 sends suggestions in a follow up which messes up the usual logic
        private List<string> _suggestionsRef;

        public Fibbage3Engine(ICompletionService completionService, ILogger logger, Fibbage3Client client, ManagedConfigFile configFile, int instance, uint coinFlip)
            : base(completionService, logger, client, configFile, instance, coinFlip)
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
                    if (self.Error.Contains("too close to the truth"))
                    {
                        LogInfo("The submitted lie was too close to the truth. Generating a new lie...");
                        RetryCount += 1;
                        if (RetryCount > Config.Fibbage.SubmissionRetries)
                        {
                            // Fibbage 3 gives everyone the same suggestions by default, this requests unique ones so that the AI overlaps less
                            JackboxClient.RequestSuggestions();
                            LieLock = TruthLock = false;
                            return;
                        }
                    }
                    else
                    {
                        LogWarning($"Received submission error from game: \"{self.Error}\"");
                    }
                }
                else
                {
                    RetryCount = 0;
                }

                LieLock = TruthLock = false;
            }

            if (JackboxClient.GameState.Room.State == RoomState.CategorySelection && self.IsChoosing)
                ChooseRandomCategory();

            if (JackboxClient.GameState.Room.State == RoomState.EnterText && !LieLock)
            {
                // Wait for suggestions if needed, since they need to be requested
                _suggestionsRef = self.SuggestionChoices;
                if (RetryCount > Config.Fibbage.SubmissionRetries && _suggestionsRef.Count == 0)
                    return;

                if (self.DoubleInput)
                    SubmitDoubleLie(self);
                else
                    SubmitLie(self);
            }

            if (JackboxClient.GameState.Room.State == RoomState.ChooseLie && !TruthLock)
                SubmitTruth(self);
        }

        private void OnRoomUpdate(object sender, Revision<Fibbage3Room> revision)
        {
            var room = revision.New;
            if (revision.Old.State != revision.New.State)
                LogDebug($"New room state: {room.State}", true);
        }

        private async void SubmitLie(Fibbage3Player self)
        {
            var lie = await FormLie(self.Question, self.MaxLength);
            JackboxClient.SubmitLie(lie);
        }
        
        private async void SubmitDoubleLie(Fibbage3Player self)
        {
            var lieParts = await FormDoubleLie(self.Question, self.AnswerDelim, self.MaxLength);
            if (lieParts.Item2 == IGNORE_MARKER)
            {
                JackboxClient.SubmitLie(lieParts.Item1);
            }
            else
            {
                var lie = string.Join(self.AnswerDelim, lieParts.Item1, lieParts.Item2);
                JackboxClient.SubmitLie(lie);
            }
        }

        private async void SubmitTruth(Fibbage3Player self)
        {
            var truth = await FormTruth(JackboxClient.GameState.Room.Question, self.LieChoices);
            JackboxClient.ChooseTruth(truth, self.LieChoices[truth].Text);
        }

        private async void ChooseRandomCategory()
        {
            var room = JackboxClient.GameState.Room;

            LogInfo("Time to choose a category.", prefix: "\n");
            await Task.Delay(Config.Fibbage.CategoryChoiceDelayMs);

            var choices = room.CategoryChoices;
            var category = choices.RandomIndex();
            LogInfo($"Choosing category \"{choices[category].Trim()}\".");

            JackboxClient.ChooseCategory(category);
        }

        protected override string GetDefaultLie()
        {
            var choices = _suggestionsRef.Count > 0 ? _suggestionsRef : JackboxClient.GameState.Room.SuggestionBackupChoices;
            if (choices.Count == 0)
            {
                LogDebug("No suggestions were available when trying to get a default answer. Submitting base default answer");
                return base.GetDefaultLie();
            }

            return choices[choices.RandomIndex()];
        }

        protected override Tuple<string, string> GetDefaultDoubleLie()
        {
            var choices = _suggestionsRef.Count > 0 ? _suggestionsRef : JackboxClient.GameState.Room.SuggestionBackupChoices;
            var delim = JackboxClient.GameState.Self.AnswerDelim;
            if (choices.Count == 0)
            {
                LogDebug("No suggestions were available when trying to get a default answer. Submitting base default answer");
                return base.GetDefaultDoubleLie();
            }

            var choice = choices[choices.RandomIndex()];
            var parts = choice.Split(delim);
            if (parts.Length != 2)
            {
                LogDebug($"Encounted indeterminate sectioning when trying to split \"{choice}\" by \"{delim}\"");
                return new Tuple<string, string>(choice, IGNORE_MARKER);
            }

            return new Tuple<string, string>(parts[0], parts[1]);
        }
    }
}
