using JackboxGPT3.Services;
using Serilog;

namespace JackboxGPT3.Games.Common
{
    public abstract class PlayerSerializedClient<TRoom, TPlayer> : BaseJackboxClient<TRoom, TPlayer>
    {
        protected PlayerSerializedClient(IConfigurationProvider configuration, ILogger logger, int instance = 0) : base(configuration, logger, instance)  {  }
        
        protected override string KEY_ROOM => "room";
        protected override string KEY_PLAYER_PREFIX => "player:";
    }
}