using Newtonsoft.Json;

namespace JackboxGPT.Games.Common.Models
{
    public struct WriteEntryRequest
    {
        public WriteEntryRequest(string entry)
        {
            Entry = entry;
        }
        
        [JsonProperty("action")]
        public static string Action => "write";
        
        [JsonProperty("entry")]
        public string Entry { get; set; }
    }
}