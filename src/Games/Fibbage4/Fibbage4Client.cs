using JackboxGPT.Games.Common;
using JackboxGPT.Games.Fibbage4.Models;
using JackboxGPT.Services;
using Serilog;

namespace JackboxGPT.Games.Fibbage4
{
    public class Fibbage4Client : PlayerSerializedClient<Fibbage4Room, Fibbage4Player>
    {
        private const string KEY_CHOOSE = "choose";
        private const string KEY_ENTRYBOX1_SUBMIT = "entertext:entry1";
        private const string KEY_ENTRYBOX2_SUBMIT = "entertext:entry2";
        private const string KEY_ENTRYBOX_ACTION = "entertext:actions";
        
        public Fibbage4Client(IConfigurationProvider configuration, ILogger logger, int instance) : base(configuration, logger, instance) { }

        public void ChooseCategory(int index)
        {
            var req = new SelectRequest<int>(index);
            ClientUpdate(req, KEY_CHOOSE);
        }
        
        public void ChooseTruth(int index)
        {
            var req = new SelectRequest<int>(index);
            ClientUpdate(req, KEY_CHOOSE);
        }

        public void SubmitLie(string lie, bool usedSuggestion = false)
        {
            ClientUpdate(lie, KEY_ENTRYBOX1_SUBMIT);
            if (usedSuggestion)
            {
                var req = new SuggestionsRequest();
                ClientUpdate(req, KEY_ENTRYBOX_ACTION);
            }
            else
            {
                var req = new AnswerRequest();
                ClientUpdate(req, KEY_ENTRYBOX_ACTION);
            }
        }

        public void SubmitDoubleLie(string lie1, string lie2, bool usedSuggestion = false)
        {
            ClientUpdate(lie1, KEY_ENTRYBOX1_SUBMIT);
            ClientUpdate(lie2, KEY_ENTRYBOX2_SUBMIT);
            if (usedSuggestion)
            {
                var req = new SuggestionsRequest();
                ClientUpdate(req, KEY_ENTRYBOX_ACTION);
            }
            else
            {
                var req = new AnswerRequest();
                ClientUpdate(req, KEY_ENTRYBOX_ACTION);
            }
        }

    }
}
