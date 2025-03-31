using JackboxGPT.Games.Quiplash3;
using JackboxGPT.Services;
using Serilog;

namespace JackboxGPT.Engines
{
    // The jackbox party starter is literaly just quiplash 3 with moderation, so Fibbage 1 situation
    // Tag override then
    public class Quiplash3Engine : Quiplash3tjspEngine
    {
        protected override string Tag => "quiplash3-tjsp";
        
        public Quiplash3Engine(ICompletionService completionService, ILogger logger, Quiplash3Client client, ManagedConfigFile configFile, int instance, uint coinFlip)
            : base(completionService, logger, client, configFile, instance, coinFlip) { }
    }
}
