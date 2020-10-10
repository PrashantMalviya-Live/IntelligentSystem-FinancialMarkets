using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ZeroMQ;
using GlobalLayer;
using System.IO;
using System.Text.Json;
namespace ZMQFacade
{
    public class ZMQServer
    {
        ZContext _context;
        ZSocket _publisher;
        ZError error;
        public string TCPPort { get; set; } = "tcp://*:5555";
        public ZMQServer(string TCPPort = "tcp://*:5555")
        {
            _context = new ZContext();
            _publisher = new ZSocket(_context, ZSocketType.PUB);
            _publisher.Linger = TimeSpan.Zero;
            _publisher.Bind(TCPPort);
        }
        //public void Bind(string tcpPort)
        //{
        //    _publisher.Bind(tcpPort);
        //}
        //public void PublishAllTicks(byte[] tickData)
        //{
        //    try
        //    {
        //        // Write two messages, each with an envelope and content
        //        using (var message = new ZMessage())
        //        {
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
        //public static void PublishAllTicks(byte[] tickData)
        //{
        //    //
        //    // Pubsub envelope publisher
        //    //
        //    // Author: metadings
        //    //

        //    // Prepare our context and publisher
        //    using (var context = new ZContext())
        //    using (var publisher = new ZSocket(context, ZSocketType.PUB))
        //    {
        //        publisher.Linger = TimeSpan.Zero;
        //        publisher.Bind("tcp://*:5555");

        //        //while (true)
        //        //{
        //        // Write two messages, each with an envelope and content
        //        using (var message = new ZMessage())
        //        {
        //            message.Add(new ZFrame(tickData));
        //            publisher.Send(message);

        //        }
        //        //}
        //    }
        //}
        public void PublishAllTicks(List<Tick> tickData)
        {
            foreach (Tick tick in tickData)
            {
                PublishData(tick.InstrumentToken, TickDataSchema.ParseTickBytes(tick));
            }
        }


        //public void PublishTicksByToken(byte[] tickData)
        //{
        //    int offset = 0;
        //    ushort count = TickDataSchema.ReadShort(tickData, ref offset); //number of packets

        //    ///publish Data and generate tick back at subcribers
        //    for (ushort i = 0; i < count; i++)
        //    {
        //        tickData.Skip(offset);

        //        ushort length = TickDataSchema.ReadShort(tickData, ref offset); // length of the packet

        //        uint token = TickDataSchema.ReadInt(tickData, ref offset);
        //        offset -= 4;

        //        PublishData(token, tickData.Take(2 + length).ToArray()); //2 for length of packet

        //        offset += length;
        //    }
        //}
        private void PublishData(uint token, byte[] tickData)
        {
            try
            {
                // Write two messages, each with an envelope and content
                using (var message = new ZMessage())
                {
                    message.Add(new ZFrame(token));
                    message.Add(new ZFrame(tickData));
                    message.Add(new ZFrame(DateTime.UtcNow.ToString("s")));
                    if (!_publisher.Send(message, out error))
                    {
                        if (error != ZError.ETERM)
                            throw new ZException(error);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWrite(String.Format("Message:{0} \n Trace:{1}", ex.Message, ex.StackTrace));
                //throw ex;
            }
        }

        
    }
}
 
