using System.Collections.Generic;
using Newtonsoft.Json;

namespace JackboxGPT.Games.SurveyScramble.Models;

public struct HorseRacePlayerInfo
{
    [JsonProperty("canSabotage")]
    public bool CanSabotage { get; set; }

    [JsonProperty("sabotagesReceived")]
    public List<int> SabotagesReceived { get; set; }

    [JsonProperty("score")]
    public float Score { get; set; } // Sometimes float, usually int
}
