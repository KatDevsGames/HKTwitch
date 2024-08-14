using System;
using System.Collections.Generic;
using ConnectorLib.JSON;
using HollowTwitch.Entities;

namespace HollowTwitch
{
    public interface IClient : IDisposable
    {
        event Func<string, string, long?, (EffectStatus, Command)> ChatMessageReceived;

        event Action<string> ClientErrored;

        event Func<GameUpdate> GameStateRequested;
        event Func<IEnumerable<EffectResponseMetadata>> MetadataRequested;

        void StartReceive();
    }
}
