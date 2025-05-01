using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Amazon;
using Amazon.TimestreamWrite;
using Amazon.TimestreamWrite.Model;
using GlobalLayer;
using Newtonsoft.Json.Linq;
using DBAccess;
namespace DataAccess
{
    public class AWSTimestreamdb : ITimeStreamDAO
    {
        private const string DatabaseName = "LiveMarketTicks";


        private readonly AmazonTimestreamWriteClient _client;

        public AWSTimestreamdb()
        {
            _client = new AmazonTimestreamWriteClient(RegionEndpoint.APSouth1); // Choose appropriate region
        }

        public async Task WriteTicksAsync(Queue<Tick> ticks)
        {
            const string TableName = "Ticks";

            var records = new List<Record>();

            while (ticks.Count > 0)
            {
                var tick = ticks.Dequeue();

                if (!tick.Timestamp.HasValue) continue;

                var dimensions = new List<Dimension>
        {
            new Dimension { Name = "Symbol", Value = tick.Symbol },
            new Dimension { Name = "InstrumentToken", Value = tick.InstrumentToken.ToString() },
            //new Dimension { Name = "Mode", Value = tick.Mode },
            //new Dimension { Name = "Tradable", Value = tick.Tradable.ToString() }
        };

                string timestampMillis = DateTime.SpecifyKind(tick.Timestamp.Value, DateTimeKind.Utc)
                    .Subtract(new DateTime(1970, 1, 1))
                    .TotalMilliseconds.ToString("F0");

                records.AddRange(new[]
                {
            CreateMeasure("LastPrice", tick.LastPrice, timestampMillis, dimensions),
            CreateMeasure("LastQuantity", tick.LastQuantity, timestampMillis, dimensions),
            CreateMeasure("AveragePrice", tick.AveragePrice, timestampMillis, dimensions),
            CreateMeasure("Volume", tick.Volume, timestampMillis, dimensions),
            CreateMeasure("BuyQuantity", tick.BuyQuantity, timestampMillis, dimensions),
            CreateMeasure("SellQuantity", tick.SellQuantity, timestampMillis, dimensions),
            CreateMeasure("Open", tick.Open, timestampMillis, dimensions),
            CreateMeasure("High", tick.High, timestampMillis, dimensions),
            CreateMeasure("Low", tick.Low, timestampMillis, dimensions),
            CreateMeasure("Close", tick.Close, timestampMillis, dimensions),
            CreateMeasure("Change", tick.Change, timestampMillis, dimensions),
            CreateMeasure("OI", tick.OI, timestampMillis, dimensions),
            CreateMeasure("OIDayHigh", tick.OIDayHigh, timestampMillis, dimensions),
            CreateMeasure("OIDayLow", tick.OIDayLow, timestampMillis, dimensions)
        });

                if (tick.Bids != null)
                    records.Add(CreateMeasure("BidCount", tick.Bids.Length, timestampMillis, dimensions));
                if (tick.Offers != null)
                    records.Add(CreateMeasure("OfferCount", tick.Offers.Length, timestampMillis, dimensions));
            }

            var request = new WriteRecordsRequest
            {
                DatabaseName = DatabaseName,
                TableName = TableName,
                Records = records
            };

            try
            {
                var response = await _client.WriteRecordsAsync(request);
                Console.WriteLine($"Wrote {records.Count} records. Status: {response.HttpStatusCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Timestream write failed: {ex.Message}");
            }
        }


        private Record CreateMeasure(string name, object value, string time, List<Dimension> dimensions)
        {
            if (value == null) return null;

            return new Record
            {
                Dimensions = dimensions,
                MeasureName = name,
                MeasureValue = Convert.ToString(value),
                MeasureValueType = value is float or double or decimal ? MeasureValueType.DOUBLE : MeasureValueType.BIGINT,
                Time = time,
                TimeUnit = TimeUnit.MILLISECONDS
            };
        }

        public int SaveCandle(object Arg, decimal closePrice, DateTime CloseTime, decimal? closeVolume, int? downTicks,
                decimal? highPrice, DateTime highTime, decimal? highVolume, uint instrumentToken,
                decimal? lowPrice, DateTime lowTime, decimal? lowVolume,
                int maxPriceLevelBuyCount, decimal maxPriceLevelBuyVolume, decimal maxPriceLevelMoney, decimal maxPriceLevelPrice,
                int maxPriceLevelSellCount, decimal maxPriceLevelSellVolume, decimal maxPriceLevelTotalVolume,
                int minPriceLevelBuyCount, decimal minPriceLevelBuyVolume, decimal minPriceLevelMoney, decimal minPriceLevelPrice,
                int minPriceLevelSellCount, decimal minPriceLevelSellVolume, decimal minPriceLevelTotalVolume,
                decimal? openInterest, decimal? openPrice, DateTime openTime, decimal? openVolume, decimal? relativeVolume,
                CandleStates candleState,
                decimal? totalPrice, int? totalTicks, decimal? totalVolume, int? upTicks, CandleType candleType)
        {
            throw new NotImplementedException();
        }

        public void SaveCandlePriceLevels(int candleId, int candlePriceLevelBuyCount, decimal candlePriceLevelBuyVolume, decimal candlePriceLevelMoney, decimal candlePriceLevelPrice,
               int candlePriceLevelSellCount, decimal candlePriceLevelSellVolume, decimal candlePriceLevelTotalVolume)
        {
            throw new NotImplementedException();
        }

        OHLC ITimeStreamDAO.GetPreviousDayRange(uint token, DateTime dateTime)
        {
            throw new NotImplementedException();
        }

        OHLC ITimeStreamDAO.GetPriceRange(uint token, DateTime startDateTime, DateTime endDateTime)
        {
            throw new NotImplementedException();
        }

        DataSet ITimeStreamDAO.GetHistoricalCandlePrices(int numberofCandles, DateTime endDateTime, string tokenList, TimeSpan timeFrame, bool isBaseInstrument, CandleType candleType, uint vThreshold, uint bToken)
        {
            throw new NotImplementedException();
        }

        DataSet ITimeStreamDAO.GetHistoricalClosePricesFromCandles(int numberofCandles, DateTime endDateTime, string tokenList, TimeSpan timeFrame)
        {
            throw new NotImplementedException();
        }

        Tick ITimeStreamDAO.GetLastTick(uint instrumentToken)
        {
            throw new NotImplementedException();
        }

        DataSet ITimeStreamDAO.LoadCandles(int numberofCandles, CandleType candleType, DateTime endDateTime, string instrumentTokenList, TimeSpan timeFrame)
        {
            throw new NotImplementedException();
        }

        decimal ITimeStreamDAO.InsertOptionIV(uint token1, uint token2, decimal _baseInstrumentPrice, decimal? iv1, decimal lastPrice1, DateTime lastTradeTime1, decimal? iv2, decimal lastPrice2, DateTime lastTradeTime2)
        {
            throw new NotImplementedException();
        }

        void ITimeStreamDAO.InsertHistoricals(Historical historical, int interval)
        {
            throw new NotImplementedException();
        }

        List<Historical> ITimeStreamDAO.GetHistoricals(uint token, DateTime fromDate, DateTime toDate, int interval)
        {
            throw new NotImplementedException();
        }

        decimal ITimeStreamDAO.RetrieveLastPrice(uint token, DateTime? time, bool buyOrder)
        {
            throw new NotImplementedException();
        }

        DataSet ITimeStreamDAO.GetDailyOHLC(IEnumerable<uint> tokens, DateTime date)
        {
            throw new NotImplementedException();
        }

        void ITimeStreamDAO.StoreTestTickData(List<Tick> liveTicks, bool shortenedTick)
        {
            throw new NotImplementedException();
        }

        void ITimeStreamDAO.StoreData(LiveOHLCData liveOHLC)
        {
            throw new NotImplementedException();
        }

        Dictionary<uint, List<PriceTime>> ITimeStreamDAO.GetTickData(uint btoken, DateTime expiry, decimal fromStrike, decimal toStrike, bool optionsData, bool futuresData, DateTime tradeDate)
        {
            throw new NotImplementedException();
        }
    }
}

