using DataAccess;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GlobalLayer;

namespace DBAccess
{
    public interface IRDSDAO :IDAO
    {
        public List<string> GetActiveTradingDays(DateTime fromDate, DateTime toDate);
        public DateTime GetPreviousTradingDate(DateTime tradeDate, int numberOfDaysPrior, uint token);
        public DateTime GetPreviousWeeklyExpiry(DateTime tradeDate, int numberofWeeksPrior = 1);
        public DateTime GetCurrentMonthlyExpiry(DateTime tradeDate, uint binstrument);
        public DateTime GetCurrentWeeklyExpiry(DateTime tradeDate, uint binstrument);

        public string GetAccessToken();
        public DataSet GetAlertTriggersforUser(string userId);
        public DataSet GetActiveAlertTriggers();
        public DataSet GetAlertsbyAlertId(int alertId);
        public DataSet GetGeneratedAlerts(string userId);
        public Task<decimal> GetUserCreditsAsync(string userId);
        public DataSet GetInstrumentList();
        public DataSet GetCandleTimeFrames();
        public DataSet GetIndicatorList();
        public DataSet GetIndicatorProperties(int indicatorId);
        public Task<int> UpdateGeneratedAlertsAsync(Alert alert);
        public Task<int> UpdateAlertTriggerAsync(
            int id, uint instrumentToken, string tradingSymbol, string userid,
            DateTime setupdate, DateTime startDate, DateTime endDate, byte numberOfTriggersPerInterval,
            byte triggerFrequency, byte totalNumberOfTriggers, string alertCriteria);
        public bool UpdateUser(User activeUser);
        public void UpdateArg8(int algoInstance, decimal arg8);
        public void UpdateAlgoParamaters(int algoInstance, decimal upperLimit = 0, decimal lowerLimit = 0, decimal arg1 = 0, decimal arg2 = 0,
            decimal arg3 = 0, decimal arg4 = 0, decimal arg5 = 0, decimal arg6 = 0, decimal arg7 = 0, decimal arg8 = 0, string arg9 = "");
        public bool UpdateCandleScore(int algoInstance, DateTime candleTime, Decimal candlePrice, int score);
        public DataSet GetActiveUser(int brokerId = 0, string userid = "");
        public DataSet GetUserByApplicationUserId(string userid, int brokerId);
        public DataSet GetActiveApplicationUser(string userid = "");
        public int GenerateAlgoInstance(AlgoIndex algoIndex, uint bToken, DateTime timeStamp, DateTime expiry,
            int initialQtyInLotsSize, int maxQtyInLotSize = 0, int stepQtyInLotSize = 0, decimal upperLimit = 0,
            decimal upperLimitPercent = 0, decimal lowerLimit = 0, decimal lowerLimitPercent = 0,
            float stopLossPoints = 0, int optionType = 0, int optionIndex = 0, float candleTimeFrameInMins = 5,
            CandleType candleType = CandleType.Time, decimal arg1 = 0, decimal arg2 = 0,
            decimal arg3 = 0, decimal arg4 = 0, decimal arg5 = 0, decimal arg6 = 0, decimal arg7 = 0,
            decimal arg8 = 0, string arg9 = "", bool positionSizing = false, decimal maxLossPerTrade = 0);

        public DataSet RetrieveActiveData(AlgoIndex algoIndex);
        public DataSet GetActiveAlgos(AlgoIndex algoIndex, string userId);
        public DataSet GetInstrument(uint instrumentToken, DateTime? expiry);
        public DataSet LoadAlgoInputs(AlgoIndex algoindex, DateTime fromDate, DateTime toDate);
        public DataSet GetInstrument(string tradingSymbol);
        public DataSet GetInstrument(DateTime? expiry, uint bToken, decimal strike, string instrumentType);
        public DataSet GetOTMInstrument(DateTime? expiry, uint bToken, decimal strike, string instrumentType);
        public DataSet GetOrders(AlgoIndex algoindex, int orderid = 0);
        public DataSet LoadInstruments(AlgoIndex algoIndex, DateTime? expiry, uint baseInstrumentToken, decimal baseInstrumentPrice = 0);
        //,decimal FromStrikePrice = 0, decimal ToStrikePrice = 0, InstrumentType instrumentType = InstrumentType.ALL)
        public DataSet LoadSpreadOptions(DateTime? nearExpiry, DateTime? farExpiry,
            uint baseInstrumentToken, string strikeList);
        public DataSet LoadIndexStocks(uint indexToken);
        public DataSet LoadOptions(DateTime? expiry, uint baseInstrumentToken, decimal baseInstrumentPrice = 0,
            decimal maxDistanceFromBInstrument = 500);
        public DataSet LoadCloseByOptions(DateTime? expiry, uint baseInstrumentToken, decimal baseInstrumentPrice = 0,
            decimal maxDistanceFromBInstrument = 500);
        public DataSet LoadCloseByStraddleOptions(DateTime? expiry, uint baseInstrumentToken, decimal baseInstrumentPrice = 0,
            decimal maxDistanceFromBInstrument = 500);
        public DataSet LoadCloseByExpiryOptions(uint baseInstrumentToken, decimal baseInstrumentPrice,
            decimal maxDistanceFromBInstrument, DateTime currentDate);
        public void UpdateAlgoPnl(int algoInstance, decimal pnl);
        public void DeActivateAlgo(int algoInstance);
        public void DeActivateOrderTrio(OrderTrio orderTrio);
        public decimal UpdateOrder(Order order, int algoInstance);
        public decimal UpdateTrade(int strategyID, UInt32 instrumentToken, decimal averageTradePrice, DateTime? exchangeTimeStamp,
            string orderId, int qty, string transactionType, AlgoIndex algoIndex, int tradedLot = 0, int triggerID = 0);
        public DataSet LoadHistoricalTokenVolume(AlgoIndex algoIndex, DateTime startTime,
            DateTime endTime, TimeSpan candleTimeSpan, int numberOfCandles);
        public DataSet GetInstruments(string strikePrices, string instrumentTypes);
        public DataSet RetrieveActiveStrangleData(AlgoIndex algoIndex);
        public DataSet RetrieveActivePivotInstruments(AlgoIndex algoIndex);
        public DataSet TestRetrieveActivePivotInstruments(AlgoIndex algoIndex);

        public int StoreBoxData(int boxID, UInt32 firstCallToken, UInt32 secondCallToken, UInt32 firstPutToken, UInt32 secondPutToken,
           decimal firstCallPrice, decimal secondCallPrice, decimal firstPutPrice, decimal secondPutPrice, decimal boxValue);
        public DataSet RetrieveBoxData(int boxID);
        public DataSet RetrieveActiveStrangles(AlgoIndex algoIndex);

        public List<Instrument> RetrieveBaseInstruments();
        public SortedList<Decimal, Instrument>[] RetrieveNextStrangleNodes(UInt32 baseInstrumentToken, DateTime expiry,
                   decimal callStrikePrice, decimal putStrikePrice, int updownboth);
        public SortedList<Decimal, Instrument>[] RetrieveNextStrangleNodes(UInt32 baseInstrumentToken, DateTime expiry,
            decimal callStrikePrice, decimal putStrikePrice, int updownboth, out Dictionary<uint, uint> mappedTokens);
        public DataSet RetrieveNextNodes(UInt32 baseInstrumentToken, string instrumentType,
          decimal currentStrikePrice, DateTime expiry, int updownboth, int noOfRecords = 5);
        public List<string> RetrieveOptionExpiries(UInt32 baseInstrumentToken);
        public List<Instrument> RetrieveOptions(UInt32 baseInstrumentToken, DateTime expiryDate);
        public int StoreStrangleInstrumentList(int strategyID = 0, int callToken = 0,
            int putToken = 0, int callIndex = 0, int putIndex = 0);
        public Dictionary<UInt32, String> GetInstrumentListToSubscribe(decimal niftyPrice, decimal bankniftyPrice,
            decimal finniftyPrice, decimal midcpniftyPrice);
        public List<UInt32> GetInstrumentTokens(uint indexToken, decimal fromStrike, decimal toStrike, DateTime expiry);
        public void StoreInstrumentList(List<Instrument> instruments);
        public DataSet GetLHSSubMenu(Int16 menuID, short cityID);
        public int UpdateStrangleData(UInt32 ceToken, UInt32 peToken, decimal cePrice, decimal pePrice, decimal bInstPrice, AlgoIndex algoIndex,
            decimal safetyMargin = 10, double stopLossPoints = 0, int initialQty = 0, int maxQty = 0, int stepQty = 0, string ceOrderId = "", string peOrderId = "", string transactionType = "");
        public int StoreStrategyData(string tokens, string prices, string qtys, string tags, string transactiontypes, string orderIds, string optionTypes,
            AlgoIndex algoIndex, UInt32 parentToken = 0, decimal parentPrice = 0, int stoplosspoints = 0, int initialQty = 0, int maxQty = 0, int stepQty = 0,
            decimal upperThreshold = 0, decimal lowerThreshold = 0, bool thresholdinpercent = false, string triggerIds = "");
        public int CreateOptionStrategy(AlgoIndex algoIndex, UInt32 parentToken = 0, int stoplosspoints = 0,
            int initialQty = 0, int maxQty = 0, int stepQty = 0,
            decimal upperThreshold = 0, decimal lowerThreshold = 0, bool thresholdinpercent = false);
        public int StoreStrategyData(string tokens, string prices, string qtys, string tags, string transactiontypes, string orderIds,
            AlgoIndex algoIndex, UInt32 parentToken = 0, decimal parentPrice = 0, int stoplosspoints = 0, int initialQty = 0, int maxQty = 0, int stepQty = 0,
            decimal upperThreshold = 0, decimal lowerThreshold = 0, bool thresholdinpercent = false);
        public int UpdateStrangleData(UInt32 ceToken, UInt32 peToken, decimal cePrice, decimal pePrice,
            AlgoIndex algoIndex, decimal ceMaxProfitPoints = 0, decimal ceMaxLossPoints = 0,
            decimal peMaxProfitPoints = 0, decimal peMaxLossPoints = 0, decimal ceMaxProfitPercent = 0,
            decimal ceMaxLossPercent = 0, decimal peMaxProfitPercent = 0, decimal peMaxLossPercent = 0,
            double stopLossPoints = 0);
        public int StoreIndexForMainPain(uint bToken, DateTime expiry, int strikePriceIncrement, AlgoIndex algoIndex,
            int tradingQty, decimal maxLossThreshold, decimal profitTarget, DateTime timeOfOrder);
        public int StorePivotInstruments(UInt32 primaryInstrumentToken, DateTime expiry,
            AlgoIndex algoIndex, int maxlot, int lotunit, PivotFrequency pivotFrequency, int pivotWindow);
        public int UpdateStrangleData(UInt32 ceToken, UInt32 peToken, decimal cePrice, decimal pePrice,
        AlgoIndex algoIndex, double ceLowerDelta = 0, double ceUpperDelta = 0, double peLowerDelta = 0,
        double peUpperDelta = 0, double stopLossPoints = 0, decimal baseInstrumentPrice = 0);
        public void UpdateListData(int strangleId, UInt32 currentCallToken, UInt32 currentPutToken, UInt32 prevCallToken, UInt32 prevPutToken,
           int currentIndex, int prevIndex, decimal[] currentSellPrice, decimal[] prevBuyPrice, AlgoIndex algoIndex, decimal bInstPrice);
        public void UpdateListData(int strangleId, UInt32 instrumentToken, int instrumentIndex, int previousInstrumentIndex, string instrumentType,
            UInt32 previousInstrumentToken, decimal currentNodePrice, decimal previousNodePrice, AlgoIndex algoIndex);
        public void UpdateOptionData(int strangleId, UInt32 instrumentToken, string instrumentType,
           decimal lastPrice, decimal currentPrice, AlgoIndex algoIndex);
        public int UpdateOrderTrio(uint optionToken, string mainOrderId, string slOrderId, string tpOrderId, decimal targetprofit, decimal stoploss,
             DateTime entryTradeTime, decimal baseInstrumentSL, bool tpflag, int algoInstance, bool isActive, int orderTrioId = 0, bool? slflag = null, string indicatorsValue = "");



    }
}
