namespace JackboxGPT.Games.Common.Models
{
    public struct GameState<TRoom, TPlayer>
    {
        public int PlayerId;
        public TRoom Room;
        public TPlayer Self;
    }
}
