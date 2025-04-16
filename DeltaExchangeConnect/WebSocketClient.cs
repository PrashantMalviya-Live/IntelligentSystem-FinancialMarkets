using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DeltaExchangeConnect
{

    public class WebSocketClientTest
    {
        private static async Task ConnectWebSocketAsync(string url)
        {
            // Create a new WebSocket instance
            using (ClientWebSocket ws = new ClientWebSocket())
            {
                // Connect to the WebSocket server
                Uri serverUri = new Uri(url);
                await ws.ConnectAsync(serverUri, CancellationToken.None);
                Console.WriteLine("Connected to WebSocket server");

                // Send the subscribe message to the server
                string subscribeMessage = JsonConvert.SerializeObject(new
                {
                    type = "subscribe",
                    payload = new
                    {
                        channels = new[]
                        {
                        new
                        {
                            name = "v2/ticker",
                            symbols = new[] { "BTCUSD", "ETHUSD" }
                        },
                        new
                        {
                            name = "l2_orderbook",
                            symbols = new[] { "BTCUSD" }
                        },
                        new
                        {
                            name = "funding_rate",
                            symbols = new[] { "all" }
                        }
                    }
                    }
                });

                await SendMessageAsync(ws, subscribeMessage);
                Console.WriteLine("Subscribe message sent");

                // Listen for messages from the WebSocket server
                await ReceiveMessagesAsync(ws);
            }
        }

        private static async Task SendMessageAsync(ClientWebSocket ws, string message)
        {
            // Convert message to byte array
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            ArraySegment<byte> segment = new ArraySegment<byte>(buffer);

            // Send message
            await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static async Task ReceiveMessagesAsync(ClientWebSocket ws)
        {
            var buffer = new byte[1024];
            WebSocketReceiveResult result;
            StringBuilder messageBuilder = new StringBuilder();

            // Continuously receive messages
            while (true)
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                // If the message is complete
                if (result.EndOfMessage)
                {
                    string message = messageBuilder.ToString();
                    Console.WriteLine($"Received: {message}");
                    HandleResponse(message);

                    // Reset for the next message
                    messageBuilder.Clear();
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }

        private static void HandleResponse(string message)
        {
            // Deserialize the message into a dynamic object
            dynamic response = JsonConvert.DeserializeObject(message);

            if (response.type == "subscriptions")
            {
                foreach (var channel in response.channels)
                {
                    string channelName = channel.name;
                    Console.WriteLine($"Channel: {channelName}");
                    foreach (var symbol in channel.symbols)
                    {
                        Console.WriteLine($"  Symbol: {symbol}");
                    }

                    // Check for errors in the subscription response
                    if (channel.error != null)
                    {
                        Console.WriteLine($"  Error: {channel.error}");
                    }
                }
            }
            else
            {
                Console.WriteLine("Received an unexpected response type");
            }
        }

        static async Task Main(string[] args)
        {
            string url = "wss://your.websocket.server.url";  // Replace with actual WebSocket server URL
            await ConnectWebSocketAsync(url);
        }
    }
}
