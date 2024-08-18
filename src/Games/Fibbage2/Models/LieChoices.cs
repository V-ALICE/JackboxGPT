using JackboxGPT3.Games.Common.Models;
using Newtonsoft.Json;

namespace JackboxGPT3.Games.Fibbage2.Models
{
    public struct LieChoices : ISelectionChoice
    {
        [JsonProperty("text")]
        public string Text { get; set; }
        public string SelectionText => Text;
    }
}
