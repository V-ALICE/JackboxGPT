using Newtonsoft.Json;

namespace JackboxGPT.Games.Fibbage4.Models
{
    public struct SuggestionsRequest
    {
        [JsonProperty("action")]
        public static string Action => "lieForMe";
    }
}