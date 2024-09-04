using JackboxGPT3.Games.Common;
using JackboxGPT3.Games.Quiplash2.Models;
using JackboxGPT3.Services;
using Serilog;

namespace JackboxGPT3.Games.Quiplash2
{
    public class Quiplash2Client : BcSerializedClient<Quiplash2Room, Quiplash2Player>
    {
        public Quiplash2Client(IConfigurationProvider configuration, ILogger logger, int instance) : base(configuration, logger, instance) { }

        public void RequestSafetyQuip(int questionId)
        {
            var req = new SafetyQuipRequest { QuestionId = questionId };
            ClientSend(req);
        }

        public void SubmitQuip(int questionId, string quip)
        {
            var req = new SendEntryRequest { Answer = quip, QuestionId = questionId };
            ClientSend(req);
        }

        public void ChooseFavorite(int playerIdx)
        {
            var req = new SendVoteRequest<int> { Vote = playerIdx };
            ClientSend(req);
        }

        public void ChooseFavorite(string dictKey)
        {
            var req = new SendVoteRequest<string> { Vote = dictKey };
            ClientSend(req);
        }
    }
}
