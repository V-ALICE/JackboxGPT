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
        protected readonly int Instance = BASE_INSTANCE;

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
        }

        // ReSharper disable UnusedMember.Global
        protected void LogWarning(string text, bool onlyOnce = false, string prefix = "")
        {
            if (onlyOnce && Instance != BASE_INSTANCE) return;
            var client = $"client{(onlyOnce ? "s" : Instance.ToString())}";
            _logger.ForContext("Prefix", prefix).Warning($"[{Tag}][{client}] {text}");
        }

        protected void LogError(string text, bool onlyOnce = false, string prefix = "")
        {
            if (onlyOnce && Instance != BASE_INSTANCE) return;
            var client = $"client{(onlyOnce ? "s" : Instance.ToString())}";
            _logger.ForContext("Prefix", prefix).Error($"[{Tag}][{client}] {text}");
        }

        protected void LogDebug(string text, bool onlyOnce = false, string prefix = "")
        {
            if (onlyOnce && Instance != BASE_INSTANCE) return;
            var client = $"client{(onlyOnce ? "s" : Instance.ToString())}";
            _logger.ForContext("Prefix", prefix).Debug($"[{Tag}][{client}] {text}");
        }

        protected void LogVerbose(string text, bool onlyOnce = false, string prefix = "")
        {
            if (onlyOnce && Instance != BASE_INSTANCE) return;
            var client = $"client{(onlyOnce ? "s" : Instance.ToString())}";
            _logger.ForContext("Prefix", prefix).Verbose($"[{Tag}][{client}] {text}");
        }

        protected void LogInfo(string text, bool onlyOnce = false, string prefix = "")
        {
            if (onlyOnce && Instance != BASE_INSTANCE) return;
            var client = $"client{(onlyOnce ? "s" : Instance.ToString())}";
            _logger.ForContext("Prefix", prefix).Information($"[{Tag}][{client}] {text}");
        }
        // ReSharper restore UnusedMember.Global
    }
}
