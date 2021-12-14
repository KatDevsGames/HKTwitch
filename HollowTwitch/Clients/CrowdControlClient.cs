using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CrowdControl;
using HollowTwitch.Entities;
using Newtonsoft.Json;

namespace HollowTwitch.Clients
{
    /// <summary>
    /// This is just for local testing
    /// You should make a server on your Machine and start it.
    /// The client will try to connect to your local Server and receive messages.
    /// </summary>
    public class CrowdControlClient : IClient
    {
        public event Func<string, string, (SimpleTCPClient.EffectResult, Command)> ChatMessageReceived;
        public event Action<string> ClientErrored;

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
            catch {/**/}
            _client = new SimpleTCPClient();
            _client.OnRequestReceived += HandleRequest;

            Logger.Log("Connecting...");
        }

        private void HandleRequest(SimpleTCPClient.Request request)
        {
            try
            {
                if (request == null)
                {
                    Logger.LogError("HandleRequest got a null request.");
                    return;
                }
                switch (request.type)
                {
                    case SimpleTCPClient.Request.RequestType.Start:
                        (SimpleTCPClient.EffectResult result, Command command) result = ((SimpleTCPClient.EffectResult, Command))ChatMessageReceived?.Invoke(request.viewer, string.Join(" ", (request.parameters?.Select(p => p.ToString()) ?? Array.Empty<string>()).Prepend('!' + request.code.Replace('_', ' ')).ToArray()));

                        SimpleTCPClient.Response response = new SimpleTCPClient.Response
                        {
                            id = request.id,
                            status = result.result,
                            timeRemaining = ((long?)result.command?.Cooldown?.TotalMilliseconds) ?? 0L
                        };
                        _client?.Respond(response);
                        return;
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
