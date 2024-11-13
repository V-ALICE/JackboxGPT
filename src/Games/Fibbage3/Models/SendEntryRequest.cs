using Newtonsoft.Json;

namespace JackboxGPT.Games.Fibbage3.Models
{
    public struct SendEntryRequest
    {
        public SendEntryRequest(string entry)
        {
            Entry = entry;
        }
        
        [JsonProperty("entry")]
        public string Entry { get; set; }
    }
}
