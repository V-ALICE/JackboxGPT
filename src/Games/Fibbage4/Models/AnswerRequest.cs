using Newtonsoft.Json;

namespace JackboxGPT.Games.Fibbage4.Models
{
    public struct AnswerRequest
    {
        [JsonProperty("action")]
        public static string Action => "answer";
    }
}
