using JackboxGPT3.Games.Common;
using JackboxGPT3.Games.Common.Models;
using JackboxGPT3.Games.JokeBoat.Models;
using JackboxGPT3.Services;
using Serilog;

namespace JackboxGPT3.Games.JokeBoat;

public class JokeBoatClient : BcSerializedClient<JokeBoatRoom, JokeBoatPlayer>
{
    public JokeBoatClient(IConfigurationProvider configuration, ILogger logger, int instance)
        : base(configuration, logger, instance) { }

    public void ChooseIndex(int index)
    {
        var req = new ChooseRequest<int>(index);
        ClientSend(req);
    }
    
    public void SubmitEntry(string entry)
    {
        var req = new WriteEntryRequest(entry);
        ClientSend(req);
    }

    public void RequestJokeForMe()
    {
        var req = new JokeForMeRequest();
        ClientSend(req);
    }
}