using Newtonsoft.Json;

namespace JackboxGPT3.Games.Fibbage2.Models
{
    public struct BloopChoice
    {
        [JsonProperty("bloop")]
        public string Bloop { get; set; }
    }
}
