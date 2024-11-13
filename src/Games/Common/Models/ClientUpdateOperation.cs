using Newtonsoft.Json;

namespace JackboxGPT.Games.Common.Models
{
    public struct ClientUpdateOperation<TValue>
    {
        [JsonProperty("key")]
        public string Key { get; set; }


        [JsonProperty("val")]
        public TValue Value { get; set; }
    }
}
