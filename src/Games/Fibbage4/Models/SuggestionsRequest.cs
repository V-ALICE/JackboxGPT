using Newtonsoft.Json;

namespace JackboxGPT3.Games.Fibbage4.Models
{
    public struct SuggestionsRequest
    {
        [JsonProperty("action")]
        public static string Action => "lieForMe";
    }
}