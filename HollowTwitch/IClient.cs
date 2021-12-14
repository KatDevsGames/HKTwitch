using System;
using CrowdControl;
using HollowTwitch.Clients;
using HollowTwitch.Entities;

namespace HollowTwitch
{
    public interface IClient : IDisposable
    {
        event Func<string, string, (SimpleTCPClient.EffectResult, Command)> ChatMessageReceived;

        event Action<string> ClientErrored;

        void StartReceive();
    }
}
