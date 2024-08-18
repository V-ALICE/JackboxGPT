using Newtonsoft.Json;

namespace JackboxGPT3.Games.Fibbage3.Models
{
    public struct SuggestionsRequest
    {

        [JsonProperty("lieForMe")]
        public bool LieForMe => true;
    }
}