using JackboxGPT.Games.Common.Models;
using Newtonsoft.Json;

namespace JackboxGPT.Games.Fibbage3.Models
{
    public struct LieChoice : ISelectionChoice
    {
        [JsonProperty("disabled")]
        public bool Disabled { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
        public string SelectionText => Text;
    }
}
