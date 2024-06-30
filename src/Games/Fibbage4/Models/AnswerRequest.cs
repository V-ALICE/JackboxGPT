using Newtonsoft.Json;

namespace JackboxGPT3.Games.Fibbage4.Models
{
    public struct AnswerRequest
    {
        [JsonProperty("action")]
        public static string Action => "answer";
    }
}
