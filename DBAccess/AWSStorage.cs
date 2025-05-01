using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using GlobalLayer;
using InfluxDB.Client.Core;
using Parquet;
using Parquet.Data;
using Parquet.Schema;



namespace DBAccess
{
    public class AWSStorage
    {
        private const string bucketName = "marketticksdata";
        //private const string region = "Asia Pacific (Mumbai) ap-south-1";
        //private static readonly RegionEndpoint bucketRegion = RegionEndpoint.GetBySystemName(region);
        private static readonly RegionEndpoint bucketRegion = RegionEndpoint.APSouth1;
        private static IAmazonS3 s3Client;

        public AWSStorage()
        {
            s3Client = new AmazonS3Client(bucketRegion);
        }

        //public static async Task Main(string[] args)
        //{
        //    s3Client = new AmazonS3Client(bucketRegion);
        //    var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        //    var s3Key = $"raw/dt={today}/ticks_{Guid.NewGuid()}.parquet";

        //    using var parquetStream = GenerateTickParquetStream(1000);
        //    await UploadToS3Async(parquetStream, s3Key);

        //    Console.WriteLine($"✅ Uploaded Parquet tick data to s3://{bucketName}/{s3Key}");
        //}
        DataTable ConvertObjectToDataTable(Queue<Tick> liveTicks, bool shortenedTick = false)
        {
            uint NIFTY_TOKEN = 256265;
            uint BANK_NIFTY_TOKEN = 260105;
            uint VIX_TOKEN = 264969;

            DataTable dtTicks = new DataTable("Ticks");
            dtTicks.Columns.Add("InstrumentToken", typeof(Int64));
            dtTicks.Columns.Add("LastPrice", typeof(Decimal));
            dtTicks.Columns.Add("LastQuantity", typeof(Int64));
            dtTicks.Columns.Add("AveragePrice", typeof(Decimal));
            dtTicks.Columns.Add("Volume", typeof(Int64));
            dtTicks.Columns.Add("BuyQuantity", typeof(Int64));
            dtTicks.Columns.Add("SellQuantity", typeof(Int64));
            dtTicks.Columns.Add("Open", typeof(Decimal));
            dtTicks.Columns.Add("High", typeof(Decimal));
            dtTicks.Columns.Add("Low", typeof(Decimal));
            dtTicks.Columns.Add("Close", typeof(Decimal));
            dtTicks.Columns.Add("Change", typeof(Decimal));
            dtTicks.Columns.Add("LastTradeTime", typeof(DateTime));
            dtTicks.Columns.Add("BackLogs", typeof(String));
            dtTicks.Columns.Add("OI", typeof(Int32));
            dtTicks.Columns.Add("OIDayHigh", typeof(Int32));
            dtTicks.Columns.Add("OIDayLow", typeof(Int32));
            dtTicks.Columns.Add("TimeStamp", typeof(DateTime));

            foreach (Tick tick in liveTicks)
            {
                if (tick.InstrumentToken == 0)
                {
                    continue;
                }
                DataRow drTick = dtTicks.NewRow();

                drTick["InstrumentToken"] = tick.InstrumentToken;
                drTick["LastPrice"] = tick.LastPrice;
                drTick["Volume"] = tick.Volume;
                drTick["OI"] = tick.OI;

                if (tick.InstrumentToken == Convert.ToUInt32(Constants.BANK_NIFTY_TOKEN)
                    || tick.InstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN)
                    || tick.InstrumentToken == Convert.ToUInt32(Constants.FINNIFTY_TOKEN)
                    || tick.InstrumentToken == Convert.ToUInt32(Constants.MIDCPNIFTY_TOKEN)
                    || tick.InstrumentToken == VIX_TOKEN)
                {
                    tick.LastTradeTime = tick.Timestamp;
                }

                if (tick.LastTradeTime != null)
                {
                    drTick["LastTradeTime"] = tick.LastTradeTime;
                }
                else
                {
                    drTick["LastTradeTime"] = DBNull.Value;
                }
                if (tick.Timestamp != null)
                {
                    drTick["TimeStamp"] = tick.Timestamp;
                }
                else
                {
                    drTick["TimeStamp"] = DBNull.Value;
                }



                if (!shortenedTick)
                {
                    //DATA STOPPED TO REDUCE DB SPACE. THIS CAN BE RE ENABLED LATER.
                    drTick["LastQuantity"] = tick.LastQuantity;
                    drTick["AveragePrice"] = tick.AveragePrice;
                    drTick["BuyQuantity"] = tick.BuyQuantity;
                    drTick["SellQuantity"] = tick.SellQuantity;
                    drTick["Change"] = tick.Change;
                    drTick["BackLogs"] = GetBacklog(tick);
                    drTick["Open"] = tick.Open;
                    drTick["High"] = tick.High;
                    drTick["Low"] = tick.Low;
                    drTick["Close"] = tick.Close;
                    drTick["OIDayHigh"] = tick.OIDayHigh;
                    drTick["OIDayLow"] = tick.OIDayLow;
                }

                dtTicks.Rows.Add(drTick);
            }

            return dtTicks;
        }
        /// <summary>
        /// -- DepthLevel,B1,BV1,BQ1,A1,AV1,AQ1|DepthLevel,B2,BV2,BQ2,A2,AV2,AQ2|...
        /// </summary>
        /// <param name="tick"></param>
        /// <returns></returns>
        private string GetBacklog(Tick tick)
        {
            StringBuilder backlog = new StringBuilder();

            ///TODO: there can be asks and no bid. Correct this below
            if (tick.Bids != null)
            {
                for (int i = 0; i < tick.Bids.Length; i++)
                {
                    DepthItem bidBacklog = tick.Bids[i];
                    DepthItem askBacklog = tick.Offers[i];

                    backlog.AppendFormat("{0},{1},{2},{3},{4},{5},{6}|", i + 1, bidBacklog.Price, bidBacklog.Orders,
                        bidBacklog.Quantity, askBacklog.Price, askBacklog.Orders, askBacklog.Quantity);
                }
            }
            return backlog.ToString();
        }

        private ParquetSchema CreateParquetSchemaFromDataTable(DataTable table)
        {
            List<Field> fields = new List<Field>();
            foreach (System.Data.DataColumn col in table.Columns)
            {
                Field field = MapToParquetField(col);
                fields.Add(field);
            }

            return new ParquetSchema(fields);
        }

        private Field MapToParquetField(System.Data.DataColumn column)
        {
            string name = column.ColumnName;
            Type type = column.DataType;

            if (type == typeof(int))
                return new DataField<int>(name);
            else if (type == typeof(long))
                return new DataField<long>(name);
            else if (type == typeof(float))
                return new DataField<float>(name);
            else if (type == typeof(double))
                return new DataField<double>(name);
            else if (type == typeof(bool))
                return new DataField<bool>(name);
            else if (type == typeof(string))
                return new DataField<string>(name);
            else if (type == typeof(DateTime))
                return new DataField<DateTime>(name);
            else if (type == typeof(uint))
                return new DataField<Int64>(name);
            else if (type == typeof(decimal))
                return new DataField<decimal>(name);
            else
                return new DataField<string>(name); // Fallback for unsupported types
        }
        public async Task StoreTickDataAsync(Queue<Tick> ticks)
        {
            DataTable dtLiveTicks = ConvertObjectToDataTable(ticks, shortenedTick:false);

            var schema = CreateParquetSchemaFromDataTable(dtLiveTicks);

            var stream = new MemoryStream();
            using (ParquetWriter writer = await ParquetWriter.CreateAsync(schema, stream))
            {
                writer.CompressionMethod = CompressionMethod.Snappy;
                using (ParquetRowGroupWriter groupWriter = writer.CreateRowGroup())
                {
                    for(int i = 0; i < dtLiveTicks.Columns.Count; i++)
                    {
                        object[] columnValues = dtLiveTicks.AsEnumerable()
                                 .Select(row => row[i])
                                 .ToArray();

                        //col.ExtendedProperties.CopyTo(array,0);
                        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn((DataField) schema[i], columnValues));
                    }
                }
            }
            stream.Position = 0;
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var s3Key = $"raw/dt={today}/ticks_{Guid.NewGuid()}.parquet";
            await UploadToS3Async(stream, s3Key);
        }

        //private static async Task<MemoryStream> GenerateTickParquetStream(int instrumentCount)
        //{
        //    var instrumentIds = new List<string>();
        //    var timestamps = new List<DateTime>();
        //    var prices = new List<double>();

        //    var random = new Random();
        //    var timestamp = DateTime.UtcNow;

        //    for (int i = 1; i <= instrumentCount; i++)
        //    {
        //        instrumentIds.Add($"SYM{i:0000}");
        //        timestamps.Add(timestamp);
        //        prices.Add(50 + random.NextDouble() * 100); // random price
        //    }

        //    var schema = new ParquetSchema(
        //        new DataField<string>("instrument_id"),
        //        new DataField<DateTime>("timestamp"),
        //        new DataField<double>("price")
        //    );

        //    var table = new DataTable(schema)
        //    {
        //        Columns =
        //    {
        //        instrumentIds.ToArray(),
        //        timestamps.ToArray(),
        //        prices.ToArray()
        //    }
        //    };
        //    ParquetOptions? formatOptions = null;
        //    bool append = false;

        //    var stream = new MemoryStream();
        //    using (var writer = await ParquetWriter.CreateAsync(schema, stream))
        //    {
        //        writer.CompressionMethod = CompressionMethod.Snappy;
        //        using var rowGroupWriter = writer.CreateRowGroup();
        //        foreach (var col in table.Columns)
        //        {
        //            rowGroupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.Fields[table.Columns.IndexOf(col)], col)).Wait();
        //        }
        //    }

        //    stream.Position = 0;
        //    return stream;
        //}

        private static async Task UploadToS3Async(Stream stream, string key)
        {
            var transferUtility = new TransferUtility(s3Client);
            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = stream,
                Key = key,
                BucketName = bucketName,
                ContentType = "application/octet-stream"
            };

            await transferUtility.UploadAsync(uploadRequest);
        }
    }
}
