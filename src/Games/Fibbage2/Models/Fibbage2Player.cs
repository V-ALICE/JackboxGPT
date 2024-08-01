using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JackboxGPT3.Games.Fibbage2.Models
{
    public struct Fibbage2Player
    {
        public List<BloopChoices> BloopChoices
        {
            get
            {
                if (State != RoomState.Lobby_PickBloop)
                    return new List<BloopChoices>();

                var parsed = JsonConvert.DeserializeObject<List<BloopChoices>>(Bloops.ToString());
                return parsed;
            }
        }

        public List<string> SuggestionChoices
        {
            get
            {
                if (State != RoomState.Gameplay_EnterLie || Suggestions == null)
                    return new List<string>();

                var parsed = JsonConvert.DeserializeObject<List<string>>(Suggestions.ToString());
                return parsed;
            }
        }

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

        [JsonProperty("bloops")]
        public JRaw Bloops { get; set; }

        [JsonProperty("hasBloop")]
        public bool HasBloop { get; set; }

        [JsonProperty("playerColor")]
        public string PlayerColor { get; set; }

        [JsonProperty("playerIndex")]
        public int PlayerIndex { get; set; }

        [JsonProperty("playerName")]
        public string PlayerName { get; set; }

        [JsonProperty("state")]
        public RoomState State { get; set; }

        [JsonProperty("isChoosing")]
        public bool IsChoosing { get; set; }

        [JsonProperty("showError")]
        public bool ShowError { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("suggestions")]
        public JRaw Suggestions { get; set; }

        [JsonProperty("choosingDone")]
        public bool? ChoosingDone { get; set; }

        [JsonProperty("hasDefib")]
        public bool HasDefib { get; set; }

        [JsonProperty("likes")]
        public JRaw Likes { get; set; }

        [JsonProperty("chosen")]
        public string Chosen { get; set; }
    }
}
