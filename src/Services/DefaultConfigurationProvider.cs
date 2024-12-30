namespace JackboxGPT.Services
{
    public abstract class DefaultConfigurationProvider : IConfigurationProvider
    {
        public string EcastHost => "ecast.jackboxgames.com";
        
        public abstract string PlayerName { get; set; }
        public abstract string OpenAICompletionEngine { get; set; }
        public abstract string OpenAIChatEngine { get; set; }
        public abstract string RoomCode { get; set; }
        public abstract string LogLevel { get; set; }
        public abstract int WorkerCount { get; set; }
    }
}
