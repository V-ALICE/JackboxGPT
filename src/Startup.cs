﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Autofac;
using JackboxGPT.Engines;
using JackboxGPT.Games.BlatherRound;
using JackboxGPT.Games.Common.Models;
using JackboxGPT.Games.Fibbage2;
using JackboxGPT.Games.Fibbage3;
using JackboxGPT.Games.Fibbage4;
using JackboxGPT.Games.JokeBoat;
using JackboxGPT.Games.Quiplash1;
using JackboxGPT.Games.Quiplash2;
using JackboxGPT.Games.Quiplash3;
using JackboxGPT.Games.SurveyScramble;
using JackboxGPT.Games.SurviveTheInternet;
using JackboxGPT.Games.WordSpud;
using JackboxGPT.Services;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;

namespace JackboxGPT
{
    public static class Startup
    {
        private static readonly HttpClient _httpClient = new();

        public static async Task Bootstrap(DefaultConfigurationProvider configuration, ManagedConfigFile configFile)
        {
            var logger = new LoggerConfiguration()
                .MinimumLevel.Is(Enum.Parse<LogEventLevel>(configuration.LogLevel, true))
                .WriteTo.Console(outputTemplate:
                    "{Prefix}[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Logger = logger;

            var instances = new List<Task>();
            for (var i = 1; i <= configuration.WorkerCount; i++)
            {
                instances.Add(BootstrapInternal(configuration, logger, configFile, i));
            }
            await Task.WhenAll(instances);
        }

        private static async Task BootstrapInternal(IConfigurationProvider configuration, ILogger logger, ManagedConfigFile configFile, int instanceNum)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(configuration).As<IConfigurationProvider>();
            builder.RegisterType<OpenAICompletionService>().As<ICompletionService>();
            builder.RegisterInstance(logger).SingleInstance();
            builder.RegisterInstance(configFile).SingleInstance();
            builder.Register(instance => instanceNum);

            builder.RegisterGameEngines();

            var container = builder.Build();

            var roomCode = configuration.RoomCode.ToUpper();
            var ecastHost = configuration.EcastHost;

            if (instanceNum == 0)
            {
                logger.Information("Starting up...");
                logger.Debug($"Ecast host: {ecastHost}");
                logger.Information($"Trying to join room with code: {roomCode}");
            }

            var response = await _httpClient.GetAsync($"https://{ecastHost}/api/v2/rooms/{roomCode}");

            try
            {
                response.EnsureSuccessStatusCode();
            } catch(HttpRequestException ex)
            {
                if (ex.StatusCode != HttpStatusCode.NotFound)
                    throw;

                logger.Error("Room not found.");
                return;
            }

            var roomResponse = JsonConvert.DeserializeObject<GetRoomResponse>(await response.Content.ReadAsStringAsync());
            var tag = roomResponse.Room.AppTag;

            if (!container.IsRegisteredWithKey<IJackboxEngine>(tag))
            {
                logger.Error($"Unsupported game: {tag}");
                return;
            }

            if (instanceNum == 0)
                logger.Information($"Room found! Starting up {tag} engine...");
            container.ResolveNamed<IJackboxEngine>(tag);
        }

        private static void RegisterGameEngines(this ContainerBuilder builder)
        {
            // Game engines, keyed with appTag

            // Fibbage 1 uses Fibbage 2 client, so nothing else to register
            builder.RegisterType<Fibbage1Engine>().Keyed<IJackboxEngine>("fibbage");

            builder.RegisterType<Fibbage2Client>();
            builder.RegisterType<Fibbage2Engine>().Keyed<IJackboxEngine>("fibbage2");

            builder.RegisterType<Fibbage3Client>();
            builder.RegisterType<Fibbage3Engine>().Keyed<IJackboxEngine>("fibbage3");

            builder.RegisterType<Fibbage4Client>();
            builder.RegisterType<Fibbage4Engine>().Keyed<IJackboxEngine>("fourbage");

            builder.RegisterType<Quiplash1Client>();
            builder.RegisterType<Quiplash1Engine>().Keyed<IJackboxEngine>("quiplash");

            builder.RegisterType<Quiplash2Client>();
            builder.RegisterType<Quiplash2Engine>().Keyed<IJackboxEngine>("quiplash2");

            builder.RegisterType<Quiplash3Client>();
            builder.RegisterType<Quiplash3Engine>().Keyed<IJackboxEngine>("quiplash3");
            
            builder.RegisterType<WordSpudClient>();
            builder.RegisterType<WordSpudEngine>().Keyed<IJackboxEngine>("wordspud");
            
            builder.RegisterType<SurviveTheInternetClient>();
            builder.RegisterType<SurviveTheInternetEngine>().Keyed<IJackboxEngine>("survivetheinternet");
            
            builder.RegisterType<BlatherRoundClient>();
            builder.RegisterType<BlatherRoundEngine>().Keyed<IJackboxEngine>("blanky-blank");

            builder.RegisterType<JokeBoatClient>();
            builder.RegisterType<JokeBoatEngine>().Keyed<IJackboxEngine>("jokeboat");

            builder.RegisterType<SurveyScrambleClient>();
            builder.RegisterType<SurveyScrambleEngine>().Keyed<IJackboxEngine>("bigsurvey");
        }
    }
}
