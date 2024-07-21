using CommandLine;
using dotenv.net;
using JackboxGPT3.Services;
using System;
using System.Linq;

namespace JackboxGPT3
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            DotEnv.AutoConfig();
            if (args.Length == 0)
            {
                var conf = new CommandLineConfigurationProvider();
#if DEBUG
                conf.PlayerName = "DBUG";
                conf.LogLevel = "debug";
#else
                conf.PlayerName = "GPT";
                conf.LogLevel = "information";
#endif
                conf.OpenAIEngine = "davinci-002";

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
                Startup.Bootstrap(conf).Wait();
            }
            else
            {
                Parser.Default.ParseArguments<CommandLineConfigurationProvider>(args)
                    .WithParsed((conf) => Startup.Bootstrap(conf).Wait());
            }
        }
    }
}
