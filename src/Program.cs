using CommandLine;
using dotenv.net;
using JackboxGPT3.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tomlyn;

namespace JackboxGPT3
{
    public static class Program
    {
        private static CommandLineConfigurationProvider GetBaseConfig(IReadOnlyCollection<string> args, ManagedConfigFile configFile)
        {
            // Command line overrides config file
            if (args.Count > 0)
                return Parser.Default.ParseArguments<CommandLineConfigurationProvider>(args).Value;

            // Load entries from config file
            var conf = new CommandLineConfigurationProvider
            {
                PlayerName = configFile.General.Name,
                LogLevel = configFile.General.LoggingLevel,
                OpenAIEngine = configFile.General.Engine
            };

#if DEBUG
            // Allow debug logging in debug builds without needing to edit the config
            if (conf.LogLevel != "verbose")
                conf.LogLevel = "debug";
#endif

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
            conf.WorkerCount = int.Parse(instances);
            conf.RoomCode = roomCode;

            return conf;
        }

        private static ManagedConfigFile LoadConfigFile(string filename)
        {
            var config = File.ReadAllText(filename);
            return Toml.ToModel<ManagedConfigFile>(config);
        }

        public static void Main(string[] args)
        {
            var configFile = LoadConfigFile("config.toml");
            var baseConfig = GetBaseConfig(args, configFile);

            DotEnv.AutoConfig();
            Startup.Bootstrap(baseConfig, configFile).Wait();
        }
    }
}
