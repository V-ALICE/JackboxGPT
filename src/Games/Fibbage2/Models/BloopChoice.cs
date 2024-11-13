using Newtonsoft.Json;

namespace JackboxGPT.Games.Fibbage2.Models
{
    public struct BloopChoice
    {
        [JsonProperty("bloop")]
        public string Bloop { get; set; }
    }
}
