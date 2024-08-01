﻿using JackboxGPT3.Games.Fibbage2;
using JackboxGPT3.Services;
using Serilog;

namespace JackboxGPT3.Engines
{
    // The fibbage2 engine is set up to support both fibbage 1 and 2 since they're nearly identical
    // This just serves to override the tag correctly
    public class Fibbage1Engine : Fibbage2Engine
    {
        protected override string Tag => "fibbage";
        
        public Fibbage1Engine(ICompletionService completionService, ILogger logger, Fibbage2Client client, int instance)
            : base(completionService, logger, client, instance) { }
    }
}