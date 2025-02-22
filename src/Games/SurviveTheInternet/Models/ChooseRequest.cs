using Newtonsoft.Json;

namespace JackboxGPT.Games.SurviveTheInternet.Models
{
    public struct ChooseRequest
    {
        public ChooseRequest(int choice)
        {
            Choice = choice;
        }
        
        [JsonProperty("action")]
        public static string Action => "choose";
        
        [JsonProperty("choice")]
        public int Choice { get; set; }
    }
}