using Newtonsoft.Json;

namespace JackboxGPT.Games.SurveyScramble.Models
{
    public struct ChooseDirectionRequest
    {
        public ChooseDirectionRequest(string direction, string points)
        {
            Points = points;
            Direction = direction;
        }

        [JsonProperty("points")]
        public string Points { get; set; }

        [JsonProperty("direction")]
        public string Direction { get; set; }
    }
}
