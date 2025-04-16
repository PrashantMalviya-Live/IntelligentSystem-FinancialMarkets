using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Global;

namespace KafkaStreams
{
    public class TickDataSchema
    {
        public static List<Tick> ParseTicks(byte[] Data)
        {
            int offset = 0;
            //uint count = ReadInt(Data, ref offset); //number of packets

            ushort count = ReadShort(Data, ref offset); //number of packets

            //if (_debug) Console.WriteLine("No of packets: " + count);
            //if (_debug) Console.WriteLine("No of bytes: " + Count);

            ///publish Data and generate tick back at subcribers

            //Tick[] ticks = new Tick[3000];
            List<Tick> ticks = new List<Tick>();
            for (uint i = 0; i < count; i++)
            {
                ushort length = ReadShort(Data, ref offset); // length of the packet
                                                             // if (_debug) Console.WriteLine("Packet Length " + length);

                Tick tick = new Tick();
                switch (length)
                {
                    case 8: //ltp
                        tick = ReadLTP(Data, ref offset);
                        break;
                    case 28: //index quote
                        tick = ReadIndexQuote(Data, ref offset);
                        break;
                    case 32: //index quote
                        tick = ReadIndexFull(Data, ref offset);
                        break;
                    case 44: //quote
                        tick = ReadQuote(Data, ref offset);
                        break;
                    case 184: // full with marketdepth and timestamp
                        tick = ReadFull(Data, ref offset);
                        break;

                }
                //if (length == 8) // ltp
                //    tick = tickSchema.ReadLTP(Data, ref offset);
                //else if (length == 28) // index quote
                //    tick = tickSchema.ReadIndexQuote(Data, ref offset);
                //else if (length == 32) // index quote
                //    tick = tickSchema.ReadIndexFull(Data, ref offset);
                //else if (length == 44) // quote
                //    tick = tickSchema.ReadQuote(Data, ref offset);
                //else if (length == 184) // full with marketdepth and timestamp
                //    tick = tickSchema.ReadFull(Data, ref offset);

                // If the number of bytes got from stream is less that that is required
                // data is invalid. This will skip that wrong tick
                if (tick.InstrumentToken != 0)// && IsConnected && offset <= Count)
                {
                    //ticks[i] = tick;
                    ticks.Add(tick);
                    // OnTick(tick,_context);
                }

            }

            //#region Write to File - Temp

            //System.IO.File.WriteAllText(@"D:\Prashant\Intelligent Software\2019\MarketData\Log\ReadTick.txt", JsonSerializer.Serialize(ticks.Where(x=>x.InstrumentToken >0).ToArray()));

            //#endregion

            return ticks;// ticks.Where(x => x != null && x.InstrumentToken > 0);
        }
        public static Tick ParseTick(byte[] Data)
        {
            int offset = 0;
            ushort length = ReadShort(Data, ref offset); // length of the packet
                                                         // if (_debug) Console.WriteLine("Packet Length " + length);

            Tick tick = new Tick();
            switch (length)
            {
                case 8: //ltp
                    tick = ReadLTP(Data, ref offset);
                    break;
                case 28: //index quote
                    tick = ReadIndexQuote(Data, ref offset);
                    break;
                case 32: //index quote
                    tick = ReadIndexFull(Data, ref offset);
                    break;
                case 44: //quote
                    tick = ReadQuote(Data, ref offset);
                    break;
                case 184: // full with marketdepth and timestamp
                    tick = ReadFull(Data, ref offset);
                    break;

            }
            // If the number of bytes got from stream is less that that is required
            // data is invalid. This will skip that wrong tick
            //if (tick.InstrumentToken != 0)// && IsConnected && offset <= Count)
            //{
            //return tick;
            //    // OnTick(tick,_context);
            //}

            return tick;
        }
        /// <summary>
        /// Reads 2 byte short int from byte stream
        /// </summary>
        public static ushort ReadShort(byte[] b, ref int offset)
        {
            ushort data = (ushort)(b[offset + 1] + (b[offset] << 8));
            offset += 2;
            return data;
        }
        //public static ushort ReadShort(byte[] b, ref int offset)
        //{
        //    ushort data = (ushort)BitConverter.ToUInt16(new byte[] { b[offset + 0], b[offset + 1] }, offset);
        //    offset += 2;
        //    return data;
        //}

        /// <summary>
        /// Reads 4 byte int32 from byte stream
        /// </summary>
        public static UInt32 ReadInt(byte[] b, ref int offset)
        {
            UInt32 data = (UInt32)BitConverter.ToUInt32(new byte[] { b[offset + 3], b[offset + 2], b[offset + 1], b[offset + 0] }, 0);
            offset += 4;
            return data;
        }

        /// <summary>
        /// Reads an ltp mode tick from raw binary data
        /// </summary>
        public static Tick ReadLTP(byte[] b, ref int offset)
        {
            Tick tick = new Tick();
            tick.Mode = Constants.MODE_LTP;
            tick.InstrumentToken = ReadInt(b, ref offset);

            decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;

            tick.Tradable = (tick.InstrumentToken & 0xff) != 9;
            tick.LastPrice = ReadInt(b, ref offset) / divisor;
            return tick;
        }

        /// <summary>
        /// Reads a index's quote mode tick from raw binary data
        /// </summary>
        public static Tick ReadIndexQuote(byte[] b, ref int offset)
        {
            Tick tick = new Tick();
            tick.Mode = Constants.MODE_QUOTE;
            tick.InstrumentToken = ReadInt(b, ref offset);

            decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;

            tick.Tradable = (tick.InstrumentToken & 0xff) != 9;
            tick.LastPrice = ReadInt(b, ref offset) / divisor;
            tick.High = ReadInt(b, ref offset) / divisor;
            tick.Low = ReadInt(b, ref offset) / divisor;
            tick.Open = ReadInt(b, ref offset) / divisor;
            tick.Close = ReadInt(b, ref offset) / divisor;
            tick.Change = ReadInt(b, ref offset) / divisor;
            return tick;
        }

        public static Tick ReadIndexFull(byte[] b, ref int offset)
        {
            Tick tick = new Tick();
            tick.Mode = Constants.MODE_FULL;
            tick.InstrumentToken = ReadInt(b, ref offset);

            decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;

            tick.Tradable = (tick.InstrumentToken & 0xff) != 9;
            tick.LastPrice = ReadInt(b, ref offset) / divisor;
            tick.High = ReadInt(b, ref offset) / divisor;
            tick.Low = ReadInt(b, ref offset) / divisor;
            tick.Open = ReadInt(b, ref offset) / divisor;
            tick.Close = ReadInt(b, ref offset) / divisor;
            tick.Change = ReadInt(b, ref offset) / divisor;
            uint time = ReadInt(b, ref offset);
            tick.Timestamp = Utils.UnixToDateTime(time);
            return tick;
        }

        /// <summary>
        /// Reads a quote mode tick from raw binary data
        /// </summary>
        public static Tick ReadQuote(byte[] b, ref int offset)
        {
            Tick tick = new Tick();
            tick.Mode = Constants.MODE_QUOTE;
            tick.InstrumentToken = ReadInt(b, ref offset);

            decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;

            tick.Tradable = (tick.InstrumentToken & 0xff) != 9;
            tick.LastPrice = ReadInt(b, ref offset) / divisor;
            tick.LastQuantity = ReadInt(b, ref offset);
            tick.AveragePrice = ReadInt(b, ref offset) / divisor;
            tick.Volume = ReadInt(b, ref offset);
            tick.BuyQuantity = ReadInt(b, ref offset);
            tick.SellQuantity = ReadInt(b, ref offset);
            tick.Open = ReadInt(b, ref offset) / divisor;
            tick.High = ReadInt(b, ref offset) / divisor;
            tick.Low = ReadInt(b, ref offset) / divisor;
            tick.Close = ReadInt(b, ref offset) / divisor;

            return tick;
        }

        /// <summary>
        /// Reads a full mode tick from raw binary data
        /// </summary>
        public static Tick ReadFull(byte[] b, ref int offset)
        {
            Tick tick = new Tick();
            tick.Mode = Constants.MODE_FULL;
            tick.InstrumentToken = ReadInt(b, ref offset);

            decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;

            tick.Tradable = (tick.InstrumentToken & 0xff) != 9;
            tick.LastPrice = ReadInt(b, ref offset) / divisor;
            tick.LastQuantity = ReadInt(b, ref offset);
            tick.AveragePrice = ReadInt(b, ref offset) / divisor;
            tick.Volume = ReadInt(b, ref offset);
            tick.BuyQuantity = ReadInt(b, ref offset);
            tick.SellQuantity = ReadInt(b, ref offset);
            tick.Open = ReadInt(b, ref offset) / divisor;
            tick.High = ReadInt(b, ref offset) / divisor;
            tick.Low = ReadInt(b, ref offset) / divisor;
            tick.Close = ReadInt(b, ref offset) / divisor;

            // KiteConnect 3 fields
            tick.LastTradeTime = Utils.UnixToDateTime(ReadInt(b, ref offset));
            tick.OI = ReadInt(b, ref offset);
            tick.OIDayHigh = ReadInt(b, ref offset);
            tick.OIDayLow = ReadInt(b, ref offset);
            tick.Timestamp = Utils.UnixToDateTime(ReadInt(b, ref offset));


            tick.Bids = new DepthItem[5];
            for (int i = 0; i < 5; i++)
            {
                tick.Bids[i].Quantity = ReadInt(b, ref offset);
                tick.Bids[i].Price = ReadInt(b, ref offset) / divisor;
                tick.Bids[i].Orders = ReadShort(b, ref offset);
                offset += 2;
            }

            tick.Offers = new DepthItem[5];
            for (int i = 0; i < 5; i++)
            {
                tick.Offers[i].Quantity = ReadInt(b, ref offset);
                tick.Offers[i].Price = ReadInt(b, ref offset) / divisor;
                tick.Offers[i].Orders = ReadShort(b, ref offset);
                offset += 2;
            }
            return tick;
        }

        public static byte[] ParseTickBytes(Tick tick)
        {
            List<byte> b = new List<byte>();

            ushort length = 184;
            
            //put length at the first and then entire tick bytes. This helps in case tick data has come from different mode.
            switch (tick.Mode)
            {
                case Constants.MODE_LTP:
                    length = 8;
                    b = WriteLTP(b, length, tick);
                    break;

                case Constants.MODE_QUOTE:
                    if (tick.Tradable)
                    {
                        length = 44;
                        b = WriteQuote(b, length, tick);
                    }
                    else
                    {
                        length = 28;
                        b = WriteIndexQuote(b, length, tick);
                    }
                    break;

                case Constants.MODE_FULL:
                    if (tick.Tradable)
                    {
                        length = 184;
                        b = WriteFull(b, length, tick);
                    }
                    else
                    {
                        length = 32;
                        b = WriteIndexFull(b, length, tick);
                    }
                    break;
            }

            

            ////put tick data to bytes. Done only for 1844
            //b = WriteFull(b, tick);

            return b.ToArray();

        }
        /// <summary>
        /// Reads an ltp mode tick from raw binary data
        /// </summary>
        public static List<byte> WriteLTP(List<byte> b, ushort length, Tick tick)
        {
            b.AddRange(BitConverter.GetBytes(length).Reverse());
            b.AddRange(BitConverter.GetBytes(tick.InstrumentToken).Reverse());
            decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;
            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.LastPrice * divisor)).Reverse());

            return b;
        }
        /// <summary>
        /// Reads a index's quote mode tick from raw binary data
        /// </summary>
        public static List<byte> WriteIndexQuote(List<byte> b, ushort length, Tick tick)
        {
            b.AddRange(BitConverter.GetBytes(length).Reverse());
            b.AddRange(BitConverter.GetBytes(tick.InstrumentToken).Reverse());
            decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;
            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.LastPrice * divisor)).Reverse());

            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.High * divisor)).Reverse());
            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.Low * divisor)).Reverse());
            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.Open * divisor)).Reverse());
            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.Close * divisor)).Reverse());
            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.Change * divisor)).Reverse());

            return b;
        }
        public static List<byte> WriteIndexFull(List<byte> b, ushort length, Tick tick)
        {
            b.AddRange(BitConverter.GetBytes(length).Reverse());
            b.AddRange(BitConverter.GetBytes(tick.InstrumentToken).Reverse());
            decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;

            //tick.Tradable = (tick.InstrumentToken & 0xff) != 9;

            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.LastPrice * divisor)).Reverse());

            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.High * divisor)).Reverse());
            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.Low * divisor)).Reverse());
            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.Open * divisor)).Reverse());
            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.Close * divisor)).Reverse());
            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.Change * divisor)).Reverse());
            b.AddRange(BitConverter.GetBytes(Utils.UnixFromDateTime(tick.Timestamp)).Take(4).Reverse());
            return b;
        }

        /// <summary>
        /// Reads a quote mode tick from raw binary data
        /// </summary>
        public static List<byte> WriteQuote(List<byte> b, ushort length, Tick tick)
        {
            b.AddRange(BitConverter.GetBytes(length).Reverse());
            b.AddRange(BitConverter.GetBytes(tick.InstrumentToken).Reverse());
            decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;
            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.LastPrice * divisor)).Reverse());
            b.AddRange(BitConverter.GetBytes(tick.LastQuantity).Reverse());
            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.AveragePrice * divisor)).Reverse());
            b.AddRange(BitConverter.GetBytes(tick.Volume).Reverse());
            b.AddRange(BitConverter.GetBytes(tick.BuyQuantity).Reverse());
            b.AddRange(BitConverter.GetBytes(tick.SellQuantity).Reverse());
            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.Open * divisor)).Reverse());
            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.High * divisor)).Reverse());
            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.Low * divisor)).Reverse());
            b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.Close * divisor)).Reverse());

            return b;
        }

        /// <summary>
        /// Reads a full mode tick from raw binary data
        /// </summary>
        public static List<byte> WriteFull(List<byte> b, ushort length, Tick tick)
        {
            //Tick[] validTicks = ticks.Where(x => x.InstrumentToken > 0).ToArray();

            //#region Write to File - Temp
            //System.IO.File.WriteAllText(@"D:\Prashant\Intelligent Software\2019\MarketData\Log\WriteTick.txt", JsonSerializer.Serialize(validTicks));
            //#endregion

            //ushort packetCount = Convert.ToUInt16(validTicks.Count());
            //b.AddRange(BitConverter.GetBytes(packetCount).Reverse());


            //Tick tick = new Tick();
            //tick.Mode = Constants.MODE_FULL;
            b.AddRange(BitConverter.GetBytes(length).Reverse());
            b.AddRange(BitConverter.GetBytes(tick.InstrumentToken).Reverse());
                decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;

                //tick.Tradable = (tick.InstrumentToken & 0xff) != 9;

                b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.LastPrice * divisor)).Reverse());
                b.AddRange(BitConverter.GetBytes(tick.LastQuantity).Reverse());
                b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.AveragePrice * divisor)).Reverse());
                b.AddRange(BitConverter.GetBytes(tick.Volume).Reverse());
                b.AddRange(BitConverter.GetBytes(tick.BuyQuantity).Reverse());
                b.AddRange(BitConverter.GetBytes(tick.SellQuantity).Reverse());
                b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.Open * divisor)).Reverse());
                b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.High * divisor)).Reverse());
                b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.Low * divisor)).Reverse());
                b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.Close * divisor)).Reverse());


                b.AddRange(BitConverter.GetBytes(Utils.UnixFromDateTime(tick.LastTradeTime)).Take(4).Reverse());
                b.AddRange(BitConverter.GetBytes(tick.OI).Reverse());
                b.AddRange(BitConverter.GetBytes(tick.OIDayHigh).Reverse());
                b.AddRange(BitConverter.GetBytes(tick.OIDayLow).Reverse());
                b.AddRange(BitConverter.GetBytes(Utils.UnixFromDateTime(tick.Timestamp)).Take(4).Reverse());


                if (tick.Bids != null)
                {
                    for (int i = 0; i < tick.Bids.Length; i++)
                    {
                        b.AddRange(BitConverter.GetBytes(tick.Bids[i].Quantity).Reverse());
                        b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.Bids[i].Price * divisor)).Reverse());
                        b.AddRange(BitConverter.GetBytes(tick.Bids[i].Orders).Take(2).Reverse());
                        b.AddRange(BitConverter.GetBytes(tick.Bids[i].Orders).Take(2).Reverse());
                    }
                }
                if (tick.Offers != null)
                {
                    for (int i = 0; i < tick.Offers.Length; i++)
                    {
                        b.AddRange(BitConverter.GetBytes(tick.Offers[i].Quantity).Reverse());
                        b.AddRange(BitConverter.GetBytes(Convert.ToUInt32(tick.Offers[i].Price * divisor)).Reverse());
                        b.AddRange(BitConverter.GetBytes(tick.Offers[i].Orders).Take(2).Reverse());
                        b.AddRange(BitConverter.GetBytes(tick.Offers[i].Orders).Take(2).Reverse());
                    }
                }
            return b;
        }
    }
}
