using System.Collections.Generic;
using Newtonsoft.Json;

namespace JackboxGPT.Games.SurveyScramble.Models;

public struct SurveyScrambleTextDescriptions
{
    [JsonProperty("latestDescriptions")]
    public List<LatestDescriptions> LatestDescriptions { get; set; }
}

public struct LatestDescriptions
{
    [JsonProperty("category")]
    public string Category { get; set; }

    [JsonProperty("text")]
    public string Text { get; set; }
}