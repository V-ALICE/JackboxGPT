using JackboxGPT.Games.Common;
using JackboxGPT.Services;
using Serilog;


namespace JackboxGPT.Engines
{
    public abstract class BaseJackboxEngine<TClient> : IJackboxEngine
        where TClient : IJackboxClient
    {
        protected abstract string Tag { get; }
        
        protected readonly ICompletionService CompletionService;
        protected readonly TClient JackboxClient;

        protected readonly ManagedConfigFile Config;

        private readonly ILogger _logger;

        protected const int BASE_INSTANCE = 1;
        protected const string BASE_INSTANCE_NAME = "System";
        protected readonly int Instance = BASE_INSTANCE;
        protected readonly string InstanceName = "Client";

        protected bool UseChatEngine = false;

        protected BaseJackboxEngine(ICompletionService completionService, ILogger logger, TClient client)
        {
            CompletionService = completionService;
            JackboxClient = client;
            _logger = logger;
        }

        protected BaseJackboxEngine(ICompletionService completionService, ILogger logger, TClient client, ManagedConfigFile configFile, int instance)
        {
            CompletionService = completionService;
            JackboxClient = client;
            _logger = logger;
            Instance = instance;
            Config = configFile;
            InstanceName = $"{configFile.General.PlayerName}-{instance}";
        }

        // ReSharper disable UnusedMember.Global
        protected void LogError(string text, bool onlyOnce = false, string prefix = "")
        {
            if (onlyOnce && Instance != BASE_INSTANCE) return;

            var name = onlyOnce ? BASE_INSTANCE_NAME : InstanceName;
            _logger.ForContext("Prefix", prefix).Error($"[{{P1}}][{{P2}}] {text}", Tag, name);
        }

        protected void LogWarning(string text, bool onlyOnce = false, string prefix = "")
        {
            if (onlyOnce && Instance != BASE_INSTANCE) return;

            var name = onlyOnce ? BASE_INSTANCE_NAME : InstanceName;
            _logger.ForContext("Prefix", prefix).Warning($"[{{P1}}][{{P2}}] {text}", Tag, name);
        }
        protected void LogInfo(string text, bool onlyOnce = false, string prefix = "")
        {
            if (onlyOnce && Instance != BASE_INSTANCE) return;

            var name = onlyOnce ? BASE_INSTANCE_NAME : InstanceName;
            _logger.ForContext("Prefix", prefix).Information($"[{{P1}}][{{P2}}] {text}", Tag, name);
        }

        protected void LogDebug(string text, bool onlyOnce = false, string prefix = "")
        {
            if (onlyOnce && Instance != BASE_INSTANCE) return;

            var name = onlyOnce ? BASE_INSTANCE_NAME : InstanceName;
            _logger.ForContext("Prefix", prefix).Debug($"[{{P1}}][{{P2}}] {text}", Tag, name);
        }

        protected void LogVerbose(string text, bool onlyOnce = false, string prefix = "")
        {
            if (onlyOnce && Instance != BASE_INSTANCE) return;

            var name = onlyOnce ? BASE_INSTANCE_NAME : InstanceName;
            _logger.ForContext("Prefix", prefix).Verbose($"[{{P1}}][{{P2}}] {text}", Tag, name);
        }
        // ReSharper restore UnusedMember.Global
    }
}
