using JackboxGPT.Games.Quiplash3;
using JackboxGPT.Services;
using Serilog;

namespace JackboxGPT.Engines
{
    // The jackbox party starter is just quiplash 3 with moderation and other tools, so Fibbage 1 situation
    // Tag override then
    public class Quiplash3tjspEngine : Quiplash3Engine
    {
        protected override string Tag => "quiplash3-tjsp";
        
        public Quiplash3Engine(ICompletionService completionService, ILogger logger, Quiplash3Client client, ManagedConfigFile configFile, int instance, uint coinFlip)
            : base(completionService, logger, client, configFile, instance, coinFlip) { }
    }
}
