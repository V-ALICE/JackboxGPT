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

        protected readonly int Instance;

        protected BaseJackboxEngine(ICompletionService completionService, ILogger logger, TClient client, int instance = 0)
        {
            CompletionService = completionService;
            JackboxClient = client;
            _logger = logger;
            Instance = instance;
        }

        // ReSharper disable UnusedMember.Global
        protected void LogWarning(string text, bool onlyOnce = false)
        {
            if (onlyOnce && Instance != 0) return;
            _logger.Warning(onlyOnce ? $"[{Tag}][clients] {text}" : $"[{Tag}][client{Instance}] {text}");
        }

        protected void LogError(string text, bool onlyOnce = false)
        {
            if (onlyOnce && Instance != 0) return;
            _logger.Error(onlyOnce ? $"[{Tag}][clients] {text}" : $"[{Tag}][client{Instance}] {text}");
        }

        protected void LogDebug(string text, bool onlyOnce = false)
        {
            if (onlyOnce && Instance != 0) return;
            _logger.Debug(onlyOnce ? $"[{Tag}][clients] {text}" : $"[{Tag}][client{Instance}] {text}");
        }

        protected void LogVerbose(string text, bool onlyOnce = false)
        {
            if (onlyOnce && Instance != 0) return;
            _logger.Verbose(onlyOnce ? $"[{Tag}][clients] {text}" : $"[{Tag}][client{Instance}] {text}");
        }

        protected void LogInfo(string text, bool onlyOnce = false)
        {
            if (onlyOnce && Instance != 0) return;
            _logger.Information(onlyOnce ? $"[{Tag}][clients] {text}" : $"[{Tag}][client{Instance}] {text}");
        }
        // ReSharper restore UnusedMember.Global
    }
}
