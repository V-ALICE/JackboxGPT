using Newtonsoft.Json;

namespace JackboxGPT.Games.JokeBoat.Models
{
    public struct JokeForMeRequest
    {
        [JsonProperty("action")]
        public static string Action => "jokeForMe";
    }
}