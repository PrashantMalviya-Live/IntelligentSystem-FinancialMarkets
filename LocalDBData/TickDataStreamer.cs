using System;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataAccess;
using System.Configuration;
using GlobalLayer;
////using Pub_Sub;
//using ZMQFacade;
using System.Threading;
//using Apache.Ignite.Core;
//using Apache.Ignite.Core.Messaging;
//using Apache.Ignite.Core.Cache.Expiry;
//using Apache.Ignite.Core.Common;
//using Algos.TLogics;
//using TibcoMessaging;
//using KafkaStreams;
//using Confluent.Kafka;
using ZMQFacade;

namespace LocalDBData
{
    public class TickDataStreamer //: IObservable<Tick[]>
    {
        /// <summary>
        /// List of subscribers to this ticker
        /// </summary>
        private List<IObserver<Tick[]>> observers;
        DateTime? prevTickTime, currentTickTime;

        //Kafka
        //Dictionary<uint, TopicPartition> instrumentPartitions = new Dictionary<uint, TopicPartition>();
        //KProducer tickProducer;
        //int partitionCount = 0;

        //public TickDataStreamer()
        //{
        //    //Subscriber list
        //    observers = new List<IObserver<Tick[]>>();

        //    tickProducer = new KProducer();
        //}

        //public IDisposable Subscribe(IObserver<Tick[]> observer)
        //{
        //    if (!observers.Contains(observer))
        //    {
        //        observers.Add(observer);
        //    }
        //    return new Unsubscriber<Tick>(observers, observer);
        //}

        public async Task<long> BeginStreaming()
        {
            //Tick tickdata =  RetrieveTicksFromDB();

          Task<long> allDone =  RetrieveTicksFromDB();

            return await allDone;
           // List<Tick> ticks = ConvertToTicks(tickdata);

            //foreach (var observer in observers)
            //    observer.OnNext(ticks.ToArray());
        }

        private async Task<long> RetrieveTicksFromDB()
        {
            long counter = 0;
            object[] data = new object[19];
            //Tick[] ticks = new Tick[500];
            List<Tick> ticks = new List<Tick>();
            //KProducer producer = new KProducer();

            ZMQServer zmqServer;
            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString()))
                {
                    await sqlConnection.OpenAsync();
                    using (SqlCommand command = new SqlCommand("GetTickDataTest", sqlConnection))
                    {
                        command.CommandTimeout = 1000000;
                        // The reader needs to be executed with the SequentialAccess behavior to enable network streaming
                        // Otherwise ReadAsync will buffer the entire BLOB into memory which can cause scalability issues or even OutOfMemoryExceptions
                        using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult))
                        {
                            if (await reader.ReadAsync())
                            {
                                if (!(await reader.IsDBNullAsync(0)))
                                {
                                    //reader.Read();
                                    //reader.GetValues(data);
                                    //Tick tick = ConvertToTicks(data);
                                    //prevTickTime = currentTickTime = tick.Timestamp;
                                    //ticks.Add(tick);
                                    //ticks[tickCounter++] = tick;
                                    zmqServer = new ZMQServer();
                                    while (reader.Read())
                                    {
                                        try
                                        {
                                            reader.GetValues(data);
                                            Tick tick = ConvertToTicks(data);
                                            currentTickTime = tick.Timestamp;

                                            if ((prevTickTime == null || (currentTickTime.Value - prevTickTime.Value).TotalSeconds < 1) && ticks.Count < 500)
                                            {
                                                //ticks[tickCounter++] = tick;
                                                ticks.Add(tick);

                                            }
                                            else
                                            {
                                                //byte[] b = TickDataSchema.ParseTickBytes(ticks.ToArray());

                                                //DO NOT STOREDATA TO DB IN TEST MODEL
                                                //Storage storage = new Storage(false);
                                                //storage.Store(ticks.ToArray());

                                                zmqServer.PublishAllTicks(ticks);
                                                Thread.Sleep(100);
                                                //zmqServer.PublishAllTicks(b);


                                                //IgniteConnector.StreamData(ticks.ToArray());
                                                // IgniteMessanger.SendIgniteMessage(ticks.ToArray());
                                                // IgniteConnector.InsertMarketData(ticks.ToArray());
                                                //Publisher tickPublisher = new Publisher();
                                                //tickPublisher.publish(ticks);
                                                //Thread.Sleep(10);

                                                //foreach (var observer in observers)
                                                //    observer.OnNext(ticks);
                                                //counter++;

                                                //tickProducer.Publish(ticks);
                                                //foreach (Tick t in ticks)
                                                //{
                                                //    uint iToken = t.InstrumentToken;
                                                //    if (!instrumentPartitions.ContainsKey(iToken))
                                                //        instrumentPartitions.Add(iToken, new TopicPartition(Constants.TOPIC_NAME, new Partition(partitionCount++)));

                                                //    tickProducer.KProducer.BeginProduce
                                                //       (
                                                //           instrumentPartitions[iToken],
                                                //            new Message<uint, byte[]>
                                                //            { 
                                                //                Key = iToken,
                                                //                Value =  TickDataSchema.ParseTickBytes (new Tick[] { t }), //Data.Skip(8).ToArray(), //bytes for count, length and instrument token skipped. 
                                                //                Timestamp = Timestamp.Default
                                                //            }
                                                //        );
                                                //}
                                                //tickProducer.KProducer.Flush(TimeSpan.FromSeconds(10));



                                                ticks.Clear();
                                                ticks.Add(tick);
                                            }
                                            prevTickTime = currentTickTime;
                                        }
                                        catch (Exception ex)
                                        {
                                            //var disconnectedException = ex.InnerException as ClientDisconnectedException;

                                            //if (disconnectedException != null)
                                            //{
                                            //    Console.WriteLine(
                                            //        "\n>>> Client disconnected from the cluster");

                                            //    disconnectedException.ClientReconnectTask.Wait();

                                            //    Console.WriteLine("\n>>> Client reconnected to the cluster.");

                                            //    // Updating the reference to the cache. The client reconnected to the new cluster.
                                            //    GlobalObjects.ICache = GlobalObjects.Ignite.GetCache<TickKey, Tick>(Constants.IGNITE_CACHENAME);
                                            //}
                                            //else
                                            //{
                                               // throw ex;
                                            //}
                                        }
                                        //    }
                                        //}
                                    }
                                }
                             }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return counter;
        }

        public Tick ConvertToTicks(object[] data)
        {
            Tick tick = new Tick();
            tick.InstrumentToken = Convert.ToUInt32(data[1]);
            tick.LastPrice = Convert.ToDecimal(data[2]);
            tick.LastQuantity = Convert.ToUInt32(data[3]);
            tick.AveragePrice = Convert.ToDecimal(data[4]);
            tick.Volume = Convert.ToUInt32(data[5]);
            tick.BuyQuantity = Convert.ToUInt32(data[6]);
            tick.SellQuantity = Convert.ToUInt32(data[7]);
            tick.Open = Convert.ToDecimal(data[8]);
            tick.High = Convert.ToDecimal(data[9]);
            tick.Low = Convert.ToDecimal(data[10]);
            tick.Close = Convert.ToDecimal(data[11]);

            tick.Mode = Constants.MODE_FULL;
            tick.Tradable = (tick.InstrumentToken & 0xff) != 9;

            tick.LastTradeTime = data[13] != System.DBNull.Value ? Convert.ToDateTime(data[13]) : DateTime.MinValue;

            tick.OI = Convert.ToUInt32(data[15]);
            tick.OIDayHigh = Convert.ToUInt32(data[16]);
            tick.OIDayLow = Convert.ToUInt32(data[17]);

            tick.Timestamp = data[18] != System.DBNull.Value ? Convert.ToDateTime(data[18]) : DateTime.MinValue;
           // tempTickTime = tick.Timestamp;

            DepthItem[][] backlogTicks = GetTickDataFromBacklog(Convert.ToString(data[14]));

            tick.Bids = backlogTicks[0];
            tick.Offers = backlogTicks[1];

            return tick;
        }

        private DepthItem[][] GetTickDataFromBacklog(string backlogData)
        {
            StringBuilder backlog = new StringBuilder();

            DepthItem[] bids = new DepthItem[5];
            DepthItem[] asks = new DepthItem[5];
            int i = 0;
            foreach (string bklog in backlogData.Split('|'))
            {
                if (bklog != "")
                {
                    bids[i] = new DepthItem();

                    string[] data = bklog.Split(',');
                    bids[i].Price = Convert.ToDecimal(data[1]);
                    bids[i].Orders = Convert.ToUInt32(data[2]);
                    bids[i].Quantity = Convert.ToUInt32(data[3]);

                    asks[i] = new DepthItem();
                    asks[i].Price = Convert.ToDecimal(data[4]);
                    asks[i].Orders = Convert.ToUInt32(data[5]);
                    asks[i].Quantity = Convert.ToUInt32(data[6]);
                    i++;
                }
            }

            DepthItem[][] backlogTicks = new DepthItem[2][];
            backlogTicks[0] = bids;
            backlogTicks[1] = asks;

            return backlogTicks;
        }

        private byte[] GetByteArray(DataRow data)
        {

            byte[] iToken = BitConverter.GetBytes(Convert.ToUInt32(data["InstrumentToken"]));

            decimal divisor = (Convert.ToUInt32(data["InstrumentToken"]) & 0xff) == 3 ? 10000000.0m : 100.0m;

            byte[] lastPrice = BitConverter.GetBytes(Convert.ToInt32((decimal)data["LastPrice"] * divisor));
            byte[] lastQuantity = BitConverter.GetBytes(Convert.ToUInt32(data["LastQuantity"]));
            byte[] averagePrice = BitConverter.GetBytes(Convert.ToInt32((decimal)data["AveragePrice"] * divisor));
            byte[] volume = BitConverter.GetBytes(Convert.ToUInt32(data["Volume"]));
            byte[] buyQuantity = BitConverter.GetBytes(Convert.ToUInt32(data["BuyQuantity"]));
            byte[] sellQuantity = BitConverter.GetBytes(Convert.ToUInt32(data["SellQuantity"]));
            byte[] open = BitConverter.GetBytes(Convert.ToInt32((decimal)data["Open"] * divisor));
            byte[] high = BitConverter.GetBytes(Convert.ToInt32((decimal)data["High"] * divisor));
            byte[] low = BitConverter.GetBytes(Convert.ToInt32((decimal)data["Low"] * divisor));
            byte[] close = BitConverter.GetBytes(Convert.ToInt32((decimal)data["Close"] * divisor));

            byte[] lastTradeTime;
            if (data["LastTradeTime"] != System.DBNull.Value)
            {
                lastTradeTime = BitConverter.GetBytes(Convert.ToInt32(ConvertToUnixTimestamp((DateTime?)data["LastTradeTime"])));
            }
            else
            {
                lastTradeTime = BitConverter.GetBytes(0);
            }
            byte[] oi = BitConverter.GetBytes(Convert.ToUInt32(data["OI"]));
            byte[] oiDayHigh = BitConverter.GetBytes(Convert.ToUInt32(data["OIDayHigh"]));
            byte[] oiDayLow = BitConverter.GetBytes(Convert.ToUInt32(data["OIDayLow"]));
            byte[] timestamp = BitConverter.GetBytes(Convert.ToInt32(ConvertToUnixTimestamp((DateTime?)data["Timestamp"])));

            return Combine(iToken, lastPrice, lastQuantity, averagePrice, volume, buyQuantity, sellQuantity, open, high, low, close, lastTradeTime, oi, oiDayHigh, oiDayLow, timestamp);
        }

        private byte[] GetByteArray(Tick tick)
        {

            byte[] iToken = BitConverter.GetBytes(tick.InstrumentToken);

            decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;

            byte[] lastPrice = BitConverter.GetBytes(Convert.ToInt32(tick.LastPrice * divisor));
            byte[] lastQuantity = BitConverter.GetBytes(tick.LastQuantity);
            byte[] averagePrice = BitConverter.GetBytes(Convert.ToInt32(tick.AveragePrice * divisor));
            byte[] volume = BitConverter.GetBytes(tick.Volume);
            byte[] buyQuantity = BitConverter.GetBytes(tick.BuyQuantity);
            byte[] sellQuantity = BitConverter.GetBytes(tick.SellQuantity);
            byte[] open = BitConverter.GetBytes(Convert.ToInt32(tick.Open * divisor));
            byte[] high = BitConverter.GetBytes(Convert.ToInt32(tick.High * divisor));
            byte[] low = BitConverter.GetBytes(Convert.ToInt32(tick.Low * divisor));
            byte[] close = BitConverter.GetBytes(Convert.ToInt32(tick.Close * divisor));


            byte[] lastTradeTime;
            if (tick.LastTradeTime.HasValue)
            {
                lastTradeTime = BitConverter.GetBytes(Convert.ToInt32(ConvertToUnixTimestamp(tick.LastTradeTime)));
            }
            else
            {
                lastTradeTime = BitConverter.GetBytes(0);
            }

            byte[] oi = BitConverter.GetBytes(tick.OI);
            byte[] oiDayHigh = BitConverter.GetBytes(tick.OIDayHigh);
            byte[] oiDayLow = BitConverter.GetBytes(tick.OIDayLow);
            byte[] timestamp = BitConverter.GetBytes(Convert.ToInt32(ConvertToUnixTimestamp(tick.Timestamp)));

            return Combine(iToken, lastPrice, lastQuantity, averagePrice, volume, buyQuantity, sellQuantity, open, high, low, close, lastTradeTime, oi, oiDayHigh, oiDayLow, timestamp);
        }


        public double ConvertToUnixTimestamp(DateTime? date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date.Value.ToUniversalTime() - origin;
            return Math.Floor(diff.TotalSeconds);
        }

        private byte[] Combine(params byte[][] arrays)
        {
            byte[] rv = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                System.Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
        }
        public List<Tick> ConvertToTicks(DataSet ds)
        {
            List<Tick> ticks = new List<Tick>();

            foreach (DataRow data in ds.Tables[0].Rows)
            {
                Tick tick = new Tick();
                tick.InstrumentToken = Convert.ToUInt32(data["InstrumentToken"]);
                tick.LastPrice = Convert.ToDecimal(data["LastPrice"]);
                tick.LastQuantity = Convert.ToUInt32(data["LastQuantity"]);
                tick.AveragePrice = Convert.ToDecimal(data["AveragePrice"]);
                tick.Volume = Convert.ToUInt32(data["Volume"]);
                tick.BuyQuantity = Convert.ToUInt32(data["BuyQuantity"]);
                tick.SellQuantity = Convert.ToUInt32(data["SellQuantity"]);
                tick.Open = Convert.ToDecimal(data["Open"]);
                tick.High = Convert.ToDecimal(data["High"]);
                tick.Low = Convert.ToDecimal(data["Low"]);
                tick.Close = Convert.ToDecimal(data["Close"]);


                data["LastTradeTime"] = data["LastTradeTime"] != System.DBNull.Value? Convert.ToDateTime(data["LastTradeTime"]):DateTime.MinValue;

                tick.OI = Convert.ToUInt32(data["OI"]);
                tick.OIDayHigh = Convert.ToUInt32(data["OIDayHigh"]);
                tick.OIDayLow = Convert.ToUInt32(data["OIDayLow"]);

                data["Timestamp"] = data["Timestamp"] != System.DBNull.Value ? Convert.ToDateTime(data["Timestamp"]) : DateTime.MinValue;

                ticks.Add(tick);
            }
            return ticks;
        }
    }
}
