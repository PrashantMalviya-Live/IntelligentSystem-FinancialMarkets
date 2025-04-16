using System;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;


namespace MQTTFacade
{
    public class MQTTPublisher
    {
        IManagedMqttClient _mqttClient;
        ManagedMqttClientOptions options;
        MqttClientOptionsBuilder builder;
        public MQTTPublisher()
        {
            builder = new MqttClientOptionsBuilder()
                                     .WithClientId("Dev.To")
                                     .WithTcpServer("localhost", 707);

            options = new ManagedMqttClientOptionsBuilder()
                                    .WithAutoReconnectDelay(TimeSpan.FromSeconds(60))
                                    .WithClientOptions(builder.Build())
                                    .Build();

            _mqttClient = new MqttFactory().CreateManagedMqttClient();

            _mqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate(OnConnected);
            _mqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(OnDisconnected);
            _mqttClient.ConnectingFailedHandler = new ConnectingFailedHandlerDelegate(OnConnectingFailed);

            //_mqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(a =>
            //{
            //    Log.Logger.Information("Message recieved: {payload}", a.ApplicationMessage);
            //});
        }

        public void Publish()
        {
            _mqttClient.StartAsync(options).GetAwaiter().GetResult();

            while (true)
            {
                string json = JsonConvert.SerializeObject(new { message = "Heyo :)", sent = DateTimeOffset.UtcNow });
                _mqttClient.PublishAsync("dev.to/topic/json", json);

                Task.Delay(1000).GetAwaiter().GetResult();
            }
        }
        public static void OnConnected(MqttClientConnectedEventArgs obj)
        {
            //Log.Logger.Information("Successfully connected.");
        }

        public static void OnConnectingFailed(ManagedProcessFailedEventArgs obj)
        {
            //Log.Logger.Warning("Couldn't connect to broker.");
        }

        public static void OnDisconnected(MqttClientDisconnectedEventArgs obj)
        {
            //Log.Logger.Information("Successfully disconnected.");
        }
    }
}
