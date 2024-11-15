using JackboxGPT.Services;
using Serilog;

namespace JackboxGPT.Games.Common
{
    /// <summary>
    /// A Jackbox client for games which use the "bc:" serialization format. It's
    /// named this way because the keys sent by the game are prefixed with "bc:".
    /// I have no idea what "bc" stands for but it seems several games share this
    /// serialization format. 
    /// </summary>
    public abstract class BcSerializedClient<TRoom, TPlayer> : BaseJackboxClient<TRoom, TPlayer>
    {
        protected BcSerializedClient(IConfigurationProvider configuration, ILogger logger, int instance = BASE_INSTANCE) : base(configuration, logger, instance)  {  }
        
        protected override string KEY_ROOM => "bc:room";
        protected override string KEY_PLAYER_PREFIX => "bc:customer:";
    }
}