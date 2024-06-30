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

        public void SubmitLie(string lie)
        {
            ClientUpdate(lie, KEY_ENTRYBOX1_SUBMIT);
            var req = new AnswerRequest();
            ClientUpdate(req, KEY_ENTRYBOX_ACTION);
        }

        public void SubmitDoubleLie(string lie1, string lie2)
        {
            ClientUpdate(lie1, KEY_ENTRYBOX1_SUBMIT);
            ClientUpdate(lie2, KEY_ENTRYBOX2_SUBMIT);
            var req = new AnswerRequest();
            ClientUpdate(req, KEY_ENTRYBOX_ACTION);
        }

        protected override void HandleOperation(IOperation op)
        {
            if (op.Key == $"{KEY_PLAYER_PREFIX}{_gameState.PlayerId}")
            {
                //Console.WriteLine(op.Value);
                var room = JsonConvert.DeserializeObject<Fibbage4Room>(op.Value);
                InvokeOnRoomUpdateEvent(this, new Revision<Fibbage4Room>(_gameState.Room, room));
                _gameState.Room = room;
            }
        }

    }
}
