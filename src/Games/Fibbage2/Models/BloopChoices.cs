using Newtonsoft.Json;

namespace JackboxGPT3.Games.Fibbage2.Models
{
    public struct BloopChoices
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
