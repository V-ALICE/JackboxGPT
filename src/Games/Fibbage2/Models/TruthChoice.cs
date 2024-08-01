using Newtonsoft.Json;

namespace JackboxGPT3.Games.Fibbage2.Models
{
    public struct TruthChoice
    {
        [JsonProperty("choice")]
        public string Choice { get; set; }
    }
}
