#nullable enable
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JackboxGPT3.Games.Fibbage4.Models
{
    // ReSharper disable UnusedMember.Global
    public enum RoomState
    {
        None,
        Lobby,
        Waiting,
        Choosing,
        Writing,
        PostGame
    }

    [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum RoomContext
    {
        None,
        Blankie,
        [System.Runtime.Serialization.EnumMember(Value = "double-blankie")]
        DoubleBlankie,
        [System.Runtime.Serialization.EnumMember(Value = "pick-category")]
        PickCategory,
        [System.Runtime.Serialization.EnumMember(Value = "pick-truth")]
        PickTruth,
        [System.Runtime.Serialization.EnumMember(Value = "pick-likes")]
        PickLikes,
        [System.Runtime.Serialization.EnumMember(Value = "final-round")]
        FinalRound,
        [System.Runtime.Serialization.EnumMember(Value = "final-round-1")]
        FinalRound1,
        [System.Runtime.Serialization.EnumMember(Value = "final-round-2")]
        FinalRound2
    }
    // ReSharper restore UnusedMember.Global

    public struct Fibbage4Player
    {
        public List<string> CategoryChoices
        {
            get
            {
                if (Context != RoomContext.PickCategory)
                    return new List<string>();

                var parsed = JsonConvert.DeserializeObject<List<Choice>>(Choices.ToString());
                return parsed == null ? new List<string>() : parsed.Select(c => c.Text).ToList();
            }
        }

        public List<string> SuggestionChoices
        {
            get
            {
                if (State != RoomState.Writing || Suggestions == null)
                    return new List<string>();

                var parsed = JsonConvert.DeserializeObject<List<string>>(Suggestions.ToString());
                return parsed ?? new List<string>();
            }
        }

        // ReSharper disable once UnusedMember.Global
        public List<Choice> LieChoices
        {
            get
            {
                if (Context != RoomContext.PickTruth &&
                    Context != RoomContext.FinalRound1 &&
                    Context != RoomContext.FinalRound2)
                    return new List<Choice>();

                var parsed = JsonConvert.DeserializeObject<List<Choice>>(Choices.ToString());
                return parsed ?? new List<Choice>();
            }
        }

        [JsonProperty("choices")]
        public JRaw Choices { get; set; }

        [JsonProperty("context")]
        public RoomContext Context { get; set; }

        [JsonProperty("kind")]
        public RoomState State { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("joiningPhrase")]
        public string JoiningPhrase { get; set; }

        [JsonProperty("maxLength")]
        public int MaxLength { get; set; }

        [JsonProperty("prompt1")]
        public string Question { get; set; }

        [JsonProperty("prompt2")]
        public string? Question2 { get; set; }

        [JsonProperty("suggestions")]
        public JRaw Suggestions { get; set; }

        [JsonProperty("error")]
        public string? Error { get; set; }
    }
}
