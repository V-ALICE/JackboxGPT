using JackboxGPT.Games.Common;
using JackboxGPT.Games.Common.Models;
using JackboxGPT.Games.Quiplash3.Models;
using JackboxGPT.Services;
using Serilog;

namespace JackboxGPT.Games.Quiplash3
{
    public class Quiplash3Client : PlayerSerializedClient<Quiplash3Room, Quiplash3Player>
    {
        public Quiplash3Client(IConfigurationProvider configuration, ILogger logger, int instance) : base(configuration, logger, instance) { }

        public void RequestSafetyQuip()
        {
            // Weird way that this game requests a safety quip
            var bytes = new byte[] { 0xE2, 0x81, 0x87 };
            SubmitQuip(System.Text.Encoding.UTF8.GetString(bytes));
        }

        public void ChooseCharacter(string name)
        {
            var req = new ChooseAvatarRequest(name);
            ClientSend(req);
        }
        
        public void SubmitQuip(string quip)
        {
            var req = new TextUpdateRequest(_gameState.Self.TextKey, quip);
            WsSend(TextUpdateRequest.OpCode, req);
        }

        public void ChooseFavorite(int index)
        {
            var req = new ChooseRequest<int>(index);
            ClientSend(req);
        }
    }
}
