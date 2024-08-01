using System;
using JackboxGPT3.Games.Common;
using JackboxGPT3.Games.Common.Models;
using JackboxGPT3.Games.Fibbage4.Models;
using JackboxGPT3.Services;
using Newtonsoft.Json;
using Serilog;

namespace JackboxGPT3.Games.Fibbage4
{
    public class Fibbage4Client : BcSerializedClient<Fibbage4Room, Fibbage4Player>
    {
        protected override string KEY_ROOM => "";
        protected override string KEY_PLAYER_PREFIX => "player:";

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

        // For fibbage 4 there are no room ops, everything is in the player ops
        protected override void HandleOperation(IOperation op)
        {
            if (op.Key == $"{KEY_PLAYER_PREFIX}{_gameState.PlayerId}")
            {
                var self = JsonConvert.DeserializeObject<Fibbage4Player>(op.Value);
                InvokeOnSelfUpdateEvent(this, new Revision<Fibbage4Player>(_gameState.Self, self));
                _gameState.Self = self;
            }
        }

    }
}
