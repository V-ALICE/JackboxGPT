#nullable enable
using JackboxGPT.Games.Common;
using JackboxGPT.Games.Common.Models;
using JackboxGPT.Games.SurveyScramble.Models;
using JackboxGPT.Services;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;

namespace JackboxGPT.Games.SurveyScramble;

public class SurveyScrambleClient : PlayerSerializedClient<SurveyScrambleRoom, SurveyScramblePlayer>
{
    private const string KEY_ROUND_INFO = "roundInfo";
    private const string KEY_HORSERACE_INFO = "horseRaceInfo";
    private const string KEY_TEXT_DESC = "textDescriptions";
    private const string KEY_GUESS_TEXT = "textGuess";
    private const string KEY_GUESS_OBJ = "objectGuess";
    private const string KEY_VOTE = "voteResponse";

    public event EventHandler<SurveyScrambleRoundInfo>? OnRoundInfoReceived;
    public event EventHandler<SurveyScrambleTextDescriptions>? OnTextDescriptionsReceived;
    public event EventHandler<Dictionary<string, HorseRacePlayerInfo>>? OnHorseRaceInfoReceived;

    public SurveyScrambleClient(IConfigurationProvider configuration, ILogger logger, int instance)
        : base(configuration, logger, instance)
    {
    }

    public void Guess(string val)
    {
        ClientUpdate(val, KEY_GUESS_TEXT);
    }

    public void GuessCategory(int val)
    {
        var req = new ChoiceRequest(val);
        ClientUpdate(req, KEY_GUESS_OBJ);
    }

    public void GuessSuggestion(string val)
    {
        var req = new SuggestionRequest(val);
        ClientUpdate(req, KEY_GUESS_OBJ);
    }

    public void ChooseTeam(string currentTeam, bool joinLeftTeam)
    {
        var desiredTeam = joinLeftTeam ? "FirstTeam" : "SecondTeam";
        if (currentTeam == desiredTeam) return;

        var req = new SetTeamRequest(desiredTeam);
        ClientUpdate(req, KEY_VOTE);
    }

    public void LockInTeam()
    {
        var req = new LockInRequest();
        ClientUpdate(req, KEY_VOTE);
    }

    public void ChooseCategory(int val)
    {
        var req = new ChoiceRequest(val);
        ClientUpdate(req, KEY_VOTE);
    }

    public void ChooseOption(int idx, bool doubleDown)
    {
        var req = new ChooseOptionRequest(idx, doubleDown);
        ClientUpdate(req, KEY_GUESS_OBJ);
    }

    public void ChooseDirection(string higher, string? points = null)
    {
        var req = new ChooseDirectionRequest(higher, points);
        ClientUpdate(req, KEY_GUESS_OBJ);
    }

    public void GuessRank(int rank)
    {
        var req = new CallShotRankRequest(rank);
        ClientUpdate(req, KEY_GUESS_OBJ);
    }

    public void SabotagePlayer(int id)
    {
        var req = new SabotageRequest(id);
        ClientUpdate(req, KEY_GUESS_OBJ);
    }

    protected override void HandleOperation(IOperation op)
    {
        base.HandleOperation(op);

        switch (op.Key)
        {
            // Inconveniently the prompts are only in these messages
            case KEY_ROUND_INFO:
                var roundInfo = JsonConvert.DeserializeObject<SurveyScrambleRoundInfo>(op.Value);
                OnRoundInfoReceived?.Invoke(this, roundInfo);
                break;
            case KEY_HORSERACE_INFO:
                var raceInfo = JsonConvert.DeserializeObject<Dictionary<string, HorseRacePlayerInfo>>(op.Value);
                if (raceInfo != null) OnHorseRaceInfoReceived?.Invoke(this, raceInfo);
                break;
            case KEY_TEXT_DESC:
                var textDesc = JsonConvert.DeserializeObject<SurveyScrambleTextDescriptions>(op.Value);
                OnTextDescriptionsReceived?.Invoke(this, textDesc);
                break;
        }
    }
}