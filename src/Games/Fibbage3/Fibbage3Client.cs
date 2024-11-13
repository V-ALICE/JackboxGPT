using JackboxGPT.Games.Common;
using JackboxGPT.Games.Common.Models;
using JackboxGPT.Games.Fibbage3.Models;
using JackboxGPT.Services;
using Serilog;

namespace JackboxGPT.Games.Fibbage3
{
    public class Fibbage3Client : BcSerializedClient<Fibbage3Room, Fibbage3Player>
    {
        public Fibbage3Client(IConfigurationProvider configuration, ILogger logger, int instance) : base(configuration, logger, instance) { }

        public void RequestSuggestions()
        {
            var req = new SuggestionsRequest();
            ClientSend(req);
        }

        public void ChooseCategory(int index)
        {
            var req = new ChooseRequest<int>(index);
            ClientSend(req);
        }
        
        public void ChooseTruth(int index, string text)
        {
            var req = new ChooseRequest<TruthChoice>(new TruthChoice
            {
                Order = index,
                Text = text
            });

            ClientSend(req);
        }

        public void SubmitLie(string lie)
        {
            var req = new SendEntryRequest(lie);
            ClientSend(req);
        }
    }
}
