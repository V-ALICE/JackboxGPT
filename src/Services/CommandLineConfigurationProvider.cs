﻿using CommandLine;
// ReSharper disable ClassNeverInstantiated.Global

namespace JackboxGPT.Services
{
    public class CommandLineConfigurationProvider : DefaultConfigurationProvider
    {
        [Value(0, Required = true, HelpText = "The room code to join.", MetaName = "room-code")]
        public override string RoomCode { get; set; }
        
        [Option("name", Default = "GPT", HelpText = "The name the player should join the room as. The instance number of the client will be appended to this name (Name-#)")]
        public override string PlayerName { get; set; }

        [Option("engine", Default = "davinci-002", HelpText = "The GPT-3 model to use for completions. Some game engines may use alternate models. Possible values: davinci-002, babbage-002")]
        public override string OpenAIEngine { get; set; }

        [Option("verbosity", Default = "information", HelpText = "Log level to output. Possible values: verbose, debug, information, warning, error, fatal")]
        public override string LogLevel { get; set; }

        [Option("instances", Default = 1, HelpText = "The number of GPT players to spin up.")]
        public override int WorkerCount { get; set; }
    }
}
