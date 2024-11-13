using Newtonsoft.Json;

namespace JackboxGPT.Games.Fibbage2.Models
{
    public struct TruthChoice
    {
        [JsonProperty("choice")]
        public string Choice { get; set; }
    }
}
