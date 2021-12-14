using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HollowTwitch;
using HollowTwitch.Extensions;
using Newtonsoft.Json;

namespace CrowdControl
{
    public class SimpleTCPClient : IDisposable
    {
        private TcpClient _client;
        private readonly SemaphoreSlim _client_lock = new SemaphoreSlim(1);
        private readonly ManualResetEventSlim _ready = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _error = new ManualResetEventSlim(false);

        private static readonly JsonSerializerSettings JSON_SETTINGS = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        public bool Connected { get; private set; }

        private readonly CancellationTokenSource _quitting = new CancellationTokenSource();

        public SimpleTCPClient()
        {
            Task.Factory.StartNew(ConnectLoop, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(Listen, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(KeepAlive, TaskCreationOptions.LongRunning);
        }

        ~SimpleTCPClient() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            _quitting.Cancel();
            if (disposing) { _client.Close(); }
        }

        private async void ConnectLoop()
        {
            while (!_quitting.IsCancellationRequested)
            {
                try
                {
                    _client = new TcpClient();
                    await _client.ConnectAsync("127.0.0.1", 58430);
                    if (!_client.Connected) { continue; }
                    Connected = true;
                    try { OnConnected?.Invoke(); }
                    catch (Exception e) { Logger.LogError(e); }
                    _ready.Set();
                    await _error.WaitHandle.WaitOneAsync(_quitting.Token);
                }
                catch (Exception e) { Logger.LogError(e); }
                finally
                {
                    Connected = false;
                    _error.Reset();
                    _ready.Reset();
                    try { _client.Close(); }
                    catch { /**/ }
                    if (!_quitting.IsCancellationRequested) { await Task.Delay(TimeSpan.FromSeconds(1)); }
                }
            }
            Connected = false;
        }

        private async void Listen()
        {
            List<byte> mBytes = new List<byte>();
            byte[] buf = new byte[4096];
            while (!_quitting.IsCancellationRequested)
            {
                try
                {
                    if (!(await _ready.WaitHandle.WaitOneAsync(_quitting.Token))) { continue; }
                    Socket socket = _client.Client;

                    int bytesRead = socket.Receive(buf);
                    //Log.Debug($"Got {bytesRead} bytes from socket.");

                    //this is "slow" but the messages are tiny so we don't really care
                    foreach (byte b in buf.Take(bytesRead))
                    {
                        if (b != 0) { mBytes.Add(b); }
                        else
                        {
                            //Log.Debug($"Got a complete message: {mBytes.ToArray().ToHexadecimalString()}");
                            string json = Encoding.UTF8.GetString(mBytes.ToArray());
                            //Log.Debug($"Got a complete message: {json}");
                            Request req = JsonConvert.DeserializeObject<Request>(json, JSON_SETTINGS);
                            //Log.Debug($"Got a request with ID {req.id}.");
                            try { OnRequestReceived?.Invoke(req); }
                            catch (Exception e) { Logger.LogError(e); }
                            mBytes.Clear();
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                    _error.Set();
                }
                finally { if (!_quitting.IsCancellationRequested) { await Task.Delay(TimeSpan.FromSeconds(1)); } }
            }
        }

        private async void KeepAlive()
        {
            while (!_quitting.IsCancellationRequested)
            {
                try
                {
                    if (Connected) { await Respond(new Response { id = 0, type = Response.ResponseType.KeepAlive }); }
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                    _error.Set();
                }
                finally { if (!_quitting.IsCancellationRequested) { await Task.Delay(TimeSpan.FromSeconds(1)); } }
            }
        }

        public event Action<Request> OnRequestReceived;
        public event Action OnConnected;

        public async Task<bool> Respond(Response response)
        {
            string json = JsonConvert.SerializeObject(response, JSON_SETTINGS);
            byte[] buffer = Encoding.UTF8.GetBytes(json + '\0');
            Socket socket = _client.Client;
            await _client_lock.WaitAsync();
            try
            {
                int bytesSent = socket.Send(buffer);
                return bytesSent > 0;
            }
            catch (Exception e)
            {
                Logger.LogError(e);
                return false;
            }
            finally { _client_lock.Release(); }
        }

        [Serializable]
        public class Request
        {
            public uint id;
            public string? code;
            public string? message;
            public object?[] parameters;
            public Target?[] targets;
            public string? viewer;
            public int? cost;
            public RequestType type;

            public enum RequestType : byte
            {
                Test = 0x00,
                Start = 0x01,
                Stop = 0x02,

                Login = 0xF0,
                KeepAlive = 0xFF
            }

            [Serializable]
            public class Target
            {
                public string id;
                public string name;
                public string avatar;
            }
        }

        [Serializable]
        public class Response
        {
            public uint id;
            public EffectResult status;
            public string? message;
            public long timeRemaining; //this is milliseconds
            public ResponseType type = ResponseType.EffectRequest;

            public enum ResponseType : byte
            {
                EffectRequest = 0x00,

                Login = 0xF0,
                KeepAlive = 0xFF
            }
        }

        public enum EffectResult
        {
            /// <summary>The effect executed successfully.</summary>
            Success = 0x00,
            /// <summary>The effect failed to trigger, but is still available for use. Viewer(s) will be refunded.</summary>
            Failure = 0x01,
            /// <summary>Same as <see cref="Failure"/> but the effect is no longer available for use.</summary>
            Unavailable = 0x02,
            /// <summary>The effect cannot be triggered right now, try again in a few seconds.</summary>
            Retry = 0x03,
            /// <summary>INTERNAL USE ONLY. The effect has been queued for execution after the current one ends.</summary>
            Queue = 0x04,
            /// <summary>INTERNAL USE ONLY. The effect triggered successfully and is now active until it ends.</summary>
            Running = 0x05,
            /// <summary>The timed effect has been paused and is now waiting.</summary>
            Paused = 0x06,
            /// <summary>The timed effect has been resumed and is counting down again.</summary>
            Resumed = 0x07,
            /// <summary>The timed effect has finished.</summary>
            Finished = 0x08,
            /// <summary>The processor isn't ready.</summary>
            NotReady = 0xFF
        }
    }
}
