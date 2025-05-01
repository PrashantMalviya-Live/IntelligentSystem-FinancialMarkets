using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBAccess
{
    public interface ITimeStreamDAO : IDAO
    {
        public OHLC GetPreviousDayRange(uint token, DateTime dateTime);
        public OHLC GetPriceRange(uint token, DateTime startDateTime, DateTime endDateTime);
        public DataSet GetHistoricalCandlePrices(int numberofCandles, DateTime endDateTime, string tokenList, TimeSpan timeFrame,
            bool isBaseInstrument = false, CandleType candleType = CandleType.Time, uint vThreshold = 0, uint bToken = 256265);
        public DataSet GetHistoricalClosePricesFromCandles(int numberofCandles, DateTime endDateTime, string tokenList, TimeSpan timeFrame);

        public Tick GetLastTick(uint instrumentToken);
        
        public int SaveCandle(object Arg, decimal closePrice, DateTime CloseTime, decimal? closeVolume, int? downTicks,
                decimal? highPrice, DateTime highTime, decimal? highVolume, uint instrumentToken,
                decimal? lowPrice, DateTime lowTime, decimal? lowVolume,
                int maxPriceLevelBuyCount, decimal maxPriceLevelBuyVolume, decimal maxPriceLevelMoney, decimal maxPriceLevelPrice,
                int maxPriceLevelSellCount, decimal maxPriceLevelSellVolume, decimal maxPriceLevelTotalVolume,
                int minPriceLevelBuyCount, decimal minPriceLevelBuyVolume, decimal minPriceLevelMoney, decimal minPriceLevelPrice,
                int minPriceLevelSellCount, decimal minPriceLevelSellVolume, decimal minPriceLevelTotalVolume,
                decimal? openInterest, decimal? openPrice, DateTime openTime, decimal? openVolume, decimal? relativeVolume,
                CandleStates candleState,
                decimal? totalPrice, int? totalTicks, decimal? totalVolume, int? upTicks, CandleType candleType);
        public DataSet LoadCandles(int numberofCandles, CandleType candleType, DateTime endDateTime, string instrumentTokenList, TimeSpan timeFrame);
        public void SaveCandlePriceLevels(int candleId, int candlePriceLevelBuyCount, decimal candlePriceLevelBuyVolume, decimal candlePriceLevelMoney, decimal candlePriceLevelPrice,
                int candlePriceLevelSellCount, decimal candlePriceLevelSellVolume, decimal candlePriceLevelTotalVolume);
        
        public decimal InsertOptionIV(uint token1, uint token2, decimal _baseInstrumentPrice, decimal? iv1, decimal lastPrice1, DateTime lastTradeTime1,
         decimal? iv2, decimal lastPrice2, DateTime lastTradeTime2);
        public void InsertHistoricals(Historical historical, int interval = 0);
        public List<Historical> GetHistoricals(uint token, DateTime fromDate, DateTime toDate, int interval = 0);
        public decimal RetrieveLastPrice(UInt32 token, DateTime? time = null, bool buyOrder = false);
        public DataSet GetDailyOHLC(IEnumerable<uint> tokens, DateTime date);
        public Task WriteTicksAsync(Queue<Tick> liveTicks);
        public void StoreTestTickData(List<Tick> liveTicks, bool shortenedTick);
        
        public void StoreData(LiveOHLCData liveOHLC);
        public Dictionary<uint, List<PriceTime>> GetTickData(uint btoken, DateTime expiry, decimal fromStrike, decimal toStrike, bool optionsData, bool futuresData, DateTime tradeDate);

    }
}
