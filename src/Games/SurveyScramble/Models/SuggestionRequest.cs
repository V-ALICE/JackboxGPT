using Newtonsoft.Json;

namespace JackboxGPT3.Games.SurveyScramble.Models
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
