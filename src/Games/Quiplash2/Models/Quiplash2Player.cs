using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace JackboxGPT.Games.Quiplash2.Models
{
    public struct Quiplash2Player
    {
        public List<ChoiceData> ResponseChoices
        {
            get
            {
                if (Votes == null || State != RoomState.Gameplay_R3Vote)
                    return new List<ChoiceData>();

                var parsedList = JsonConvert.DeserializeObject<List<ChoiceData>>(Votes.ToString());
                return parsedList;
            }
        }

        [JsonProperty("canCensor")]
        public bool CanCensor { get; set; }

        [JsonProperty("canDoUGC")]
        public bool CanDoUgc { get; set; }

        [JsonProperty("canReport")]
        public bool CanReport { get; set; }

        [JsonProperty("canViewAuthor")]
        public bool CanViewAuthor { get; set; }

        [JsonProperty("history")]
        public JRaw History { get; set; }

        [JsonProperty("isAllowedToStartGame")]
        public bool IsAllowedToStartGame { get; set; }

        // [JsonProperty("lastUGCResult")]
        // public JRaw? LastUGCResult { get; set; }

        [JsonProperty("playerColor")]
        public string PlayerColor { get; set; }

        [JsonProperty("playerIndex")]
        public int PlayerIndex { get; set; }

        [JsonProperty("playerName")]
        public string PlayerName { get; set; }

        [JsonProperty("state")]
        public RoomState State { get; set; }

        [JsonProperty("question")]
        public QuestionContent? Question { get; set; }

        [JsonProperty("censorOnly")]
        public bool CensorOnly { get; set; }

        [JsonProperty("doneVoting")]
        public bool DoneVoting { get; set; }

        [JsonProperty("showError")]
        public bool ShowError { get; set; }

        [JsonProperty("votes")]
        public JRaw Votes { get; set; }

        [JsonProperty("votesLeft")]
        public float VotesLeft { get; set; }

        [JsonProperty("currentVote")]
        public int CurrentVote { get; set; }
    }
}
