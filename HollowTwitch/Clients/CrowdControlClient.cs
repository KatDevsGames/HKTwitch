using System;
using System.Collections.Generic;
using System.Linq;
using ConnectorLib.JSON;
using CrowdControl;
using HollowTwitch.Entities;

namespace HollowTwitch.Clients
{
    /// <summary>
    /// This is just for local testing
    /// You should make a server on your Machine and start it.
    /// The client will try to connect to your local Server and receive messages.
    /// </summary>
    public class CrowdControlClient : IClient
    {
        public event Func<string, string, long?, (EffectStatus, Command)> ChatMessageReceived;
        public event Action<string> ClientErrored;
        public event Func<GameUpdate> GameStateRequested;
        public event Func<IEnumerable<EffectResponseMetadata>> MetadataRequested;

        private SimpleTCPClient _client;

        public CrowdControlClient(Config config)
        {

        }

        public void Dispose()
        {
            try { _client?.Dispose(); }
            catch {/**/}
        }

        public void StartReceive()
        {
            Connect("127.0.0.1", 58430);
        }

        private void Connect(string host, int port)
        {
            try { _client?.Dispose(); }
            catch { /**/ }
            _client = new SimpleTCPClient();
            _client.OnRequestReceived += HandleRequest;

            Logger.Log("Connecting...");
        }

        private void HandleRequest(SimpleJSONRequest request)
        {
            try
            {
                if (request == null)
                {
                    Logger.LogError("HandleRequest got a null request.");
                    return;
                }

                switch (request)
                {
                    case EffectRequest req:
                    {
                        string command = string.Join(" ",
                            (req.parameters?.Select(p => p.ToString()) ?? Array.Empty<string>())
                            .Prepend('!' + req.code.Replace('_', ' ')).ToArray());

                        (EffectStatus result, Command command) result = ((EffectStatus, Command))ChatMessageReceived?.Invoke(req.viewer, command, req.duration);

                        EffectResponse response = new()
                        {
                            id = request.id,
                            status = result.result,
                            timeRemaining = ((long?)result.command?.Cooldown?.TotalMilliseconds) ?? 0L,
                            metadata = MetadataRequested?.Invoke().ToDictionary(m => m.key)
                        };
                        _client?.Respond(response);
                        return;
                    }
                    case not null when request.type == RequestType.GameUpdate:
                    {
                        GameUpdate result = GameStateRequested?.Invoke();
                        _client?.Respond(result);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                try { Logger.LogError(e); }
                catch {/*oh well, I guess*/}
            }
        }
    }
}
