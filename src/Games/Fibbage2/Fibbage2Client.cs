using JackboxGPT3.Games.Common;
using JackboxGPT3.Games.Fibbage2.Models;
using JackboxGPT3.Services;
using Serilog;

namespace JackboxGPT3.Games.Fibbage2
{
    public class Fibbage2Client : BcSerializedClient<Fibbage2Room, Fibbage2Player>
    {
        public Fibbage2Client(IConfigurationProvider configuration, ILogger logger, int instance) : base(configuration, logger, instance) { }

        public void StartGame()
        {
            var req = new StartGameRequest();
            ClientSend(req);
        }

        public void ChooseBloop(string bloop)
        {
            var req = new BloopChoice() { Bloop = bloop };
            ClientSend(req);
        }

        public void ChooseCategory(int index)
        {
            var req = new CategoryChoice { ChosenCategory = index };
            ClientSend(req);
        }
        
        public void ChooseTruth(string text)
        {
            var req = new TruthChoice { Choice = text };
            ClientSend(req);
        }

        public void SubmitLie(string lie, bool usedSuggestion)
        {
            var req = new SendEntryRequest
            {
                LieEntered = lie,
                UsedSuggestion = usedSuggestion
            };
            ClientSend(req);
        }
    }
}
