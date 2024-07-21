using JackboxGPT3.Games.Common;
using JackboxGPT3.Services;
using Serilog;

namespace JackboxGPT3.Engines
{
    public abstract class BaseJackboxEngine<TClient> : IJackboxEngine
        where TClient : IJackboxClient
    {
        protected abstract string Tag { get; }
        
        protected readonly ICompletionService CompletionService;
        protected readonly TClient JackboxClient;
        
        private readonly ILogger _logger;

        protected const int BASE_INSTANCE = 1;
        protected readonly int Instance;

        protected BaseJackboxEngine(ICompletionService completionService, ILogger logger, TClient client, int instance = BASE_INSTANCE)
        {
            CompletionService = completionService;
            JackboxClient = client;
            _logger = logger;
            Instance = instance;
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
