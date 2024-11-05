using JackboxGPT.Games.Common.Models;
using Newtonsoft.Json;

namespace JackboxGPT.Games.Fibbage4.Models
{
    public struct Choice : ISelectionChoice
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        public string SelectionText => Text;
    }
}