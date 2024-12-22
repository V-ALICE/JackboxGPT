using Newtonsoft.Json;

namespace JackboxGPT.Games.SurveyScramble.Models
{
    public struct CallShotRankRequest
    {
        public CallShotRankRequest(int calledShotRank)
        {
            CalledShotRank = calledShotRank;
        }

        [JsonProperty("calledShotRank")]
        public int CalledShotRank { get; set; }
    }
}