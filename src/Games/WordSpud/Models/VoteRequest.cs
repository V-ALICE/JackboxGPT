using Newtonsoft.Json;

namespace JackboxGPT.Games.WordSpud.Models
{
    public struct VoteRequest
    {
        public VoteRequest(int vote)
        {
            Vote = vote;
        }
        
        [JsonProperty("vote")]
        public int Vote { get; set; }
    }
}