using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroMQ;
using GlobalLayer;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZMQFacade
{
    public class ZMQClient
    {
        public readonly TimeSpan MAX_ALLOWED_DELAY = TimeSpan.FromMilliseconds(25000);
        ZContext _context;
        ZSocket _subscriber;
        ZError error;
        
        public string TCPPort { get; set; } = "tcp://localhost:5555";
        public ZMQClient(string TCPPort = "tcp://localhost:5555")
        {
            _context = new ZContext();
            _subscriber = new ZSocket(_context, ZSocketType.SUB);
            _subscriber.Connect(TCPPort);
            _subscriber.TcpKeepAlive = TcpKeepaliveBehaviour.Enable;
            _subscriber.TcpKeepAliveCount = 300000;
            _subscriber.TcpKeepAliveInterval = 3000000;
        }

        public void AddSubscriber(List<uint> tokens)
        {
            //_subscriber.Subscribe("");
            foreach (uint token in tokens)
            {
                _subscriber.Subscribe(BitConverter.GetBytes(token));

            }
        }
        public async Task Subscribe(IZMQ algoObject)
        {
            Task<bool> dataProcessed = null;
            while (true)
            {
                try
                {
                    using (ZMessage message = _subscriber.ReceiveMessage(out error))
                    {

                        if (message == null)
                        {
                            if (error == ZError.ETERM)
                                break;
                            throw new ZException(error);
                        }
                        //string sentDateTime = message[2].ReadString();
                        //if (DateTime.UtcNow - DateTime.Parse(sentDateTime) > MAX_ALLOWED_DELAY)
                        //{
                        //    //Logger.LogWrite("Subscriber cannot keep up with the message so aborting");
                        //    //break;
                        //}

                        //ready = false;

                        // Read envelope with address
                        byte[] tickData = message[1].Read();
                        if (dataProcessed == null || dataProcessed.IsCompleted)
                        {
                            dataProcessed = SendData(algoObject, tickData);
                        }
                           // await dataProcessed;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWrite(String.Format("Message:{0} \n Trace:{1}", ex.Message, ex.StackTrace));
                    //throw ex;
                }
                //finally
                //{
                //    _context.Dispose();
                //    _context.Dispose();
                //}
            }
        }
        //public static void ZMQSubcribebyToken(IZMQ algoObject, string[] tokens)
        //{
        //    ZError error;
        //    bool ready = false;
        //    Task<bool> dataProcessed = null; 
        //    using (var context = new ZContext())
        //    using (var subscriber = new ZSocket(context, ZSocketType.SUB))
        //    {
        //        subscriber.Connect("tcp://localhost:5555");
                
        //        //foreach(string token in tokens)
        //        //{
        //        //    subscriber.Subscribe(token);
        //        //}
        //        subscriber.Subscribe("");
        //        subscriber.TcpKeepAlive = TcpKeepaliveBehaviour.Enable;
        //        subscriber.TcpKeepAliveCount = 300000;
        //        subscriber.TcpKeepAliveInterval = 3000000;

        //        while (true)
        //        {
        //            try
        //            {
        //                using (ZMessage message = subscriber.ReceiveMessage(out error))
        //                {
                            
        //                    if (message == null)
        //                    {
        //                        if (error == ZError.ETERM)
        //                            break;
        //                        throw new ZException(error);
        //                    }
        //                    if (DateTime.UtcNow - DateTime.Parse(message[2].ReadString()) > MAX_ALLOWED_DELAY)
        //                    {
        //                        Logger.LogWrite("Subscriber cannot keep up with the message so aborting");
        //                        break;
        //                    }

        //                    ready = false;

        //                    // Read envelope with address
        //                    byte[] tickData = message[1].Read();
        //                    //Tick[] ticks = TickDataSchema.ParseTicks(tickData);

        //                    if (dataProcessed == null || dataProcessed.IsCompleted)
        //                    {
        //                        dataProcessed = SendData(algoObject, tickData);
        //                    }
        //                    //Tick[] ticks = TickDataSchema.ParseTicks(tickData);
        //                    //algoObject.OnNext(ticks);
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                Logger.LogWrite(String.Format("Message:{0} \n Trace:{1}", ex.Message, ex.StackTrace));
        //                //throw ex;
        //            }
        //        }                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               
        //    }
        //}

        public static async Task<bool> SendData(IZMQ algoObject, byte[] tickData)
        {
            Tick tick = TickDataSchema.ParseTick(tickData);

            if(tick.InstrumentToken != 0 && tick.Timestamp != null)
            await algoObject.OnNext(new Tick[] { tick });

            return true;
        }


        //public static void ZMQSubcribeAllTicks(IZMQ algoObject)
        //{
        //    ZError error;
        //    bool ready = false;
        //    Task<bool> dataProcessed = null;

        //    using (var context = new ZContext())
        //    using (var subscriber = new ZSocket(context, ZSocketType.SUB))
        //    {
        //        subscriber.Connect("tcp://localhost:5555");
        //        subscriber.Subscribe("");
        //        subscriber.TcpKeepAlive = TcpKeepaliveBehaviour.Enable;
        //        subscriber.TcpKeepAliveCount = 300000;
        //        subscriber.TcpKeepAliveInterval = 3000000;

        //        while (true)
        //        {
        //            try
        //            {
        //                using (ZMessage message = subscriber.ReceiveMessage(out error))
        //                {

        //                    if(message == null)
        //                    {
        //                        if (error == ZError.ETERM)
        //                            break;
        //                        throw new ZException(error);
        //                    }
        //                    string sentDateTime = message[2].ReadString();
        //                    if ( DateTime.UtcNow - DateTime.Parse(sentDateTime) > MAX_ALLOWED_DELAY)
        //                    {
        //                        Logger.LogWrite("Subscriber cannot keep up with the message so aborting");
        //                        break;
        //                    }

        //                    ready = false;

        //                    // Read envelope with address
        //                    byte[] tickData = message[1].Read();
        //                    if (dataProcessed == null || dataProcessed.IsCompleted)
        //                    {
        //                        dataProcessed = SendData(algoObject, tickData);
        //                    }
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                Logger.LogWrite(String.Format("Message:{0} \n Trace:{1}", ex.Message, ex.StackTrace));
        //                //throw ex;
        //            }
        //        }
        //    }
        //}

        //static object Deserialize(byte[] buffer, Type type)
        //{
        //    using (StreamReader sr = new StreamReader(new MemoryStream(buffer)))
        //    {
        //        return JsonConvert.DeserializeObject(sr.ReadToEnd(), type);
        //    }
        //}


    }
}
