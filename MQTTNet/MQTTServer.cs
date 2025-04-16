using System;
using System.Text;
using MQTTnet;
using MQTTnet.Server;


namespace MQTTFacade
{
    public class MQTTSubscriber
    {
        public MQTTSubscriber()
        {
            MqttServerOptionsBuilder options = new MqttServerOptionsBuilder()
               .WithDefaultEndpoint()
               .WithDefaultEndpointPort(707)
               .WithConnectionValidator(OnNewConnection)
               .WithApplicationMessageInterceptor(OnNewMessage);


            IMqttServer mqttServer = new MqttFactory().CreateMqttServer();

            mqttServer.StartAsync(options.Build()).GetAwaiter().GetResult();
            Console.ReadLine();
        }
        public static void OnNewConnection(MqttConnectionValidatorContext context)
        {
            //Log.Logger.Information(
            //        "New connection: ClientId = {clientId}, Endpoint = {endpoint}, CleanSession = {cleanSession}",
            //        context.ClientId,
            //        context.Endpoint,
            //        context.CleanSession);
        }

        public static void OnNewMessage(MqttApplicationMessageInterceptorContext context)
        {
            var payload = context.ApplicationMessage?.Payload == null ? null : Encoding.UTF8.GetString(context.ApplicationMessage?.Payload);

            //MessageCounter++;

            //Log.Logger.Information(
            //    "MessageId: {MessageCounter} - TimeStamp: {TimeStamp} -- Message: ClientId = {clientId}, Topic = {topic}, Payload = {payload}, QoS = {qos}, Retain-Flag = {retainFlag}",
            //    MessageCounter,
            //    DateTime.Now,
            //    context.ClientId,
            //    context.ApplicationMessage?.Topic,
            //    payload,
            //    context.ApplicationMessage?.QualityOfServiceLevel,
            //    context.ApplicationMessage?.Retain);
        }
    }
}
