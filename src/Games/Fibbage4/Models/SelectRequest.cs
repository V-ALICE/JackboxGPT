using Newtonsoft.Json;

namespace JackboxGPT.Games.Fibbage4.Models
{
    public struct SelectRequest<TChoice>
    {
        public SelectRequest(TChoice choice)
        {
            Choice = choice;
        }

        [JsonProperty("action")]
        public static string Action => "select";

        [JsonProperty("choice")]
        public TChoice Choice { get; set; }
    }
}
