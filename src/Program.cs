using CommandLine;
using dotenv.net;
using JackboxGPT3.Services;
using System;

namespace JackboxGPT3
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            DotEnv.AutoConfig();
#if DEBUG
            Parser.Default.ParseArguments<CommandLineConfigurationProvider>(args)
                .WithParsed((conf) =>
                {
                    Console.Write("Number of instances: ");
                    conf.WorkerCount = int.Parse(Console.ReadLine() ?? "1");
                    Console.Write("Room Code: ");
                    conf.RoomCode = (Console.ReadLine() ?? "ZZZZ").Trim();
                    Startup.Bootstrap(conf).Wait();
                });
#else
            Parser.Default.ParseArguments<CommandLineConfigurationProvider>(args)
                .WithParsed((conf) => Startup.Bootstrap(conf).Wait());
#endif
        }
    }
}
