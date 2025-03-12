// This file was generated with jb_api_gen.py

#nullable enable
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JackboxGPT.Games.BlatherRound.Models;

public enum LobbyState
{
    None,
    CanStart,
    Countdown,
    PostGame,
    WaitingForMore
}

public struct BlatherRoundRoom
{
    [JsonProperty("analytics")]
    public JRaw Analytics { get; set; } // Always empty list in API data

    [JsonProperty("audience")]
    public JRaw Audience { get; set; } // Always empty list in API data

    [JsonProperty("gameCanStart")]
    public bool GameCanStart { get; set; }

    [JsonProperty("gameFinished")]
    public bool GameFinished { get; set; }

    [JsonProperty("gameIsStarting")]
    public bool GameIsStarting { get; set; }

    [JsonProperty("lobbyState")]
    public LobbyState LobbyState { get; set; }

    [JsonProperty("locale")]
    public string Locale { get; set; }

    [JsonProperty("platformId")]
    public string PlatformId { get; set; }

    [JsonProperty("state")]
    public State State { get; set; }
}
