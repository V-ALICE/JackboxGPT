using Newtonsoft.Json;

namespace JackboxGPT3.Games.SurveyScramble.Models
{
    public struct LockInRequest
    {
        [JsonProperty("lockIn")]
        public static bool LockIn => true;
    }
}