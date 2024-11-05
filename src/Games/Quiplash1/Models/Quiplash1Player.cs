using System.Collections.Generic;
using JackboxGPT.Games.Quiplash2.Models;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JackboxGPT.Games.Quiplash1.Models
{
    public struct Quiplash1Player
    {
        public bool ActuallyDoneVoting
        {
            get
            {
                if (DoneVoting == null) return false;
                return DoneVoting.ToString().StartsWith("\"") || JsonConvert.DeserializeObject<bool>(DoneVoting.ToString());
            }
        }

        [JsonProperty("playerColor")]
        public string PlayerColor { get; set; }

        [JsonProperty("playerName")]
        public string PlayerName { get; set; }

        [JsonProperty("isAllowedToStartGame")]
        public bool IsAllowedToStartGame { get; set; }

        [JsonProperty("state")]
        public RoomState State { get; set; }

        [JsonProperty("question")]
        public QuestionContent? Question { get; set; }

        [JsonProperty("doneVoting")]
        public JRaw DoneVoting { get; set; } // Can be string or bool for some reason

        [JsonProperty("showError")]
        public bool ShowError { get; set; }

        [JsonProperty("ignore")]
        public List<int> Ignore { get; set; }

        [JsonProperty("votes")]
        public JRaw Votes { get; set; }

        [JsonProperty("votesLeft")]
        public float VotesLeft { get; set; }
    }
}