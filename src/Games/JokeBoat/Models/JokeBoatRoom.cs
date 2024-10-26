// This file was generated with jb_api_gen.py

#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JackboxGPT3.Games.JokeBoat.Models;

public enum LobbyState
{
    None,
    CanStart,
    Countdown,
    PostGame,
    WaitingForMore
}

public struct JokeBoatRoom
{
    [JsonProperty("activeContentId")]
    public JRaw ActiveContentId { get; set; } // Type is unknown because the value was always null in API data

    [JsonProperty("audience")]
    public RoomAudience? Audience { get; set; }

    [JsonProperty("classes")]
    public List<string> Classes { get; set; }

    [JsonProperty("formattedActiveContentId")]
    public JRaw FormattedActiveContentId { get; set; } // Type is unknown because the value was always null in API data

    [JsonProperty("gameCanStart")]
    public bool GameCanStart { get; set; }

    [JsonProperty("gameFinished")]
    public bool GameFinished { get; set; }

    [JsonProperty("gameIsStarting")]
    public bool GameIsStarting { get; set; }

    [JsonProperty("isLocal")]
    public bool IsLocal { get; set; }

    [JsonProperty("lobbyState")]
    public LobbyState LobbyState { get; set; }

    [JsonProperty("locale")]
    public string Locale { get; set; }

    [JsonProperty("platformId")]
    public string PlatformId { get; set; }

    [JsonProperty("state")]
    public State State { get; set; }

    [JsonProperty("textDescriptions")]
    public List<RoomTextDescriptions> TextDescriptions { get; set; }

    [JsonProperty("analytics")]
    public List<RoomAnalytics> Analytics { get; set; }
}

public struct RoomAudience
{
    [JsonProperty("state")]
    public State State { get; set; }
}

public struct RoomTextDescriptions
{
    [JsonProperty("category")]
    public string Category { get; set; }

    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("text")]
    public string Text { get; set; }
}

public struct RoomAnalytics
{
    [JsonProperty("appid")]
    public string Appid { get; set; }

    [JsonProperty("appname")]
    public string Appname { get; set; }

    [JsonProperty("appversion")]
    public string Appversion { get; set; }

    [JsonProperty("screen")]
    public string Screen { get; set; }

    [JsonProperty("action")]
    public string Action { get; set; }

    [JsonProperty("category")]
    public string Category { get; set; }

    [JsonProperty("value")]
    public int Value { get; set; }
}
