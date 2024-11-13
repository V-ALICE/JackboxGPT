using Newtonsoft.Json;

namespace JackboxGPT.Games.Fibbage2.Models
{
    public struct StartGameRequest
    {

        [JsonProperty("startGame")]
        public bool StartGame => true;
    }
}