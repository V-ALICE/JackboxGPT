using Newtonsoft.Json;

namespace JackboxGPT.Games.Quiplash2.Models
{
    public struct SendVoteRequest<TVote>
    {
        [JsonProperty("vote")]
        public TVote Vote { get; set; }
    }
}