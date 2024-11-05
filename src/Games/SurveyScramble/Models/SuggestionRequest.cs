using Newtonsoft.Json;

namespace JackboxGPT.Games.SurveyScramble.Models
{
    public struct SuggestionRequest
    {
        public SuggestionRequest(string suggestion)
        {
            Suggestion = suggestion;
        }
        
        [JsonProperty("suggestion")]
        public string Suggestion { get; set; }
    }
}
