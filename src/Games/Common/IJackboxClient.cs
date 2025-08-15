using System;
using JackboxGPT.Games.Common.Models;
// ReSharper disable UnusedMemberInSuper.Global
// ReSharper disable EventNeverSubscribedTo.Global

namespace JackboxGPT.Games.Common
{
    public interface IJackboxClient
    {
        public void SetName(string name);
        public void Connect();
        public event EventHandler<ClientWelcome> PlayerStateChanged;
    }
}