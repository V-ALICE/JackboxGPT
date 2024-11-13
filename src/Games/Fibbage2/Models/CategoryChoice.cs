using Newtonsoft.Json;

namespace JackboxGPT.Games.Fibbage2.Models
{
    public struct CategoryChoice
    {
        [JsonProperty("chosenCategory")]
        public int ChosenCategory { get; set; }
    }
}
