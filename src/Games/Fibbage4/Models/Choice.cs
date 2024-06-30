using Newtonsoft.Json;

namespace JackboxGPT3.Games.Fibbage4.Models
{
    public struct Choice
    {
        [JsonProperty("text")]
        public string Text { get; set; }
    }
}