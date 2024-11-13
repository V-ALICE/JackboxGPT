using Newtonsoft.Json;

namespace JackboxGPT.Games.SurveyScramble.Models
{
    public struct LockInRequest
    {
        [JsonProperty("lockIn")]
        public static bool LockIn => true;
    }
}