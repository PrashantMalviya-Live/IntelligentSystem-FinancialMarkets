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
using KafkaStreams;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Global;


namespace MarketDataTest
{
    public class TickDataStreamer
    {
        public void BeginStreaming()
        {
            int partitionNumber = 0;
            DataSet tickdata =  RetrieveTicksFromDB();

            Dictionary<Int32, TopicPartition> instrumentPartitions = new Dictionary<int, TopicPartition>();

            //KafkaServer.StartKafka();

            Producer tickProducer = new Producer();
            //Partition
            using (tickProducer.KProducer)
            {
                //using (var adminClient = new AdminClient(tickProducer.KProducer.Handle))
                //{
                    //adminClient.CreateTopicsAsync(new TopicSpecification[] { new TopicSpecification { Name = Constants.TOPIC_NAME, NumPartitions = 1, ReplicationFactor = 1 } }).Wait();
                    //adminClient.CreatePartitionsAsync(new List<PartitionsSpecification> { new PartitionsSpecification { Topic = Constants.TOPIC_NAME, IncreaseTo = 500 } }).Wait();

                    foreach (DataRow tick in tickdata.Tables[0].Rows)
                    {

                       
                        int iToken = Convert.ToInt32(tick["InstrumentToken"]);
                        if (!instrumentPartitions.Keys.Contains(iToken))
                            instrumentPartitions.Add(iToken, new TopicPartition(Constants.TOPIC_NAME, new Partition(partitionNumber++)));

                        tickProducer.KProducer.BeginProduce
                           (
                               instrumentPartitions[iToken],
                                new Message<int, byte[]>
                                {
                                    Key = iToken,
                                    Value = GetByteArray(tick), //Data.Skip(8).ToArray(), //bytes for count, length and instrument token skipped. 
                                Timestamp = Timestamp.Default
                                }
                            );
                    }

                    tickProducer.KProducer.Flush(TimeSpan.FromSeconds(10));
                  //  adminClient.DeleteTopicsAsync(new List<string> { Constants.TOPIC_NAME }).Wait();
                //}

            }
            
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
           
           byte[] iToken =  BitConverter.GetBytes(tick.InstrumentToken);

            decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;

            byte[] lastPrice = BitConverter.GetBytes(Convert.ToInt32(tick.LastPrice* divisor));
            byte[] lastQuantity = BitConverter.GetBytes(tick.LastQuantity);
            byte[] averagePrice = BitConverter.GetBytes(Convert.ToInt32(tick.AveragePrice * divisor));
            byte[] volume = BitConverter.GetBytes(tick.Volume);
            byte[] buyQuantity = BitConverter.GetBytes(tick.BuyQuantity);
            byte[] sellQuantity = BitConverter.GetBytes(tick.SellQuantity);
            byte[] open = BitConverter.GetBytes(Convert.ToInt32(tick.Open * divisor));
            byte[] high = BitConverter.GetBytes(Convert.ToInt32(tick.High * divisor));
            byte[] low = BitConverter.GetBytes(Convert.ToInt32(tick.Low * divisor));
            byte[] close = BitConverter.GetBytes(Convert.ToInt32(tick.Close * divisor));


            byte[] lastTradeTime = BitConverter.GetBytes(Convert.ToInt32(ConvertToUnixTimestamp(tick.LastTradeTime)));
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
        private DataSet RetrieveTicksFromDB()
        {
            //StringBuilder strIItokenBuilder = new StringBuilder();
            //foreach (UInt32 instrumentToken in instrumentTokens)
            //{
            //    strIItokenBuilder.AppendFormat("{0},", instrumentToken);
            //}
            //strIItokenBuilder.Remove(strIItokenBuilder.Length - 1, 1);

            
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetTickDataTest", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
           // selectCMD.Parameters.AddWithValue("@ITokenList", instrumentTokens);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsTicks = new DataSet();
            daInstruments.Fill(dsTicks);
            sqlConnection.Close();

           // Task<DataSet> ticks = new 

            return dsTicks;
        }
        public void ConvertToBinaries(DataSet ds)
        {

        }
    }
}
