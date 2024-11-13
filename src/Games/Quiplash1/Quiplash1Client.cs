using JackboxGPT.Games.Common;
using JackboxGPT.Games.Quiplash1.Models;
using JackboxGPT.Services;
using Serilog;

namespace JackboxGPT.Games.Quiplash1
{
    public class Quiplash1Client : BcSerializedClient<Quiplash1Room, Quiplash1Player>
    {
        public Quiplash1Client(IConfigurationProvider configuration, ILogger logger, int instance) : base(configuration, logger, instance) { }

        public void StartGame()
        {
            var req = new StartGameRequest();
            ClientSend(req);
        }

        public void SubmitQuip(int questionId, string quip)
        {
            var req = new SendEntryRequest { Answer = quip, QuestionId = questionId };
            ClientSend(req);
        }

        public void ChooseFavorite(string dictKeyOrPlayerIdx)
        {
            if (int.TryParse(dictKeyOrPlayerIdx, out var playerIdx))
            {
                var req = new SendVoteRequest<int> { Vote = playerIdx };
                ClientSend(req);
            }
            else
            {
                var req = new SendVoteRequest<string> { Vote = dictKeyOrPlayerIdx };
                ClientSend(req);
            }
        }
    }
}
