// This file was generated with jb_api_gen.py

#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JackboxGPT.Games.BlatherRound.Models;

public enum State
{
    None,
    EnterSingleText,
    Lobby,
    Logo,
    MakeSentence,
    MakeSingleChoice
}

public enum ChoiceType
{
    None,
    Mybad,
    Password,
    SkipTutorial
}

public struct BlatherRoundPlayer
{
    [JsonProperty("playerCanReport")]
    public bool PlayerCanReport { get; set; }

    [JsonProperty("playerCanStartGame")]
    public bool PlayerCanStartGame { get; set; }

    [JsonProperty("playerInfo")]
    public PlayerPlayerInfo PlayerInfo { get; set; }

    [JsonProperty("playerIsVIP")]
    public bool PlayerIsVIP { get; set; }

    [JsonProperty("state")]
    public State State { get; set; }

    [JsonProperty("choiceId")]
    public string ChoiceId { get; set; }

    [JsonProperty("choiceType")]
    public ChoiceType ChoiceType { get; set; }

    [JsonProperty("choices")]
    public List<PlayerChoices> Choices { get; set; }

    [JsonProperty("classes")]
    public List<string> Classes { get; set; }

    [JsonProperty("prompt")]
    public PlayerPrompt Prompt { get; set; }

    [JsonProperty("description")]
    public PlayerDescription Description { get; set; }

    [JsonProperty("category")]
    public PlayerCategory Category { get; set; }

    [JsonProperty("entryId")]
    public string EntryId { get; set; }

    [JsonProperty("error")]
    public JRaw Error { get; set; } // Type is unknown because the value was always null in API data

    [JsonProperty("inputType")]
    public string InputType { get; set; }

    [JsonProperty("sentence")]
    public Sentence Sentence { get; set; }

    [JsonProperty("entry")]
    public bool Entry { get; set; }

    [JsonProperty("maxLength")]
    public int MaxLength { get; set; }

    [JsonProperty("placeholder")]
    public string Placeholder { get; set; }

    [JsonProperty("strings")]
    public PlayerStrings Strings { get; set; }

    [JsonProperty("textKey")]
    public string TextKey { get; set; }

    [JsonProperty("chosen")]
    public int Chosen { get; set; }

    [JsonProperty("doneText")]
    public PlayerDoneText DoneText { get; set; }

    [JsonProperty("message")]
    public PlayerMessage Message { get; set; }

    [JsonProperty("playerCanCensor")]
    public bool PlayerCanCensor { get; set; }
}

public struct PlayerPlayerInfo
{
    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("username")]
    public string Username { get; set; }
}

public struct PlayerChoices
{
    [JsonProperty("html")]
    public string Html { get; set; }

    [JsonProperty("action")]
    public string Action { get; set; }

    [JsonProperty("category")]
    public string Category { get; set; }

    [JsonProperty("className")]
    public string ClassName { get; set; }
}

public struct PlayerPrompt
{
    [JsonProperty("html")]
    public string Html { get; set; }
}

public struct PlayerDescription
{
    [JsonProperty("html")]
    public string Html { get; set; }
}

public struct PlayerCategory
{
    [JsonProperty("html")]
    public string Html { get; set; }
}

public struct PlayerStrings
{
    [JsonProperty("ERROR_NOTHING_ENTERED")]
    public string ERROR_NOTHING_ENTERED { get; set; }

    [JsonProperty("ERROR_REJECTED_TEXT")]
    public string ERROR_REJECTED_TEXT { get; set; }
}

public struct PlayerDoneText
{
    [JsonProperty("html")]
    public string Html { get; set; }
}

public struct PlayerMessage
{
    [JsonProperty("html")]
    public string Html { get; set; }
}
