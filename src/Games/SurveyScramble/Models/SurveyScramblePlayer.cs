// This file was generated with jb_api_gen.py

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JackboxGPT.Games.SurveyScramble.Models;

public enum Kind
{
    None,
    Bounce,
    ChoiceWaiting,
    Choices,
    ChoicesWaiting,
    Dare,
    DareText,
    HighLow,
    HorseRace,
    HorseRaceSabotage,
    HorseRaceWaiting,
    Lobby,
    Speed,
    TeamChoice,
    TeamWaiting,
    TicTacToe,
    Waiting,
    PostGame
}

public struct SurveyScramblePlayer
{
    public List<string> CategoryChoices
    {
        get
        {
            if (Kind != Kind.Choices)
                return new List<string>();

            var parsed = JsonConvert.DeserializeObject<List<TextObj>>(Choices.ToString());
            return parsed == null ? new List<string>() : parsed.Select(a => a.Text).ToList();
        }
    }

    [JsonProperty("kind")]
    public Kind Kind { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("choices")]
    public JRaw Choices { get; set; } // List of several things (strings or multiple different structs)

    [JsonProperty("prompt")]
    public string Prompt { get; set; }

    [JsonProperty("responseKey")]
    public string ResponseKey { get; set; }

    [JsonProperty("votedIndex")]
    public int VotedIndex { get; set; }

    [JsonProperty("currentTeamName")]
    public string CurrentTeamName { get; set; }

    [JsonProperty("roundFormat")]
    public string RoundFormat { get; set; }

    [JsonProperty("teammateIds")]
    public List<int> TeammateIds { get; set; }

    [JsonProperty("teamMembers")]
    public List<int> TeamMembers { get; set; }

    [JsonProperty("teamName")]
    public string TeamName { get; set; }

    [JsonProperty("clearTextAfterSubmission")]
    public bool ClearTextAfterSubmission { get; set; }

    [JsonProperty("inTutorial")]
    public bool InTutorial { get; set; }

    [JsonProperty("instructions")]
    public string Instructions { get; set; }

    [JsonProperty("preventResponseLock")]
    public bool PreventResponseLock { get; set; }

    [JsonProperty("suggestions")]
    public List<PlayerSuggestions> Suggestions { get; set; }

    [JsonProperty("textKey")]
    public string TextKey { get; set; }

    [JsonProperty("feedback")]
    public PlayerFeedback Feedback { get; set; }

    [JsonProperty("usedGuesses")]
    public List<PlayerUsedGuesses> UsedGuesses { get; set; }

    [JsonProperty("canEnd")]
    public bool CanEnd { get; set; }

    [JsonProperty("canStart")]
    public bool CanStart { get; set; }

    [JsonProperty("textEntry")]
    public PlayerTextEntry TextEntry { get; set; }

    [JsonProperty("highDifficulty")]
    public int HighDifficulty { get; set; }

    [JsonProperty("lowDifficulty")]
    public int LowDifficulty { get; set; }

    [JsonProperty("nextPlayer")]
    public int NextPlayer { get; set; }

    [JsonProperty("dareDifficulty")]
    public int DareDifficulty { get; set; }

    [JsonProperty("goal")]
    public string Goal { get; set; }

    [JsonProperty("objectResponseKey")]
    public string ObjectResponseKey { get; set; }

    [JsonProperty("showCallShot")]
    public bool ShowCallShot { get; set; }

    [JsonProperty("textResponseKey")]
    public string TextResponseKey { get; set; }

    [JsonProperty("successfulGuess")]
    public string? SuccessfulGuess { get; set; }

    [JsonProperty("decoys")]
    public List<string> Decoys { get; set; }

    [JsonProperty("infoEntityKey")]
    public string InfoEntityKey { get; set; }

    [JsonProperty("options")]
    public List<string> Options { get; set; }

    [JsonProperty("scoreToWin")]
    public int ScoreToWin { get; set; }

    [JsonProperty("doubledDown")]
    public bool DoubledDown { get; set; }

    [JsonProperty("optionIndex")]
    public int OptionIndex { get; set; }

    [JsonProperty("canEndRound")]
    public bool CanEndRound { get; set; }

    [JsonProperty("objectKey")]
    public string ObjectKey { get; set; }

    [JsonProperty("drawInitiatedBy")]
    public string DrawInitiatedBy { get; set; }

    [JsonProperty("history")]
    public List<PlayerHistory> History { get; set; }

    [JsonProperty("showDraw")]
    public bool ShowDraw { get; set; }
}

public struct PlayerSuggestions
{
    [JsonProperty("active")]
    public bool Active { get; set; }

    [JsonProperty("isDisabled")]
    public bool IsDisabled { get; set; }

    [JsonProperty("player")]
    public int Player { get; set; }

    [JsonProperty("rank")]
    public int Rank { get; set; }

    [JsonProperty("text")]
    public string Text { get; set; }
}

public struct PlayerFeedback
{
    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("word")]
    public string Word { get; set; }
}

public struct PlayerUsedGuesses
{
    [JsonProperty("active")]
    public bool Active { get; set; }

    [JsonProperty("isDisabled")]
    public bool IsDisabled { get; set; }

    [JsonProperty("player")]
    public int Player { get; set; }

    [JsonProperty("rank")]
    public int Rank { get; set; }

    [JsonProperty("text")]
    public string Text { get; set; }
}

public struct PlayerTextEntry
{
    [JsonProperty("autoSubmit")]
    public bool AutoSubmit { get; set; }

    [JsonProperty("clearTextAfterSubmission")]
    public bool ClearTextAfterSubmission { get; set; }

    [JsonProperty("preventResponseLock")]
    public bool PreventResponseLock { get; set; }

    [JsonProperty("prompt")]
    public string Prompt { get; set; }

    [JsonProperty("responseKey")]
    public string ResponseKey { get; set; }

    [JsonProperty("responseType")]
    public string ResponseType { get; set; }
}

public struct PlayerHistory
{
    [JsonProperty("player")]
    public int Player { get; set; }

    [JsonProperty("rank")]
    public float Rank { get; set; } // Sometimes float, usually int

    [JsonProperty("text")]
    public string Text { get; set; }
}

public struct TextObj
{
    [JsonProperty("text")]
    public string Text { get; set; }
}
