using Newtonsoft.Json;

namespace JackboxGPT3.Games.Quiplash2.Models
{
    public struct SafetyQuipRequest
    {
        [JsonProperty("safetyQuip")]
        public static bool SafetyQuip => true;

        [JsonProperty("questionId")]
        public int QuestionId { get; set; }
    }
}