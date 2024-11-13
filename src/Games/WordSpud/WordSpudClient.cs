using JackboxGPT.Games.Common;
using JackboxGPT.Games.WordSpud.Models;
using JackboxGPT.Services;
using Serilog;

namespace JackboxGPT.Games.WordSpud
{
    public class WordSpudClient : BcSerializedClient<WordSpudRoom, WordSpudPlayer>
    {
        public WordSpudClient(IConfigurationProvider configuration, ILogger logger, int instance) : base(configuration, logger, instance) { }

        public void SubmitSpud(string spud)
        {
            var req = new SubmitSpudRequest(spud);
            ClientSend(req);
        }
        
        public void Vote(int vote)
        {
            var req = new VoteRequest(vote);
            ClientSend(req);
        }
    }
}