#nullable enable
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using JackboxGPT.Extensions;
using JackboxGPT.Games.Common.Models;
using JackboxGPT.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Websocket.Client;

namespace JackboxGPT.Games.Common
{
    public abstract class BaseJackboxClient<TRoom, TPlayer> : IJackboxClient
    {
        private const string OP_CLIENT_WELCOME = "client/welcome";
        private const string OP_CLIENT_SEND = "client/send";
        private const string OP_UPDATE_TEXT = "text/update";
        private const string OP_UPDATE_OBJECT = "object/update";

        private const string OP_OBJECT = "object";
        private const string OP_TEXT = "text";
        
        protected abstract string KEY_ROOM { get; }
        protected abstract string KEY_PLAYER_PREFIX { get; }

        public event EventHandler<ClientWelcome>? PlayerStateChanged;
        public event EventHandler<Revision<TRoom>>? OnRoomUpdate;
        public event EventHandler<Revision<TPlayer>>? OnSelfUpdate;

        private readonly IConfigurationProvider _configuration;
        private readonly ILogger _logger;

        private readonly Guid _playerId = Guid.NewGuid();
        // ReSharper disable once InconsistentNaming
        protected GameState<TRoom, TPlayer> _gameState;

        private WebsocketClient? _webSocket;
        private ManualResetEvent? _exitEvent;
        private int _msgSeq;

        protected const int BASE_INSTANCE = 1;
        private readonly int _instance;

        public GameState<TRoom, TPlayer> GameState => _gameState;

        protected BaseJackboxClient(IConfigurationProvider configuration, ILogger logger, int instance = BASE_INSTANCE)
        {
            _configuration = configuration;
            _logger = logger;
            _instance = instance;
        }

        public void Connect()
        {
            var bootstrap = new BootstrapPayload
            {
                Role = "player",
                Name = $"{_configuration.PlayerName}-{_instance}",
                UserId = _playerId.ToString(),
                Format = "json",
                Password = ""
            };

            var url = new Uri($"wss://{_configuration.EcastHost}/api/v2/rooms/{_configuration.RoomCode.ToUpper()}/play?{bootstrap.AsQueryString()}");

            if (_instance == 0)
                _logger.Debug($"Trying to connect to ecast websocket with url: {url}");

            _webSocket = new WebsocketClient(url, () =>
            {
                var nativeClient = new ClientWebSocket();
                nativeClient.Options.AddSubProtocol("ecast-v0");
                return nativeClient;
            }) {
                MessageEncoding = Encoding.UTF8,
                IsReconnectionEnabled = false
            };

            _exitEvent = new ManualResetEvent(false);

            _webSocket.MessageReceived.Subscribe(WsReceived);
            _webSocket.ReconnectionHappened.Subscribe(WsConnected);
            _webSocket.DisconnectionHappened.Subscribe(WsDisconnected);

            _webSocket.Start();
            _exitEvent.WaitOne();
        }
        
        private void ServerMessageReceived(ServerMessage<JRaw> message)
        {
            switch(message.OpCode)
            {
                case OP_TEXT:
                    var textOp = JsonConvert.DeserializeObject<TextOperation>(message.Result.ToString());
                    HandleOperation(textOp);
                    break;
                case OP_OBJECT:
                    var objOp = JsonConvert.DeserializeObject<ObjectOperation>(message.Result.ToString());
                    HandleOperation(objOp);
                    break;
            }
        }

        protected void InvokeOnSelfUpdateEvent(object? sender, Revision<TPlayer> e)
        {
            OnSelfUpdate?.Invoke(sender, e);
        }

        protected void InvokeOnRoomUpdateEvent(object? sender, Revision<TRoom> e)
        {
            OnRoomUpdate?.Invoke(sender, e);
        }

        protected virtual void HandleOperation(IOperation op)
        {
            if (op.Key == $"{KEY_PLAYER_PREFIX}{_playerId}" || op.Key == $"{KEY_PLAYER_PREFIX}{_gameState.PlayerId}")
            {
                var self = JsonConvert.DeserializeObject<TPlayer>(op.Value);
                if (self == null) return;

                InvokeOnSelfUpdateEvent(this, new Revision<TPlayer>(_gameState.Self, self));
                _gameState.Self = self;
            }
            else if (op.Key == KEY_ROOM)
            {
                var room = JsonConvert.DeserializeObject<TRoom>(op.Value);
                if (room == null) return;

                InvokeOnRoomUpdateEvent(this, new Revision<TRoom>(_gameState.Room, room));
                _gameState.Room = room;
            }
        }

        private void WsReceived(ResponseMessage msg)
        {
            if (msg.Text == null) return;

            var srvMsg = JsonConvert.DeserializeObject<ServerMessage<JRaw>>(msg.Text);
            
            if (srvMsg.OpCode == OP_CLIENT_WELCOME)
            {
                var cw = JsonConvert.DeserializeObject<ClientWelcome>(srvMsg.Result.ToString());
                HandleClientWelcome(cw);
                PlayerStateChanged?.Invoke(this, cw);
            }
            ServerMessageReceived(srvMsg);
        }

        private void WsConnected(ReconnectionInfo inf)
        {
            if (_instance == 0)
                _logger.Information($"Connected to Jackbox games services.");
        }

        private void WsDisconnected(DisconnectionInfo inf)
        {
            _logger.Information($"Client{_instance} disconnected from Jackbox games services.");
            _exitEvent?.Set();
        }

        private void HandleClientWelcome(ClientWelcome cw)
        {
            _gameState.PlayerId = cw.Id;
            _logger.Debug($"Client{_instance} welcome message received. Player ID: {_gameState.PlayerId}");
        }

        protected void WsSend<T>(string opCode, T body)
        {
            _msgSeq++;

            var clientMessage = new ClientMessageOperation<T>
            {
                Seq = _msgSeq,
                OpCode = opCode,
                Params = body
            };

            var msg = JsonConvert.SerializeObject(clientMessage);
            _webSocket?.Send(msg);
        }
        
        protected void ClientSend<T>(T req)
        {
            var cs = new ClientSendOperation<T>
            {
                From = _gameState.PlayerId,
                To = 1,
                Body = req
            };

            WsSend(OP_CLIENT_SEND, cs);
        }

        protected void ClientUpdate<T>(T req, string updateKey)
        {
            var cs = new ClientUpdateOperation<T>
            {
                Key = $"{updateKey}:{_gameState.PlayerId}",
                Value = req
            };

            var op = typeof(T) == typeof(string) ? OP_UPDATE_TEXT : OP_UPDATE_OBJECT;
            WsSend(op, cs);
        }

        public int GetPlayerId()
        {
            return _gameState.PlayerId;
        }
    }
}
