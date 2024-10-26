using Newtonsoft.Json;

namespace JackboxGPT3.Games.JokeBoat.Models
{
    public struct JokeForMeRequest
    {
        [JsonProperty("action")]
        public static string Action => "jokeForMe";
    }
}