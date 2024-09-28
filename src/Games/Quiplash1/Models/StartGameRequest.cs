using Newtonsoft.Json;

namespace JackboxGPT3.Games.Quiplash1.Models
{
    public struct StartGameRequest
    {

        [JsonProperty("startGame")]
        public static bool StartGame => true;
    }
}