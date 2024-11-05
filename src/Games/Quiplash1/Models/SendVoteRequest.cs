using Newtonsoft.Json;

namespace JackboxGPT.Games.Quiplash1.Models
{
    public struct SendVoteRequest<TVote>
    {
        [JsonProperty("vote")]
        public TVote Vote { get; set; }
    }
}