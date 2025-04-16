using DataAccess;
using GlobalLayer;
using System;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZMQFacade;

namespace DeltaExchangeConnect
{
    // Delegates for events
    public delegate void OnConnectHandler();
    public delegate void OnCloseHandler();
    public delegate void OnErrorHandler(string Message);
    public delegate void OnDataHandler(byte[] Data, int Count, string MessageType);

    public class WebSocketClient : IWebSocket
    {
        // If set to true will print extra debug information
        private bool _debug = false;

        public event OnConnectHandler OnConnect;
        public event OnCloseHandler OnClose;
        public event OnDataHandler OnData;
        public event OnErrorHandler OnError;

        private  ClientWebSocket _webSocket = new ClientWebSocket();
        private  Uri _serverUri;
        private  string _apiKey;
        private  string _apiSecret;

        int _bufferLength = 2000000; // Length of buffer to keep binary chunk

        // A watchdog timer for monitoring the connection of ticker.
        System.Timers.Timer _timer;
        int _timerTick = 5;

        private System.Timers.Timer _healthCheckTimer;
        private int _healthCounter = 0;

        /// <summary>
        /// ZeroMQ messaging server
        /// </summary>
        public CryptoZMQServer zmqServer;//, zmqServer2;

        //private Storage storage;

        bool _storeTicks = false, _storeCandles = false;


        public WebSocketClient(string serverUrl, string apiKey, string apiSecret)
        {
            _serverUri = new Uri(serverUrl);
            _apiKey = apiKey;
            _apiSecret = apiSecret;
            
        }

        public async Task ConnectAsync(string Url)
        {
            try
            {
                await _webSocket.ConnectAsync(_serverUri, CancellationToken.None);
                Console.WriteLine("Connected to WebSocket server.");
            }
            catch (Exception ex) {
                Console.WriteLine(ex.Message);
            }
            
            await SendAuthMessageAsync();
            await SendEnableHeatBeatMessageAsync();

            zmqServer = new CryptoZMQServer();
            //if (ConfigurationManager.AppSettings["storeticks"] == "true")
            //{
            //    _storeTicks = true;
            //}
            //if (ConfigurationManager.AppSettings["storecandles"] == "true")
            //{
            //    _storeCandles = true;
            //}
            //storage = new Storage(_storeTicks, _storeCandles);
            //_storeTicks = true; _storeCandles = false;
            //storage = new Storage(true, false);

            _ = Task.Run(ReceiveMessagesAsync);
        }
        private static string GenerateSignature(string secret, string message)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }


        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[1024 * 4];
            while (_webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    Console.WriteLine("Disconnected from WebSocket server.");
                    break;
                }

                string response = Encoding.UTF8.GetString(buffer, 0, result.Count);
                //Console.WriteLine("Received message:");
                //Console.WriteLine(response);

                try
                {
                    var jsonResponse = JsonSerializer.Deserialize<JsonElement>(response);

                    if (jsonResponse.TryGetProperty("type", out var typeProp))
                    {
                        if (typeProp.GetString() == "subscribe")
                        {
                            if (jsonResponse.TryGetProperty("channels", out var channelsProp))
                            {
                                foreach (var channel in channelsProp.EnumerateArray())
                                {
                                    if (channel.TryGetProperty("error", out var errorProp))
                                    {
                                        Console.WriteLine($"Subscription error: {errorProp.GetString()}");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Successfully subscribed to {channel.GetProperty("name").GetString()}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            string channel =  typeProp.GetString();
                            zmqServer.PublishData(channel, response);


                            //if (channel == "l1_orderbook")
                            //{
                            //    await DataAccess.QuestDB.InsertObject(L1Orderbook.FromJson(response));
                            //}
                            //else if (channel == "all_trades")
                            //{
                            //    await DataAccess.QuestDB.InsertObject(AllTradesResponse.FromJson(response));
                            //}

                            //Below code is commented to improve efficiency but it can be open too
                            //if (_storeCandles)
                            //{
                            //    storage.StoreTimeCandles(ticks);
                            //}
                            //if (_storeTicks)
                            //{

                            //shortenedTick = true;
                            //storage.StoreTicks(ticks, shortenedTick);
                        }
                    }
                }
                catch (JsonException e)
                {
                    Console.WriteLine($"Error parsing response: {e.Message}");
                }
            }
        }
        /// <summary>
        /// Check if WebSocket is connected or not
        /// </summary>
        /// <returns>True if connection is live</returns>
        public bool IsConnected()
        {
            if (_webSocket is null)
                return false;

            return _webSocket.State == WebSocketState.Open;
        }
        

        private async Task SendAuthMessageAsync()
        {
            string method = "GET";
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string path = "/live";
            string signatureData = method + timestamp + path;
            string signature = GenerateSignature(_apiSecret, signatureData);

            var authMessage = new
            {
                type = "auth",
                payload = new
                {
                    api_key = _apiKey,
                    signature,
                    timestamp
                }
            };

            await SendMessageAsync(authMessage);
            
        }

        public async Task SendEnableHeatBeatMessageAsync()
        {
            var enableHBMessage = new
            {
                type = "enable_heartbeat",
                payload = new { }
            };

            await SendMessageAsync(enableHBMessage);
        }

        public async Task SendUnauthMessageAsync()
        {
            var unauthMessage = new
            {
                type = "unauth",
                payload = new { }
            };

            await SendMessageAsync(unauthMessage);
        }

        private async Task SendMessageAsync(object message)
        {
            string jsonMessage = JsonSerializer.Serialize(message);
            byte[] messageBytes = Encoding.UTF8.GetBytes(jsonMessage);
            await _webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);
            Console.WriteLine("Sent message:");
            Console.WriteLine(jsonMessage);
        }

        public async Task DisconnectAsync()
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            Console.WriteLine("Disconnected from WebSocket server.");
        }

        /// <summary>
        /// Subscribe to channel for all the symbols
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="symbols">comma seperated list</param>
        /// <returns></returns>
        public async Task Subscribe(Dictionary<string, List<string>> subscribedChannelsAndSymbols)//string channel, List<string> symbols)
        {
            //await SendSubscribeMessageAsync();

            foreach(var channelAndSymbols in subscribedChannelsAndSymbols)
            {

                //string strSymbols = "\"" + String.Join(",", channelAndSymbols.Value) + "\"";

                //string jsonString = JsonSerializer.Serialize(channelAndSymbols.Value);

                var message = new
                {
                    type = "subscribe",
                    payload = new { channels = new object[] { new { name = channelAndSymbols.Key, symbols = channelAndSymbols.Value.ToArray() } } }
                };
                await SendMessageAsync(message);
            }
        }
        /// <summary>
        /// Subscribe to channel for all the symbols
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="symbols">comma seperated list</param>
        /// <returns></returns>
        public async Task UnSubscribe(Dictionary<string, List<string>> subscribedChannelsAndSymbols)//string channel, string symbols)
        {
            foreach (var channelAndSymbols in subscribedChannelsAndSymbols)
            {
                var message = new
                {
                    type = "unsubscribe",
                    payload = new { channels = new[] { new { name = channelAndSymbols.Key, symbols = new[] { channelAndSymbols.Value.ToArray() } } } }
                };
                await SendMessageAsync(message);
            }
        }
        public async Task SendSubscribeMessageAsync()
        {
            var message = new
            {
                type = "subscribe",
                payload = new
                {
                    channels = new object[]
                    {
                    new
                    {
                        name = "v2/ticker",
                        symbols = new[] { "BTCUSD", "ETHUSD" }
                    },
                    //new
                    //{
                    //    name = "l2_orderbook",
                    //    symbols = new[] { "BTCUSD" }
                    //},
                    //new
                    //{
                    //    name = "funding_rate",
                    //    symbols = new[] { "all" }
                    //}
                    }
                }
            };

            await SendMessageAsync(message);
        }

        

        public async Task SendUnsubscribeMessageAsync()
        {
            var message = new
            {
                type = "unsubscribe",
                payload = new
                {
                    channels = new object[]
                    {
                    new
                    {
                        name = "v2/ticker",
                        symbols = new[] { "BTCUSD" }
                    },
                    new
                    {
                        name = "l2_orderbook"
                    }
                    }
                }
            };

            await SendMessageAsync(message);
        }


        //Channels

        public async Task SubscribeToL1OrderbookAsync(string symbol)
        {
            var message = new
            {
                type = "subscribe",
                payload = new { channels = new[] { new { name = "l1_orderbook", symbols = new[] { symbol } } } }
            };
            await SendMessageAsync(message);
        }


        /// <summary>
        /// all_trades channel provides a real time feed of all trades (fills). 
        /// You need to send the list of symbols for which you would like to subscribe to all trades channel. 
        /// After subscribing to this channel, you get a snapshot of last 50 trades and then trade data in real time. 
        /// You can also subscribe to all trades updates for category of products by sending category-names. 
        /// For example: to receive updates for put options and futures, refer this: {"symbols": ["put_options", "futures"]}. 
        /// If you would like to subscribe for all the listed contracts, pass: { "symbols": ["all"] }.
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public async Task SubscribeToAllTradesAsync(string symbol)
        {
            var message = new
            {
                type = "subscribe",
                payload = new { channels = new[] { new { name = "all_trades", symbols = new[] { symbol } } } }
            };
            await SendMessageAsync(message);
        }

        public async Task SubscribeToSpotPriceAsync(string symbol)
        {
            var message = new
            {
                type = "subscribe",
                payload = new { channels = new[] { new { name = "spot_price", symbols = new[] { symbol } } } }
            };
            await SendMessageAsync(message);
        }

        public async Task SubscribeToV2SpotPriceAsync(string symbol)
        {
            var message = new
            {
                type = "subscribe",
                payload = new { channels = new[] { new { name = "v2/spot_price", symbols = new[] { symbol } } } }
            };
            await SendMessageAsync(message);
        }

        public async Task SubscribeToSpot30mTwapPriceAsync(string symbol)
        {
            var message = new
            {
                type = "subscribe",
                payload = new { channels = new[] { new { name = "spot_30mtwap_price", symbols = new[] { symbol } } } }
            };
            await SendMessageAsync(message);
        }

        public async Task SubscribeToCandlestickAsync(string symbol, string resolution)
        {
            var message = new
            {
                type = "subscribe",
                payload = new { channels = new[] { new { name = $"candlestick_{resolution}", symbols = new[] { symbol } } } }
            };
            await SendMessageAsync(message);
        }

        public async Task SubscribeToPositionsAsync(string symbol)
        {
            var message = new
            {
                type = "subscribe",
                payload = new { channels = new[] { new { name = "positions", symbols = new[] { symbol } } } }
            };
            await SendMessageAsync(message);
        }

        public async Task SubscribeToOrdersAsync(string symbol)
        {
            var message = new
            {
                type = "subscribe",
                payload = new { channels = new[] { new { name = "orders", symbols = new[] { symbol } } } }
            };
            await SendMessageAsync(message);
        }

        public async Task SubscribeToUserTradesAsync(string symbol)
        {
            var message = new
            {
                type = "subscribe",
                payload = new { channels = new[] { new { name = "v2/user_trades", symbols = new[] { symbol } } } }
            };
            await SendMessageAsync(message);
        }



        /// <summary>
        /// Connect to WebSocket
        /// </summary>
        public void Connect(string Url)
        {
            _serverUri = new Uri(Url);
            try
            {
                // Initialize ClientWebSocket instance and connect with Url
                _webSocket = new ClientWebSocket();
                //if (headers != null)
                //{
                //    foreach (string key in headers.Keys)
                //    {
                //        _webSocket.Options.SetRequestHeader(key, headers[key]);
                //    }
                //}
                _webSocket.ConnectAsync(_serverUri, CancellationToken.None).Wait();

                //To begin receiving feed messages, you must first send a subscribe message to the server indicating which channels and contracts to subscribe for.




            }
            catch (AggregateException e)
            {
                OnError?.Invoke("Error while connecting. Message: " + e.InnerException.Message);
                if (e.InnerException.Message.Contains("Forbidden") && e.InnerException.Message.Contains("403"))
                {
                    OnClose?.Invoke();
                }

                //foreach (string ie in e.InnerException.Message())
                //{
                //    OnError?.Invoke("Error while connecting. Message: " + ie);
                //    if (ie.Contains("Forbidden") && ie.Contains("403"))
                //    {
                //        OnClose?.Invoke();
                //    }
                //}
                return;
            }
            catch (Exception e)
            {
                OnError?.Invoke("Error while connecting. Message:  " + e.Message);
                return;
            }
            OnConnect?.Invoke();

            byte[] buffer = new byte[_bufferLength];
            Action<Task<WebSocketReceiveResult>> callback = null;

            try
            {
                //Callback for receiving data
                callback = t =>
                {
                    try
                    {
                        byte[] tempBuff = new byte[_bufferLength];
                        int offset = t.Result.Count;
                        bool endOfMessage = t.Result.EndOfMessage;
                        // if chunk has even more data yet to recieve do that synchronously
                        while (!endOfMessage)
                        {
                            WebSocketReceiveResult result = _webSocket.ReceiveAsync(new ArraySegment<byte>(tempBuff), CancellationToken.None).Result;
                            Array.Copy(tempBuff, 0, buffer, offset, result.Count);
                            offset += result.Count;
                            endOfMessage = result.EndOfMessage;
                        }
                        // send data to process
                        OnData?.Invoke(buffer, offset, t.Result.MessageType.ToString());
                        // Again try to receive data
                        if (_webSocket != null)
                        {
                            _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).ContinueWith(callback);
                        }
                    }
                    catch (Exception e)
                    {
                        if (IsConnected())
                            OnError?.Invoke("Error while recieving data. Message:  " + e.Message);
                        else
                            OnError?.Invoke("Lost ticker connection.");
                    }
                };
                if (_webSocket != null)
                {
                    // To start the receive loop in the beginning
                    _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).ContinueWith(callback);
                }
            }
            catch (Exception e)
            {
                OnError?.Invoke("Error while recieving data. Message:  " + e.Message);
            }
        }


        private void _onData(byte[] Data, int Count, string MessageType)
        {
            if (MessageType == "Binary")
            {
                if (Count == 1)
                {
                    if (_debug) Console.WriteLine(DateTime.Now.ToLocalTime() + " Heartbeat");
                }
                else
                {
                    #region oldcode
                    //int offset = 0;
                    //ushort count = ReadShort(Data, ref offset); //number of packets
                    //if (_debug) Console.WriteLine("No of packets: " + count);
                    //if (_debug) Console.WriteLine("No of bytes: " + Count);

                    //for (ushort i = 0; i < count; i++)
                    //{
                    //    ushort length = ReadShort(Data, ref offset); // length of the packet
                    //    if (_debug) Console.WriteLine("Packet Length " + length);
                    //    Tick tick = new Tick();
                    //    if (length == 8) // ltp
                    //        tick = ReadLTP(Data, ref offset);
                    //    else if (length == 28) // index quote
                    //        tick = ReadIndexQuote(Data, ref offset);
                    //    else if (length == 32) // index quote
                    //        tick = ReadIndexFull(Data, ref offset);
                    //    else if (length == 44) // quote
                    //        tick = ReadQuote(Data, ref offset);
                    //    else if (length == 184) // full with marketdepth and timestamp
                    //        tick = ReadFull(Data, ref offset);
                    //    // If the number of bytes got from stream is less that that is required
                    //    // data is invalid. This will skip that wrong tick
                    //    if (tick.InstrumentToken != 0 && IsConnected && offset <= Count)
                    //    {
                    //        OnTick(tick);
                    //    }
                    //}

                    /*zmqServer1.PublishAllTicks(Data);*/
                    #endregion
                    //bool shortenedTick = false;
                    //List<Tick> ticks = TickDataSchema.ParseTicks(Data, shortenedTick);

                    // zmqServer.PublishAllTicks(ticks, shortenedTick);

                    //Below code is commented to improve efficiency but it can be open too
                    //if (_storeCandles)
                    //{
                    //    storage.StoreTimeCandles(ticks);
                    //}
                    //if (_storeTicks)
                    //{
                    // shortenedTick = true;
                    // storage.StoreTicks(ticks, shortenedTick);
                    //}


                    Interlocked.Increment(ref _healthCounter);
                }
            }
            else if (MessageType == "Text")
            {
                //string message = Encoding.UTF8.GetString(Data.Take(Count).ToArray());
                //if (_debug) Console.WriteLine("WebSocket Message: " + message);

                //Dictionary<string, dynamic> messageDict = Utils.JsonDeserialize(message);
                //if (messageDict["type"] == "order")
                //{
                //    OnOrderUpdate?.Invoke(new Order(messageDict["data"]));
                //}
                //else if (messageDict["type"] == "error")
                //{
                //    OnError?.Invoke(messageDict["data"]);
                //}
            }
            else if (MessageType == "Close")
            {
                // Close();
            }

        }

        /// <summary>
        /// Send message to socket connection
        /// </summary>
        /// <param name="Message">Message to send</param>
        public void Send(string Message)
        {
            if (_webSocket.State == WebSocketState.Open)
                try
                {
                    _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(Message)), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
                }
                catch (Exception e)
                {
                    OnError?.Invoke("Error while sending data. Message:  " + e.Message);
                }
        }

        /// <summary>
        /// Close the WebSocket connection
        /// </summary>
        /// <param name="Abort">If true WebSocket will not send 'Close' signal to server. Used when connection is disconnected due to netork issues.</param>
        public void Close(bool Abort = false)
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                try
                {
                    if (Abort)
                        _webSocket.Abort();
                    else
                    {
                        _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait();
                        OnClose?.Invoke();
                    }
                }
                catch (Exception e)
                {
                    OnError?.Invoke("Error while closing connection. Message: " + e.Message);
                }
            }
        }

    }

}
