using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
        public event Func<string, string, (EffectResult, Command)> ChatMessageReceived;
        public event Action<string> ClientErrored;

        private TcpClient _client;
        private byte[] receiveBuf;
        private NetworkStream stream;

        private readonly List<byte> messageBuffer = new List<byte>(4096);

        public CrowdControlClient(Config config)
        {

        }

        public void Dispose()
        {
            try { stream.Dispose(); }
            catch {/**/}
            try { _client.Close(); }
            catch {/**/}
        }

        public void StartReceive()
        {
            Connect("127.0.0.1", 58430);
        }

        private void Connect(string host, int port)
        {
            _client = new TcpClient
            {
                ReceiveBufferSize = 4096,
                SendBufferSize = 4096,
            };
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            receiveBuf = new byte[4096];

            Logger.Log("Connecting...");

            _client.BeginConnect(host, port, ConnectCallback, _client);
        }

        private void ConnectCallback(IAsyncResult result)
        {
            try { _client.EndConnect(result); }
            catch (Exception e) { Logger.LogError(e); }

            if (!_client.Connected)
            {
                Logger.LogError("Connection failed.");
                return;
            }

            Logger.Log("Connection Successful. Waiting for messages.");

            stream = _client.GetStream();

            stream.BeginRead(receiveBuf, 0, 4096, RecvCallback, null);
        }

        private void RecvCallback(IAsyncResult result)
        {
            try
            {
                int byte_len = stream.EndRead(result);
                Logger.Log($"Got {byte_len} bytes...");

                if (byte_len <= 0)
                {
                    Logger.LogError("Received length < 0!");

                    ClientErrored?.Invoke("Invalid length!");

                    return;
                }

                //this is "slow" but the commands are tiny are rarely don't line up, don't care
                messageBuffer.AddRange(receiveBuf.Take(byte_len));

                int end = messageBuffer.IndexOf(0);
                if (end >= 0)
                {
                    Logger.Log($"Found EOM at position {end}...");
                    string json = Encoding.UTF8.GetString(messageBuffer.Take(end).ToArray());
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        Logger.Log("RecvCallback trimmed an empty message...");
                        return;
                    }
                    Request req = JsonConvert.DeserializeObject<Request>(json);
                    HandleRequest(req); //guaranteed won't throw
                    messageBuffer.RemoveRange(0, end + 1);
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }

            stream.BeginRead(receiveBuf, 0, 4096, RecvCallback, null);
        }

        private void HandleRequest(Request request)
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
                    case RequestType.Start:
                        (EffectResult result, Command command) result = ((EffectResult, Command))ChatMessageReceived?.Invoke(request.viewer, string.Join(" ", (request.parameters?.Select(p => p.ToString()) ?? Array.Empty<string>()).Prepend('!' + request.code.Replace('_', ' ')).ToArray()));

                        Response response = new Response
                        {
                            id = request.id,
                            status = result.result,
                            timeRemaining = ((long?)result.command?.Cooldown?.TotalMilliseconds) ?? 0L
                        };
                        byte[] b = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));
                        stream.Write(b, 0, b.Length);
                        stream.WriteByte(0);
                        return;
                }
            }
            catch (Exception e)
            {
                try { Logger.LogError(e); }
                catch {/*oh well, I guess*/}
            }
        }

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        public enum RequestType
        {
            Test = 0,
            Start = 1,
            Stop = 2
        }

        [Serializable]
        [SuppressMessage("ReSharper", "NotAccessedField.Local")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "UnassignedField.Local")]
        public class Request
        {
            private static int _next_id = 0;
            public uint id;
            public string code;
            public object[] parameters;
            public string viewer;
            public int? cost;
            public RequestType type;

            public Request()
            {
                // ReSharper disable once EmptyEmbeddedStatement
                while ((id = unchecked((uint)Interlocked.Increment(ref _next_id))) == 0) ;
            }
        }

        [Serializable]
        [SuppressMessage("ReSharper", "NotAccessedField.Local")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "UnassignedField.Local")]
        public class Response
        {
            public uint id;
            public EffectResult status;
            public string message;
            public long timeRemaining; //this is milliseconds
            public ResponseType type = ResponseType.EffectRequest;

            public enum ResponseType : byte
            {
                EffectRequest = 0x00,
                KeepAlive = 0xFF
            }
        }

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public enum EffectResult
        {
            /// <summary>The effect executed successfully.</summary>
            Success = 0,
            /// <summary>The effect failed to trigger, but is still available for use. Viewer(s) will be refunded.</summary>
            Failure = 1,
            /// <summary>Same as <see cref="Failure"/> but the effect is no longer available for use.</summary>
            Unavailable = 2,
            /// <summary>The effect cannot be triggered right now, try again in a few seconds.</summary>
            Retry = 3,
            /// <summary>INTERNAL USE ONLY. The effect has been queued for execution after the current one ends.</summary>
            Queue = 4,
            /// <summary>INTERNAL USE ONLY. The effect triggered successfully and is now active until it ends.</summary>
            Running = 5,
            /// <summary>The timed effect has been paused and is now waiting.</summary>
            Paused = 6,
            /// <summary>The timed effect has been resumed and is counting down again.</summary>
            Resumed = 7,
            /// <summary>The timed effect has finished.</summary>
            Finished = 8
        }
    }
}
