// This file was generated with jb_api_gen.py

#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JackboxGPT.Games.Bracketeering.Models;

public enum State
{
    None,
    EnterSingleText,
    Gameplay,
    Lobby,
    Logo,
    MakeSingleChoice
}

public struct BracketeeringPlayer
{
    [JsonProperty("isAllowedToStartGame")]
    public bool IsAllowedToStartGame { get; set; }

    [JsonProperty("playerIndex")]
    public int PlayerIndex { get; set; }

    [JsonProperty("playerInfo")]
    public PlayerPlayerInfo PlayerInfo { get; set; }

    [JsonProperty("playerName")]
    public string PlayerName { get; set; }

    [JsonProperty("state")]
    public State State { get; set; }

    [JsonProperty("doneText")]
    public string? DoneText { get; set; }

    [JsonProperty("entryId")]
    public string EntryId { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }

    [JsonProperty("inputType")]
    public string InputType { get; set; }

    [JsonProperty("maxLength")]
    public int MaxLength { get; set; }

    [JsonProperty("placeholder")]
    public JRaw Placeholder { get; set; } // Type is unknown because the value was always null in API data

    [JsonProperty("text")]
    public string Text { get; set; }

    [JsonProperty("entry")]
    public bool Entry { get; set; }

    [JsonProperty("choices")]
    public List<PlayerChoices> Choices { get; set; }

    [JsonProperty("chosen")]
    public int Chosen { get; set; }
}

public struct PlayerPlayerInfo
{
    [JsonProperty("avatar")]
    public PlayerPlayerInfoAvatar Avatar { get; set; }
}

public struct PlayerPlayerInfoAvatar
{
    [JsonProperty("frame")]
    public string Frame { get; set; }
}

public struct PlayerChoices
{
    [JsonProperty("text")]
    public string Text { get; set; }
}
