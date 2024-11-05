// ReSharper disable InconsistentNaming
namespace JackboxGPT.Services
{
    public interface IConfigurationProvider
    {
        public string EcastHost { get; }
        public string PlayerName { get; }
        public string RoomCode { get; }
        public string LogLevel { get; }
        
        public string OpenAIEngine { get; }
        public int WorkerCount { get; }
    }
}
