using System;
using System.Collections.Generic;
using System.Linq;
using JackboxGPT.Games.Quiplash2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JackboxGPT.Games.Quiplash1.Models
{
    public enum RoomState
    {
        Lobby,
        Gameplay_Logo,
        Gameplay_Round,
        Gameplay_AnswerQuestion,
        Gameplay_Vote
    }

    public enum LobbyState
    {
        WaitingForMore,
        CanStart,
        Countdown,
        PostGame
    }

    public struct Quiplash1Room
    {
        // Easiest format to work with in this case (doesn't require any further list creation/manipulation after)
        public Tuple<List<string>, List<string>> GetResponseChoices(List<int> ignoreKeys)
        {
            if (Choices == null || State != RoomState.Gameplay_Vote)
                    return new Tuple<List<string>, List<string>>(new List<string>(), new List<string>());

            var keys = new List<string>();
            var vals = new List<string>();
            foreach (var entry in Choices)
            {
                if (ignoreKeys != null && ignoreKeys.Contains(int.Parse(entry.Key))) continue;
                keys.Add(entry.Key);
                vals.Add(entry.Value);
            }
            return new Tuple<List<string>, List<string>>(keys, vals);
        }

        [JsonProperty("state")]
        public RoomState State { get; set; }

        [JsonProperty("lobbyState")]
        public LobbyState LobbyState { get; set; }

        [JsonProperty("round")]
        public int Round { get; set; }

        [JsonProperty("choices")]
        public Dictionary<string, string> Choices { get; set; }

        [JsonProperty("order")]
        public JRaw Order { get; set; }

        [JsonProperty("question")]
        public QuestionContent Question { get; set; }

        [JsonProperty("analytics")]
        public JRaw Analytics { get; set; }
    }

    public struct QuestionContent
    {
        [JsonProperty("id")]
        public int ID { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("random_for_content_manager")]
        public double RandomForContentManager { get; set; }

        [JsonProperty("x")]
        public bool X { get; set; }
    }
}
