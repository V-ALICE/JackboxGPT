using Newtonsoft.Json;

namespace JackboxGPT3.Games.Fibbage2.Models
{
    public struct SendEntryRequest
    {
        [JsonProperty("lieEntered")]
        public string LieEntered { get; set; }

        [JsonProperty("usedSuggestion")]
        public bool UsedSuggestion { get; set; }
    }
}
