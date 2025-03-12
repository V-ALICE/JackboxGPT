using JackboxGPT.Extensions;
using JackboxGPT.Games.Common;
using JackboxGPT.Services;
using Serilog;
using System;
using System.Xml.Linq;


namespace JackboxGPT.Engines
{
    public abstract class BaseJackboxEngine<TClient> : IJackboxEngine
        where TClient : IJackboxClient
    {
        protected abstract string Tag { get; }

        protected readonly ICompletionService CompletionService;
        protected readonly TClient JackboxClient;

        protected readonly ManagedConfigFile Config;
        protected abstract ManagedConfigFile.EnginePreference EnginePref { get; }

        private readonly ILogger _logger;

        protected const int BASE_INSTANCE = 1;
        protected const string BASE_INSTANCE_NAME = " SYSTEM MSG "; // padded to 12 characters
        protected readonly int Instance = BASE_INSTANCE;
        protected string InstanceName = "Client";

        protected readonly Random RandGen = new();

        protected bool UsingChatEngine => EnginePref == ManagedConfigFile.EnginePreference.Chat
                                        || (EnginePref == ManagedConfigFile.EnginePreference.Mix && RandGen.Next(0, 2) == 0);

        protected BaseJackboxEngine(ICompletionService completionService, ILogger logger, TClient client)
        {
            CompletionService = completionService;
            JackboxClient = client;
            _logger = logger;

            CheckEnginePref();
        }

        protected BaseJackboxEngine(ICompletionService completionService, ILogger logger, TClient client, ManagedConfigFile configFile, int instance)
        {
            CompletionService = completionService;
            JackboxClient = client;
            _logger = logger;
            Instance = instance;
            Config = configFile;

            ApplyName(configFile.General.PlayerName);
            CheckEnginePref();
        }

        protected void ApplyName(string name)
        {
            if (name.Length > 9 && Instance >= 10) // Jackbox name length limit is 12, so this leaves room for -## after name
                name = name[..9];
            else if (name.Length > 10) // Jackbox name length limit is 12, so this leaves room for -# after name
                name = name[..10];

            var instanceName = $"{name}-{Instance}";
            InstanceName = instanceName.PadLeft(12);
            JackboxClient.SetName(instanceName);
        }

        public void ApplyRandomPersonality()
        {
            if (EnginePref == ManagedConfigFile.EnginePreference.Completion)
                return;

            var choice = Config.Model.ChatPersonalityTypes[Config.Model.ChatPersonalityTypes.RandomIndex()].ToLower();
            CompletionService.ApplyPersonalityType(choice);

            var split = choice.Split('`');
            var name = (split.Length == 1) ? choice.ToUpper() : split[1].ToUpper();

            ApplyName(name);
            LogInfo($"Applying personality \"{choice}\"");
        }

        private void CheckEnginePref()
        {
            switch (EnginePref)
            {
                case ManagedConfigFile.EnginePreference.Completion:
                    LogDebug("Using Completion engine", true);
                    break;
                case ManagedConfigFile.EnginePreference.Chat:
                    LogDebug("Using Chat engine", true);
                    break;
                case ManagedConfigFile.EnginePreference.Mix:
                    LogDebug("Using a mix of Completion and Chat engines", true);
                    break;
            }
            CompletionService.ResetAll();
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
