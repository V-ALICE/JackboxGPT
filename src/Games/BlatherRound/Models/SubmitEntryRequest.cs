using Newtonsoft.Json;

namespace JackboxGPT.Games.BlatherRound.Models
{
    public class SubmitEntryRequest<T>
    {
        public SubmitEntryRequest(T entry)
        {
            Entry = entry;
        }
        
        [JsonProperty("entry")]
        public T Entry { get; set; }
    }
}