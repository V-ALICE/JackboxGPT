using Newtonsoft.Json;

namespace JackboxGPT.Games.SurveyScramble.Models;

public struct SurveyScrambleRoundInfo
{
    [JsonProperty("longPrompt")]
    public string LongPrompt { get; set; }

    [JsonProperty("shortPrompt")]
    public string ShortPrompt { get; set; }

    [JsonProperty("roundFormat")]
    public string RoundFormat { get; set; }

    [JsonProperty("surveyLength")]
    public int SurveyLength { get; set; }
}
