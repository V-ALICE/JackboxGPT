using Newtonsoft.Json;

namespace JackboxGPT.Games.Common.Models
{
    public struct ChooseRequest<TChoice>
    {
        public ChooseRequest(TChoice choice)
        {
            Choice = choice;
        }
        
        [JsonProperty("action")]
        public static string Action => "choose";

        [JsonProperty("choice")]
        public TChoice Choice { get; set; }
    }
}
