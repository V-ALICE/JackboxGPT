using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JackboxGPT3.Games.Quiplash2.Models
{
    public enum RoomState
    {
        Lobby,
        Logo,
        Gameplay_Logo,
        Gameplay_Round,
        Gameplay_AnswerQuestion,
        Gameplay_Vote,
        Gameplay_R3Vote
    }

    public enum LobbyState
    {
        WaitingForMore,
        CanStart,
        Countdown,
        PostGame
    }

    public struct ChoiceData
    {
        [JsonProperty("answer")]
        public string Answer { get; set; }

        [JsonProperty("isCensored")]
        public bool IsCensored { get; set; }

        [JsonProperty("playerIndex")]
        public int PlayerIndex { get; set; }

        [JsonProperty("hasVote")]
        public bool HasVote { get; set; }

        public string Key { get; set; }
    }

    public enum R3Type
    {
        None,
        WordLash,
        AcroLash,
        ComicLash
    }

    public struct QuestionContent
    {
        [JsonProperty("id")]
        public int ID { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("quip")]
        public string Quip { get; set; }

        [JsonProperty("random_for_content_manager")]
        public double RandomForContentManager { get; set; }

        [JsonProperty("type")]
        public R3Type Type { get; set; }
    }

    public struct Quiplash2Room
    {
        public List<ChoiceData> ResponseChoices
        {
            get
            {
                if (Choices == null || (State != RoomState.Gameplay_Vote && State != RoomState.Gameplay_R3Vote))
                    return new List<ChoiceData>();

                if (State == RoomState.Gameplay_Vote)
                {
                    var parsedDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(Choices.ToString());
                    var output = parsedDict.Select(entry => new ChoiceData { Key = entry.Key, Answer = entry.Value });
                    return output.ToList();
                }

                var parsedList = JsonConvert.DeserializeObject<List<ChoiceData>>(Choices.ToString());
                return parsedList;
            }
        }

        // [JsonProperty("activeContentId")]
        // public int? ActiveContentId { get; set; }
        // 
        // [JsonProperty("artifact")]
        // public JRaw? Artifact { get; set; }
        // 
        // [JsonProperty("formattedActiveContentId")]
        // public string? FormattedActiveContentId { get; set; }

        [JsonProperty("isLocal")]
        public bool IsLocal { get; set; }

        [JsonProperty("lobbyState")]
        public LobbyState LobbyState { get; set; }

        [JsonProperty("platformId")]
        public string PlatformId { get; set; }

        [JsonProperty("state")]
        public RoomState State { get; set; }

        [JsonProperty("analytics")]
        public JRaw Analytics { get; set; }

        [JsonProperty("audienceQuip")]
        public string AudienceQuip { get; set; }

        [JsonProperty("canDoAudiencePlay")]
        public bool CanDoAudiencePlay { get; set; }

        [JsonProperty("safetyQuip")]
        public bool SafetyQuip { get; set; }

        [JsonProperty("choices")]
        public JRaw Choices { get; set; } // This can be a list or a dictionary

        [JsonProperty("isCensored")]
        public JRaw IsCensored { get; set; }

        [JsonProperty("order")]
        public JRaw Order { get; set; }

        [JsonProperty("question")]
        public QuestionContent Question { get; set; }

        [JsonProperty("round")]
        public int Round { get; set; }
    }

}
