using JackboxGPT.Games.Common;
using JackboxGPT.Games.Bracketeering.Models;
using JackboxGPT.Services;
using Serilog;
using JackboxGPT.Games.Common.Models;

namespace JackboxGPT.Games.Bracketeering;

public class BracketeeringClient : BcSerializedClient<BracketeeringRoom, BracketeeringPlayer>
{
    public BracketeeringClient(IConfigurationProvider configuration, ILogger logger, int instance)
        : base(configuration, logger, instance)
    {
    }

    public void ChooseIndex(int index)
    {
        var req = new ChooseRequest<int>(index);
        ClientSend(req);
    }

    public void WriteEntry(string entry)
    {
        var req = new WriteEntryRequest(entry);
        ClientSend(req);
    }
}