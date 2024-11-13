using Newtonsoft.Json;

namespace JackboxGPT.Games.SurveyScramble.Models
{
    public struct SetTeamRequest
    {
        public SetTeamRequest(string team)
        {
            SetTeam = team;
        }

        [JsonProperty("setTeam")]
        public string SetTeam { get; set; }
    }
}