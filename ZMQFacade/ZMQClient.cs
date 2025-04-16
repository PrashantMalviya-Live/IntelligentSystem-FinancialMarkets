using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GlobalLayer;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetMQ.Sockets;
using NetMQ;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace ZMQFacade
{
    public class ZMQClient
    {
        public readonly TimeSpan MAX_ALLOWED_DELAY = TimeSpan.FromMilliseconds(25000);
        //ZContext _context;
        //ZSocket _subscriber;
        //ZError error;

        SubscriberSocket sub;
        public string TCPPort { get; set; } = "tcp://127.0.0.1:5555";
        public ZMQClient(string TCPPort = "tcp://127.0.0.1:5555")
        {
            sub = new SubscriberSocket();
            sub.Connect(TCPPort);
            sub.Options.ReceiveHighWatermark = 5000;
        }

        public void AddSubscriber(List<uint> tokens)
        {
            //_subscriber.Subscribe("");
            foreach (uint token in tokens)
            {
                sub.Subscribe(BitConverter.GetBytes(token));
            }
        }
        public async Task Subscribe(IZMQ algoObject)
        {
            //Task<bool> dataProcessed = null;
            while (true)
            {
                try
                {
                    NetMQMessage message = sub.ReceiveMultipartMessage(3);//.ReceiveMessage(out error)

                    if (message == null)
                    {
                        throw new NetMQException("Null message received");
                    }
                    //string sentDateTime = message[2].ReadString();
                    //if (DateTime.UtcNow - DateTime.Parse(sentDateTime) > MAX_ALLOWED_DELAY)
                    //{
                    //    //Logger.LogWrite("Subscriber cannot keep up with the message so aborting");
                    //    //break;
                    //}

                    //ready = false;

                    // Read envelope with address
                    byte[] tickData = message[1].Buffer;
                    
                    SendData(algoObject, tickData);
                    
                    //if (dataProcessed == null || dataProcessed.IsCompleted)
                    //{
                    //dataProcessed = SendData(algoObject, tickData);
                    //}
                    //await dataProcessed;
                }
                catch (Exception ex)
                {
                    Logger.LogWrite(String.Format("Message:{0} \n Trace:{1}", ex.Message, ex.StackTrace));
                    //throw ex;
                }
            }
        }

        public static void SendData(IZMQ algoObject, byte[] tickData)
        {
#if LOCAL
            Tick tick = TickDataSchema.ParseTick(tickData, shortenedTick: true);
#else
            Tick tick = TickDataSchema.ParseTick(tickData, shortenedTick: false);
#endif

            if (tick.InstrumentToken != 0 && tick.Timestamp != null)
                //algoObject.OnNext(new Tick[] { tick });
                algoObject.OnNext(tick);
        }
        public static object DeserializeObject(byte[] byteArray)
        {
            using (MemoryStream memoryStream = new MemoryStream(byteArray))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                return formatter.Deserialize(memoryStream);
            }
        }

//        public static void SendFyersData(IZMQ algoObject, byte[] tickData)
//        {
//#if LOCAL
//            Tick tick = TickDataSchema.ParseTick(tickData, shortenedTick: true);
//#else
//            FyerTick tick = (FyerTick) DeserializeObject(tickData);
//#endif

//            //if (tick.InstrumentToken != 0 && tick.Timestamp != null)
//                //algoObject.OnNext(new Tick[] { tick });
//              //  algoObject.OnNext(Tick.);
//        }

//        //public static async Task<bool> SendData(IZMQ algoObject, byte[] tickData)
//        //{
//        //    Tick tick = TickDataSchema.ParseTick(tickData);

//        //    if (tick.InstrumentToken != 0 && tick.Timestamp != null)
//        //        await algoObject.OnNext(new Tick[] { tick });

//        //    return true;
//        //}





    }
}
