using Newtonsoft.Json;

namespace JackboxGPT3.Games.Fibbage2.Models
{
    public struct StartGameRequest
    {

        [JsonProperty("startGame")]
        public bool StartGame => true;
    }
}