using Newtonsoft.Json;

namespace JackboxGPT.Games.SurveyScramble.Models
{
    public struct SabotageRequest
    {
        public SabotageRequest(int id)
        {
            Sabotage = id;
        }

        [JsonProperty("sabotage")]
        public int Sabotage { get; set; }
    }
}