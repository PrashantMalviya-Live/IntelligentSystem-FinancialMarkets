using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Configuration;
using GlobalLayer;

using DataAccess;
using System.Runtime.CompilerServices;
//using DataAccess;
//using ZMQFacade;
//using GlobalCore;
namespace DeltaExchangeConnect
{
    /// <summary>
    /// The WebSocket client for connecting to Kite Connect's streaming quotes service.
    /// </summary>
    public class DeltaExchangeTicker
    {
        // If set to true will print extra debug information
        private bool _debug = false;


        //Production - wss://socket.india.delta.exchange
        //Testnet(Demo Account) - wss://socket-ind.testnet.deltaex.org

        // Root domain for ticker. Can be changed with Root parameter in the constructor.
        private string _root = "wss://socket.india.delta.exchange";
        //Testnet(Demo Account) - wss://socket-ind.testnet.deltaex.org
        // Configurations to create ticker connection
        private string _apiKey;
        private string _apiSecret;
        private string _accessToken;
        private string _socketUrl = "";
        private bool _isReconnect = false;
        private int _interval = 5;
        private int _retries = 50;
        private int _retryCount = 0;

        // A watchdog timer for monitoring the connection of ticker.
        System.Timers.Timer _timer;
        int _timerTick = 5;

        private System.Timers.Timer _healthCheckTimer;
        private int _healthCounter = 0;


        // Instance of WebSocket class that wraps .Net version
        private IWebSocket _ws;

        // Dictionary that keeps instrument_token -> mode data
        //Channel & symbols in the channel
        private Dictionary<string, List<string>> _subscribedChannelsAndSymbols;

        public Dictionary<string, List<string>> SubscribedChannelsAndSymbols
        {
            get { return _subscribedChannelsAndSymbols; }
            set { _subscribedChannelsAndSymbols = value; }
        }

        // Delegates for callbacks

        /// <summary>
        /// Delegate for OnConnect event
        /// </summary>
        public delegate void OnConnectHandler();

        /// <summary>
        /// Delegate for OnClose event
        /// </summary>
        public delegate void OnCloseHandler();

        /// <summary>
        /// Delegate for OnTick event
        /// </summary>
        /// <param name="TickData">Tick data</param>
        /// <param name="State">Tick data</param>
        public delegate void OnTickHandler(Tick[] TickData, Object State);

        /// <summary>
        /// Delegate for OnOrderUpdate event
        /// </summary>
        /// <param name="OrderData">Order data</param>
        public delegate void OnOrderUpdateHandler(Order OrderData);

        /// <summary>
        /// Delegate for OnError event
        /// </summary>
        /// <param name="Message">Error message</param>
        public delegate void OnErrorHandler(string Message);

        /// <summary>
        /// Delegate for OnReconnect event
        /// </summary>
        public delegate void OnReconnectHandler();

        /// <summary>
        /// Delegate for OnNoReconnect event
        /// </summary>
        public delegate void OnNoReconnectHandler();

        // Events that can be subscribed
        /// <summary>
        /// Event triggered when ticker is connected
        /// </summary>
        public event OnConnectHandler OnConnect;

        /// <summary>
        /// Event triggered when ticker is disconnected
        /// </summary>
        public event OnCloseHandler OnClose;

        /// <summary>
        /// Event triggered when ticker receives a tick
        /// </summary>
        public event OnTickHandler OnTick;

        /// <summary>
        /// Event triggered when ticker receives an order update
        /// </summary>
        public event OnOrderUpdateHandler OnOrderUpdate;

        /// <summary>
        /// Event triggered when ticker encounters an error
        /// </summary>
        public event OnErrorHandler OnError;

        /// <summary>
        /// Event triggered when ticker is reconnected
        /// </summary>
        public event OnReconnectHandler OnReconnect;

        /// <summary>
        /// Event triggered when ticker is not reconnecting after failure
        /// </summary>
        public event OnNoReconnectHandler OnNoReconnect;

        

        /// <summary>
        /// Initialize websocket client instance.
        /// </summary>
        /// <param name="APIKey">API key issued to you</param>
        /// <param name="UserID">Zerodha client id of the authenticated user</param>
        /// <param name="AccessToken">Token obtained after the login flow in 
        /// exchange for the `request_token`.Pre-login, this will default to None,
        /// but once you have obtained it, you should
        /// persist it in a database or session to pass
        /// to the Kite Connect class initialisation for subsequent requests.</param>
        /// <param name="Root">Websocket API end point root. Unless you explicitly 
        /// want to send API requests to a non-default endpoint, this can be ignored.</param>
        /// <param name="Reconnect">Enables WebSocket autreconnect in case of network failure/disconnection.</param>
        /// <param name="ReconnectInterval">Interval (in seconds) between auto reconnection attemptes. Defaults to 5 seconds.</param>
        /// <param name="ReconnectTries">Maximum number reconnection attempts. Defaults to 50 attempts.</param>
        public DeltaExchangeTicker(string APIKey, string APISecret, string Root = null, 
            bool Reconnect = false, int ReconnectInterval = 5, int ReconnectTries = 30005, bool Debug = false, IWebSocket CustomWebSocket = null)
        {
            _debug = Debug;
            _apiKey = APIKey;
            //_accessToken = AccessToken;
            _apiSecret = APISecret;

            _subscribedChannelsAndSymbols = new Dictionary<string, List<string>>();
            _interval = ReconnectInterval;
            _timerTick = ReconnectInterval;
            _retries = ReconnectTries;

            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();

            if (!String.IsNullOrEmpty(Root))
                _root = Root;
            _isReconnect = Reconnect;
            _socketUrl = _root;// + String.Format("?api_key={0}&access_token={1}", _apiKey, _accessToken);

            //initialize websocket
            if (CustomWebSocket != null)
            {
                _ws = CustomWebSocket;
            }
            else
            {
                _ws = new WebSocketClient(_root, _apiKey, _apiSecret);
            }

            _ws.OnConnect += _onConnect;
           // _ws.OnData += _onData;
            _ws.OnClose += _onClose;
            _ws.OnError += _onError;

            // initializing  watchdog timer
            _timer = new System.Timers.Timer();
            _timer.Elapsed += _onTimerTick;
            _timer.Interval = 10000; // checks connection every second

            



            
        }

        private void _onError(string Message)
        {
            // pipe the error message from ticker to the events
            OnError?.Invoke(Message);
        }

        private void _onClose()
        {
            // stop the timer while normally closing the connection
            _timer.Stop();
            OnClose?.Invoke();
        }

     

        //private void _onData(byte[] Data, int Count, string MessageType)
        //{
        //    _timerTick = _interval;
        //    if (MessageType == "Binary")
        //    {
        //        if (Count == 1)
        //        {
        //            if (_debug) Console.WriteLine(DateTime.Now.ToLocalTime() + " Heartbeat");
        //        }
        //        else
        //        {
        //            #region oldcode
        //            //int offset = 0;
        //            //ushort count = ReadShort(Data, ref offset); //number of packets
        //            //if (_debug) Console.WriteLine("No of packets: " + count);
        //            //if (_debug) Console.WriteLine("No of bytes: " + Count);

        //            //for (ushort i = 0; i < count; i++)
        //            //{
        //            //    ushort length = ReadShort(Data, ref offset); // length of the packet
        //            //    if (_debug) Console.WriteLine("Packet Length " + length);
        //            //    Tick tick = new Tick();
        //            //    if (length == 8) // ltp
        //            //        tick = ReadLTP(Data, ref offset);
        //            //    else if (length == 28) // index quote
        //            //        tick = ReadIndexQuote(Data, ref offset);
        //            //    else if (length == 32) // index quote
        //            //        tick = ReadIndexFull(Data, ref offset);
        //            //    else if (length == 44) // quote
        //            //        tick = ReadQuote(Data, ref offset);
        //            //    else if (length == 184) // full with marketdepth and timestamp
        //            //        tick = ReadFull(Data, ref offset);
        //            //    // If the number of bytes got from stream is less that that is required
        //            //    // data is invalid. This will skip that wrong tick
        //            //    if (tick.InstrumentToken != 0 && IsConnected && offset <= Count)
        //            //    {
        //            //        OnTick(tick);
        //            //    }
        //            //}

        //            /*zmqServer1.PublishAllTicks(Data);*/
        //            #endregion
        //            bool shortenedTick = false;
        //            List<Tick> ticks = TickDataSchema.ParseTicks(Data, shortenedTick);

        //            zmqServer.PublishAllTicks(ticks, shortenedTick);

        //            //Below code is commented to improve efficiency but it can be open too
        //            //if (_storeCandles)
        //            //{
        //            //    storage.StoreTimeCandles(ticks);
        //            //}
        //            //if (_storeTicks)
        //            //{
        //            shortenedTick = true;
        //            storage.StoreTicks(ticks, shortenedTick);
        //            //}


        //            Interlocked.Increment(ref _healthCounter);
        //        }
        //    }
        //    else if (MessageType == "Text")
        //    {
        //        string message = Encoding.UTF8.GetString(Data.Take(Count).ToArray());
        //        if (_debug) Console.WriteLine("WebSocket Message: " + message);

        //        Dictionary<string, dynamic> messageDict = Utils.JsonDeserialize(message);
        //        if(messageDict["type"] == "order")
        //        {
        //            OnOrderUpdate?.Invoke(new Order(messageDict["data"]));
        //        } else if (messageDict["type"] == "error")
        //        {
        //            OnError?.Invoke(messageDict["data"]);
        //        }
        //    }
        //    else if (MessageType == "Close")
        //    {
        //        Close();
        //    }

        //}

        private void CheckHealth(object sender, System.Timers.ElapsedEventArgs e)
        {
            //expecting atleast 30 ticks in 1 min
            if (_healthCounter >= 30)
            {
                _healthCounter = 0;

            //    LoggerCore.PublishLog(Constants.MARKET_DATA_SERVICE_INSTANCE, AlgoIndex.KiteConnect, LogLevel.Health, e.SignalTime, "1", "CheckHealth");
            }
            else
            {
             //   LoggerCore.PublishLog(Constants.MARKET_DATA_SERVICE_INSTANCE, AlgoIndex.KiteConnect, LogLevel.Health, e.SignalTime, "0", "CheckHealth");
            }
        }

        private void _onTimerTick(object sender, System.Timers.ElapsedEventArgs e)
        {
            // For each timer tick count is reduced. If count goes below 0 then reconnection is triggered.
            _timerTick--;
            if (_timerTick < 0)
            {
                _timer.Stop();
                if (_isReconnect && !IsConnected)
                    Reconnect();
            }
            if (_debug) Console.WriteLine(_timerTick);
        }

        private void _onConnect()
        {
            // Reset timer and retry counts and resubscribe to tokens.
            _retryCount = 0;
            _timerTick = _interval;
            _timer.Start();
            if (_subscribedChannelsAndSymbols.Count > 0)
                ReSubscribe();
            OnConnect?.Invoke();
        }

        /// <summary>
        /// Tells whether ticker is connected to server not.
        /// </summary>
        public bool IsConnected
        {
            get { return _ws.IsConnected(); }
        }

        /// <summary>
        /// Start a WebSocket connection
        /// </summary>
        public async Task  Connect()
        {
            _timerTick = _interval;
            _timer.Start();
            if (!IsConnected)
            {
                await _ws.ConnectAsync(_socketUrl);//, new Dictionary<string, string>() { ["X-Kite-Version"] = "3" });
                 
            }
        }

        /// <summary>
        /// Close a WebSocket connection
        /// </summary>
        public void Close()
        {
            _timer.Stop();
            _ws.Close();
        }

        /// <summary>
        /// Reconnect WebSocket connection in case of failures
        /// </summary>
        private void Reconnect()
        {
            if (IsConnected)
                _ws.Close(true);

            if (_retryCount > _retries)
            {
                _ws.Close(true);
                DisableReconnect();
                OnNoReconnect?.Invoke();
            }
            else
            {
                OnReconnect?.Invoke();
                _retryCount += 1;
                _ws.Close(true);
                Connect();
                _timerTick = (int)Math.Min(Math.Pow(2, _retryCount) * _interval, 60);
                if (_debug) Console.WriteLine("New interval " + _timerTick);
                _timer.Start();
            }
        }

        /// <summary>
        /// Subscribe to a list of instrument_tokens.
        /// </summary>
        /// <param name="Symbols">List of instrument instrument_tokens to subscribe</param>
        public void Subscribe(Dictionary<string, List<string>> subscribedChannelsAndSymbols)//string channel, List<string> symbols)
        {
            // if (symbols.Count == 0) return;

            if (IsConnected)
                _ws.Subscribe(subscribedChannelsAndSymbols);// channel, symbols);


            ////string msg = "{\"a\":\"subscribe\",\"v\":[" + String.Join(",", symbols) + "]}";
            ////if (_debug) Console.WriteLine(msg.Length);

            //var message = new
            //{
            //    type = "subscribe",
            //    payload = new
            //    {
            //        channels = new object[]
            //        {
            //        new
            //        {
            //            name = "v2/ticker",
            //            symbols = new[] { "BTCUSD", "ETHUSD" }
            //        },
            //        //new
            //        //{
            //        //    name = "l2_orderbook",
            //        //    symbols = new[] { "BTCUSD" }
            //        //},
            //        //new
            //        //{
            //        //    name = "funding_rate",
            //        //    symbols = new[] { "all" }
            //        //}
            //        }
            //    }
            //};
            //string jsonMessage = System.Text.Json.JsonSerializer.Serialize(message);

            //if (IsConnected)
            //    _ws.Send(jsonMessage);

            ////    _ws.Send(msg);
            ////foreach (string token in symbols)
            ////    if (!_subscribedTokens.ContainsKey(channelname))
            ////        _subscribedTokens.Add(channelname, symbols);
        }

        /// <summary>
        /// Unsubscribe the given list of instrument_tokens.
        /// </summary>
        /// <param name="Tokens">List of instrument instrument_tokens to unsubscribe</param>
        public void UnSubscribe(Dictionary<string, List<string>> channelsAndSymbols)
        {
            _ws.UnSubscribe(channelsAndSymbols);// channel, symbols);

            //if (Tokens.Length == 0) return;

            //string msg = "{\"a\":\"unsubscribe\",\"v\":[" + String.Join(",", Tokens) + "]}";
            //if (_debug) Console.WriteLine(msg);

            //if (IsConnected)
            //    _ws.Send(msg);
            //foreach (UInt32 token in Tokens)
            //    if (_subscribedTokens.ContainsKey(token))
            //        _subscribedTokens.Remove(token);
        }

        /// <summary>
        /// Set streaming mode for the given list of tokens.
        /// </summary>
        /// <param name="Tokens">List of instrument tokens on which the mode should be applied</param>
        /// <param name="Mode">Mode to set. It can be one of the following: ltp, quote, full.</param>
        public void SetMode(UInt32[] Tokens, string Mode)
        {
            //if (Tokens.Length == 0) return;

            //string msg = "{\"a\":\"mode\",\"v\":[\"" + Mode + "\", [" + String.Join(",", Tokens) + "]]}";
            //if (_debug) Console.WriteLine(msg);

            //if (IsConnected)
            //    _ws.Send(msg);
            //foreach (UInt32 token in Tokens)
            //    if (_subscribedTokens.ContainsKey(token))
            //        _subscribedTokens[token] = Mode;
        }

        /// <summary>
        /// Resubscribe to all currently subscribed tokens. Used to restore all the subscribed tokens after successful reconnection.
        /// </summary>
        public void ReSubscribe()
        {
            if (_debug) Console.WriteLine("Resubscribing");
            //UInt32[] all_tokens = _subscribedTokens.Keys.ToArray();

            //UInt32[] ltp_tokens = all_tokens.Where(key => _subscribedTokens[key] == "ltp").ToArray();
            //UInt32[] quote_tokens = all_tokens.Where(key => _subscribedTokens[key] == "quote").ToArray();
            //UInt32[] full_tokens = all_tokens.Where(key => _subscribedTokens[key] == "full").ToArray();

            UnSubscribe(_subscribedChannelsAndSymbols);
            Subscribe(_subscribedChannelsAndSymbols);

            //SetMode(ltp_tokens, "ltp");
            //SetMode(quote_tokens, "quote");
            //SetMode(full_tokens, "full");
        }

        /// <summary>
        /// Enable WebSocket autreconnect in case of network failure/disconnection.
        /// </summary>
        /// <param name="Interval">Interval between auto reconnection attemptes. `onReconnect` callback is triggered when reconnection is attempted.</param>
        /// <param name="Retries">Maximum number reconnection attempts. Defaults to 50 attempts. `onNoReconnect` callback is triggered when number of retries exceeds this value.</param>
        public void EnableReconnect(int Interval = 1, int Retries = 500000)
        {
            _isReconnect = true;
            _interval = Math.Max(Interval, 5);
            _retries = Retries;

            _timerTick = _interval;
            if (IsConnected)
                _timer.Start();
        }

        /// <summary>
        /// Disable WebSocket autreconnect.
        /// </summary>
        public void DisableReconnect()
        {
            _isReconnect = false;
            if (IsConnected)
                _timer.Stop();
            _timerTick = _interval;
        }
    }
}
