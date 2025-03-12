// This file was generated with jb_api_gen.py

#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JackboxGPT.Games.Bracketeering.Models;

public enum LobbyState
{
    None,
    CanStart,
    Countdown,
    PostGame
}

public struct BracketeeringRoom
{
    [JsonProperty("audience")]
    public JRaw Audience { get; set; } // Type is unknown because the value was always null in API data

    [JsonProperty("lobbyState")]
    public LobbyState LobbyState { get; set; }

    [JsonProperty("platformId")]
    public string PlatformId { get; set; }

    [JsonProperty("state")]
    public State State { get; set; }

    [JsonProperty("analytics")]
    public List<RoomAnalytics> Analytics { get; set; }

    [JsonProperty("artifact")]
    public RoomArtifact Artifact { get; set; }
}

public struct RoomAnalytics
{
    [JsonProperty("action")]
    public string Action { get; set; }

    [JsonProperty("appid")]
    public string Appid { get; set; }

    [JsonProperty("appname")]
    public string Appname { get; set; }

    [JsonProperty("appversion")]
    public string Appversion { get; set; }

    [JsonProperty("category")]
    public string Category { get; set; }

    [JsonProperty("value")]
    public int Value { get; set; }
}

public struct RoomArtifact
{
    [JsonProperty("artifactId")]
    public string ArtifactId { get; set; }

    [JsonProperty("categoryId")]
    public string CategoryId { get; set; }

    [JsonProperty("rootId")]
    public string RootId { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; }
}
