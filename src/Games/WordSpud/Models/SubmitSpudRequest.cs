using Newtonsoft.Json;

namespace JackboxGPT.Games.WordSpud.Models
{
    public struct SubmitSpudRequest
    {
        public SubmitSpudRequest(string spud)
        {
            Spud = spud;
            Submitted = true;
        }
        
        [JsonProperty("spud")]
        public string Spud { get; set; }
        
        [JsonProperty("submitted")]
        public bool Submitted { get; set; }
    }
}