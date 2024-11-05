#nullable enable
using JackboxGPT.Games.Common;
using JackboxGPT.Games.Common.Models;
using JackboxGPT.Games.SurveyScramble.Models;
using JackboxGPT.Services;
using Newtonsoft.Json;
using Serilog;
using System;

namespace JackboxGPT.Games.SurveyScramble;

public class SurveyScrambleClient : PlayerSerializedClient<SurveyScrambleRoom, SurveyScramblePlayer>
{
    private const string KEY_ROUND_INFO = "roundInfo";
    private const string KEY_GUESS_TEXT = "textGuess";
    private const string KEY_GUESS_OBJ = "objectGuess";
    private const string KEY_VOTE = "voteResponse";

    public event EventHandler<SurveyScrambleRoundInfo>? OnRoundInfoReceived;

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

    protected override void HandleOperation(IOperation op)
    {
        base.HandleOperation(op);

        // Inconveniently the prompts are only in these messages
        if (op.Key == KEY_ROUND_INFO)
        {
            var info = JsonConvert.DeserializeObject<SurveyScrambleRoundInfo>(op.Value);
            OnRoundInfoReceived?.Invoke(this, info);
        }
    }
}