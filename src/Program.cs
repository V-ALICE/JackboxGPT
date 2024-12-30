using CommandLine;
using dotenv.net;
using JackboxGPT.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tomlyn;

namespace JackboxGPT
{
    public static class Program
    {
        private static void GetUserInput(CommandLineConfigurationProvider config)
        {
            // Request instance count and room code from user
            var instances = "";
            while (!int.TryParse(instances, out _))
            {
                Console.Write("Number of instances: ");
                instances = Console.ReadLine() ?? "";
            }
            var roomCode = "";
            while (roomCode.Length != 4 || !roomCode.All(char.IsLetter))
            {
                Console.Write("Room Code: ");
                roomCode = Console.ReadLine() ?? "";
            }
            config.WorkerCount = int.Parse(instances);
            config.RoomCode = roomCode;
        }

        private static bool CheckEndOfService()
        {
            Console.WriteLine("");
            while (true)
            {
                Console.Write("New Room? [Y/N]: ");
                var answer = Console.ReadLine() ?? "";

                if (answer.Trim().ToUpper() == "Y") return true;
                if (answer.Trim().ToUpper() == "N") return false;
            }
        }

        private static CommandLineConfigurationProvider GetBaseConfig(IReadOnlyCollection<string> args, ManagedConfigFile configFile)
        {
            // Command line overrides config file and skips user input
            if (args.Count > 0)
                return Parser.Default.ParseArguments<CommandLineConfigurationProvider>(args).Value;

            // Load entries from config file
            var conf = new CommandLineConfigurationProvider
            {
                PlayerName = configFile.General.PlayerName,
                LogLevel = configFile.General.LoggingLevel,
                OpenAICompletionEngine = configFile.Model.CompletionEngine,
                OpenAIChatEngine = configFile.Model.ChatEngine
            };

#if DEBUG
            // Allow debug logging in debug builds without needing to edit the config
            if (conf.LogLevel != "verbose")
                conf.LogLevel = "debug";
#endif

            return conf;
        }

        private static ManagedConfigFile LoadConfigFile(string filename)
        {
            var config = File.ReadAllText(filename);
            return Toml.ToModel<ManagedConfigFile>(config);
        }

        public static void Main(string[] args)
        {
            DotEnv.Load();
            var configFile = LoadConfigFile("config.toml");
            var baseConfig = GetBaseConfig(args, configFile);

            if (args.Length > 0)
            {
                // CLI session
                Startup.Bootstrap(baseConfig, configFile).Wait();
            }
            else do
            {
                // Interactive session
                GetUserInput(baseConfig);
                Startup.Bootstrap(baseConfig, configFile).Wait();
            } while (CheckEndOfService());
        }
    }
}
