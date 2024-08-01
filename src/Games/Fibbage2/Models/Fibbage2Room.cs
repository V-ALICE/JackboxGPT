using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JackboxGPT3.Games.Fibbage2.Models
{
    // ReSharper disable UnusedMember.Global
    [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum RoomState
    {
        [System.Runtime.Serialization.EnumMember(Value = "")]
        None,
        Lobby_PickBloop,
        Lobby_WaitingForMore,
        Lobby_CanStart,
        Lobby_Countdown,
        Gameplay_Logo,
        Gameplay_Round,
        Gameplay_CategorySelection,
        Gameplay_EnterLie,
        Gameplay_LieReceived,
        Gameplay_LyingDone,
        Gameplay_ChooseLie,
        Gameplay_ChooseLike,
        Gameplay_EndShortie,
        Gameplay_EndGame,
        Lobby_PostGame,
        Lobby_PostGameHost
    }
    // ReSharper restore UnusedMember.Global

    public struct Fibbage2Room
    {
        public List<string> CategoryChoices
        {
            get
            {
                if (State != RoomState.Gameplay_CategorySelection)
                    return new List<string>();

                var parsed = JsonConvert.DeserializeObject<List<string>>(Choices.ToString());
                return parsed;
            }
        }

        // ReSharper disable once UnusedMember.Global
        public List<LieChoices> LieChoices
        {
            get
            {
                if (State != RoomState.Gameplay_ChooseLie)
                    return new List<LieChoices>();

                var parsed = JsonConvert.DeserializeObject<List<string>>(Choices.ToString());
                return parsed.Select(choice => new LieChoices { Text = choice }).ToList();
            }
        }

        [JsonProperty("choices")]
        public JRaw Choices { get; set; }

        [JsonProperty("choosingPlayerName")]
        public string ChoosingPlayerName { get; set; }

        [JsonProperty("state")]
        public RoomState State { get; set; }

        [JsonProperty("questionNumber")]
        public float QuestionNumber { get; set; }

        [JsonProperty("question")]
        public string Question { get; set; }

        [JsonProperty("answer")]
        public string Answer { get; set; }

        [JsonProperty("analytics")]
        public JRaw Analytics { get; set; }
    }
}
