using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ;
using GlobalLayer;
using System.IO;
using System.Text.Json;
using NetMQ.Sockets;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.Numerics;
using System.Runtime.Serialization.Formatters.Binary;

namespace ZMQFacade
{
    public class CryptoZMQServer
    {
        PublisherSocket pub;
        public string TCPPort { get; set; } = "tcp://127.0.0.1:5566";
        public CryptoZMQServer(string TCPPort = "tcp://127.0.0.1:5566")
        {
            pub = new PublisherSocket();
            pub.Bind(TCPPort); // BindRandomPort("tcp://127.0.0.1");

            pub.Options.SendHighWatermark = 5000;
        }
        //public void PublishData(string channel List<Tick> tickData, bool shortenedTick = false)
        //{
        //    foreach (Tick tick in tickData)
        //    {
        //        PublishData(tick.Symbol, TickDataSchema.ParseTickBytes(tick, shortenedTick));
        //    }
        //}
        //public byte[] SerializeObject(object obj)
        //{
        //    using (MemoryStream memoryStream = new MemoryStream())
        //    {
        //        BinaryFormatter formatter = new BinaryFormatter();
        //        formatter.Serialize(memoryStream, obj);
        //        return memoryStream.ToArray();
        //    }
        //}
        //public void PublishFyersTick(FyerTick tick)
        //{
        //    try
        //    {
        //        var message = new NetMQMessage();
        //        // Write two messages, each with an envelope and content
        //        message.Append(new NetMQFrame(tick.Symbol));
        //        message.Append(new NetMQFrame(SerializeObject(tick)));
        //        message.Append(new NetMQFrame(DateTime.UtcNow.ToString()));
        //        pub.SendMultipartMessage(message);


        //        //pub.SendMoreFrame(BitConverter.GetBytes(token)).SendMoreFrame("tickData").SendFrame(DateTime.UtcNow.ToString());


        //        //if (!_publisher.Send(message, out error))
        //        //{
        //        //    if (error != ZError.ETERM)
        //        //        throw new ZException(error);
        //        //}
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.LogWrite(String.Format("Message:{0} \n Trace:{1}", ex.Message, ex.StackTrace));
        //        //throw ex;
        //    }
        //}

        public void PublishData(string channel, string data)
        {
            try
            {
                var message = new NetMQMessage();
                // Write two messages, each with an envelope and content
                message.Append(new NetMQFrame(channel));
                message.Append(new NetMQFrame(data));
                message.Append(new NetMQFrame(DateTime.UtcNow.ToString()));
                pub.SendMultipartMessage(message);


                //pub.SendMoreFrame(BitConverter.GetBytes(token)).SendMoreFrame("tickData").SendFrame(DateTime.UtcNow.ToString());


                //if (!_publisher.Send(message, out error))
                //{
                //    if (error != ZError.ETERM)
                //        throw new ZException(error);
                //}
            }
            catch (Exception ex)
            {
                Logger.LogWrite(String.Format("Message:{0} \n Trace:{1}", ex.Message, ex.StackTrace));
                //throw ex;
            }
        }
        //private void PublishData(uint token, byte[] tickData)
        //{
        //    try
        //    {
        //        // Write two messages, each with an envelope and content
        //        using (var message = new ZMessage())
        //        {
        //            message.Add(new ZFrame(token));
        //            message.Add(new ZFrame(tickData));
        //            message.Add(new ZFrame(DateTime.UtcNow.ToString("s")));
        //            if (!_publisher.Send(message, out error))
        //            {
        //                if (error != ZError.ETERM)
        //                    throw new ZException(error);
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.LogWrite(String.Format("Message:{0} \n Trace:{1}", ex.Message, ex.StackTrace));
        //        //throw ex;
        //    }
        //}


    }
}