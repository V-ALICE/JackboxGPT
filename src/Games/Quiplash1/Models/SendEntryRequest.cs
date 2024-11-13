using Newtonsoft.Json;

namespace JackboxGPT.Games.Quiplash1.Models
{
    public struct SendEntryRequest
    {
        [JsonProperty("answer")]
        public string Answer { get; set; }

        [JsonProperty("questionId")]
        public int QuestionId { get; set; }
    }
}