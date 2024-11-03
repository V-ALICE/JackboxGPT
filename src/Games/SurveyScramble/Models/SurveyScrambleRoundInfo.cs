using Newtonsoft.Json;

namespace JackboxGPT3.Games.SurveyScramble.Models;

public struct SurveyScrambleRoundInfo
{
    [JsonProperty("longPrompt")]
    public string LongPrompt { get; set; }

    [JsonProperty("shortPrompt")]
    public string ShortPrompt { get; set; }

    [JsonProperty("roundFormat")]
    public string RoundFormat { get; set; }
}
