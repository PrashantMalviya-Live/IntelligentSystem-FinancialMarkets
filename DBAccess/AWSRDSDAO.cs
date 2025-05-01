using System;
using System.Threading.Tasks;
using MySqlConnector;
using System.Data;
using Microsoft.Extensions.Configuration;
using GlobalLayer;
using System.Collections.Generic;
using DBAccess;

namespace DataAccess
{
    public class AWSRDSDAO : IRDSDAO
    {
        private readonly string _connectionString;

        public AWSRDSDAO(string connectionString)
        {
            _connectionString = connectionString;
        }
        public DataSet GetActiveUser(int brokerId = 0, string userid = "")
        {
            MySqlConnection sqlConnection = new MySqlConnection(_connectionString);
            MySqlCommand selectCMD = new MySqlCommand("GetActiveUser", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("BrokerId", brokerId);
            selectCMD.Parameters.AddWithValue("InUserId", userid);
            MySqlDataAdapter daInstruments = new MySqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsUser = new DataSet();
            daInstruments.Fill(dsUser);
            sqlConnection.Close();

            return dsUser;
        }

        int IRDSDAO.CreateOptionStrategy(AlgoIndex algoIndex, uint parentToken, int stoplosspoints, int initialQty, int maxQty, int stepQty, decimal upperThreshold, decimal lowerThreshold, bool thresholdinpercent)
        {
            throw new NotImplementedException();
        }

        void IRDSDAO.DeActivateAlgo(int algoInstance)
        {
            throw new NotImplementedException();
        }

        void IRDSDAO.DeActivateOrderTrio(OrderTrio orderTrio)
        {
            throw new NotImplementedException();
        }

        int IRDSDAO.GenerateAlgoInstance(AlgoIndex algoIndex, uint bToken, DateTime timeStamp, DateTime expiry, int initialQtyInLotsSize, int maxQtyInLotSize, int stepQtyInLotSize, decimal upperLimit, decimal upperLimitPercent, decimal lowerLimit, decimal lowerLimitPercent, float stopLossPoints, int optionType, int optionIndex, float candleTimeFrameInMins, CandleType candleType, decimal arg1, decimal arg2, decimal arg3, decimal arg4, decimal arg5, decimal arg6, decimal arg7, decimal arg8, string arg9, bool positionSizing, decimal maxLossPerTrade)
        {
            throw new NotImplementedException();
        }

        string IRDSDAO.GetAccessToken()
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetActiveAlertTriggers()
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetActiveAlgos(AlgoIndex algoIndex, string userId)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetActiveApplicationUser(string userid)
        {
            throw new NotImplementedException();
        }

        List<string> IRDSDAO.GetActiveTradingDays(DateTime fromDate, DateTime toDate)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetAlertsbyAlertId(int alertId)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetAlertTriggersforUser(string userId)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetCandleTimeFrames()
        {
            throw new NotImplementedException();
        }

        DateTime IRDSDAO.GetCurrentMonthlyExpiry(DateTime tradeDate, uint binstrument)
        {
            throw new NotImplementedException();
        }

        DateTime IRDSDAO.GetCurrentWeeklyExpiry(DateTime tradeDate, uint binstrument)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetGeneratedAlerts(string userId)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetIndicatorList()
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetIndicatorProperties(int indicatorId)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetInstrument(uint instrumentToken, DateTime? expiry)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetInstrument(string tradingSymbol)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetInstrument(DateTime? expiry, uint bToken, decimal strike, string instrumentType)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetInstrumentList()
        {
            throw new NotImplementedException();
        }

        Dictionary<uint, string> IRDSDAO.GetInstrumentListToSubscribe(decimal niftyPrice, decimal bankniftyPrice, decimal finniftyPrice, decimal midcpniftyPrice)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetInstruments(string strikePrices, string instrumentTypes)
        {
            throw new NotImplementedException();
        }

        List<uint> IRDSDAO.GetInstrumentTokens(uint indexToken, decimal fromStrike, decimal toStrike, DateTime expiry)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetLHSSubMenu(short menuID, short cityID)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetOrders(AlgoIndex algoindex, int orderid)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetOTMInstrument(DateTime? expiry, uint bToken, decimal strike, string instrumentType)
        {
            throw new NotImplementedException();
        }

        DateTime IRDSDAO.GetPreviousTradingDate(DateTime tradeDate, int numberOfDaysPrior, uint token)
        {
            throw new NotImplementedException();
        }

        DateTime IRDSDAO.GetPreviousWeeklyExpiry(DateTime tradeDate, int numberofWeeksPrior)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.GetUserByApplicationUserId(string userid, int brokerId)
        {
            throw new NotImplementedException();
        }

        Task<decimal> IRDSDAO.GetUserCreditsAsync(string userId)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.LoadAlgoInputs(AlgoIndex algoindex, DateTime fromDate, DateTime toDate)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.LoadCloseByExpiryOptions(uint baseInstrumentToken, decimal baseInstrumentPrice, decimal maxDistanceFromBInstrument, DateTime currentDate)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.LoadCloseByOptions(DateTime? expiry, uint baseInstrumentToken, decimal baseInstrumentPrice, decimal maxDistanceFromBInstrument)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.LoadCloseByStraddleOptions(DateTime? expiry, uint baseInstrumentToken, decimal baseInstrumentPrice, decimal maxDistanceFromBInstrument)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.LoadHistoricalTokenVolume(AlgoIndex algoIndex, DateTime startTime, DateTime endTime, TimeSpan candleTimeSpan, int numberOfCandles)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.LoadIndexStocks(uint indexToken)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.LoadInstruments(AlgoIndex algoIndex, DateTime? expiry, uint baseInstrumentToken, decimal baseInstrumentPrice)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.LoadOptions(DateTime? expiry, uint baseInstrumentToken, decimal baseInstrumentPrice, decimal maxDistanceFromBInstrument)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.LoadSpreadOptions(DateTime? nearExpiry, DateTime? farExpiry, uint baseInstrumentToken, string strikeList)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.RetrieveActiveData(AlgoIndex algoIndex)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.RetrieveActivePivotInstruments(AlgoIndex algoIndex)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.RetrieveActiveStrangleData(AlgoIndex algoIndex)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.RetrieveActiveStrangles(AlgoIndex algoIndex)
        {
            throw new NotImplementedException();
        }

        List<Instrument> IRDSDAO.RetrieveBaseInstruments()
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.RetrieveBoxData(int boxID)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.RetrieveNextNodes(uint baseInstrumentToken, string instrumentType, decimal currentStrikePrice, DateTime expiry, int updownboth, int noOfRecords)
        {
            throw new NotImplementedException();
        }

        SortedList<decimal, Instrument>[] IRDSDAO.RetrieveNextStrangleNodes(uint baseInstrumentToken, DateTime expiry, decimal callStrikePrice, decimal putStrikePrice, int updownboth)
        {
            throw new NotImplementedException();
        }

        SortedList<decimal, Instrument>[] IRDSDAO.RetrieveNextStrangleNodes(uint baseInstrumentToken, DateTime expiry, decimal callStrikePrice, decimal putStrikePrice, int updownboth, out Dictionary<uint, uint> mappedTokens)
        {
            throw new NotImplementedException();
        }

        List<string> IRDSDAO.RetrieveOptionExpiries(uint baseInstrumentToken)
        {
            throw new NotImplementedException();
        }

        List<Instrument> IRDSDAO.RetrieveOptions(uint baseInstrumentToken, DateTime expiryDate)
        {
            throw new NotImplementedException();
        }

        int IRDSDAO.StoreBoxData(int boxID, uint firstCallToken, uint secondCallToken, uint firstPutToken, uint secondPutToken, decimal firstCallPrice, decimal secondCallPrice, decimal firstPutPrice, decimal secondPutPrice, decimal boxValue)
        {
            throw new NotImplementedException();
        }

        int IRDSDAO.StoreIndexForMainPain(uint bToken, DateTime expiry, int strikePriceIncrement, AlgoIndex algoIndex, int tradingQty, decimal maxLossThreshold, decimal profitTarget, DateTime timeOfOrder)
        {
            throw new NotImplementedException();
        }

        void IRDSDAO.StoreInstrumentList(List<Instrument> instruments)
        {
            throw new NotImplementedException();
        }

        int IRDSDAO.StorePivotInstruments(uint primaryInstrumentToken, DateTime expiry, AlgoIndex algoIndex, int maxlot, int lotunit, PivotFrequency pivotFrequency, int pivotWindow)
        {
            throw new NotImplementedException();
        }

        int IRDSDAO.StoreStrangleInstrumentList(int strategyID, int callToken, int putToken, int callIndex, int putIndex)
        {
            throw new NotImplementedException();
        }

        int IRDSDAO.StoreStrategyData(string tokens, string prices, string qtys, string tags, string transactiontypes, string orderIds, string optionTypes, AlgoIndex algoIndex, uint parentToken, decimal parentPrice, int stoplosspoints, int initialQty, int maxQty, int stepQty, decimal upperThreshold, decimal lowerThreshold, bool thresholdinpercent, string triggerIds)
        {
            throw new NotImplementedException();
        }

        int IRDSDAO.StoreStrategyData(string tokens, string prices, string qtys, string tags, string transactiontypes, string orderIds, AlgoIndex algoIndex, uint parentToken, decimal parentPrice, int stoplosspoints, int initialQty, int maxQty, int stepQty, decimal upperThreshold, decimal lowerThreshold, bool thresholdinpercent)
        {
            throw new NotImplementedException();
        }

        DataSet IRDSDAO.TestRetrieveActivePivotInstruments(AlgoIndex algoIndex)
        {
            throw new NotImplementedException();
        }

        Task<int> IRDSDAO.UpdateAlertTriggerAsync(int id, uint instrumentToken, string tradingSymbol, string userid, DateTime setupdate, DateTime startDate, DateTime endDate, byte numberOfTriggersPerInterval, byte triggerFrequency, byte totalNumberOfTriggers, string alertCriteria)
        {
            throw new NotImplementedException();
        }

        void IRDSDAO.UpdateAlgoParamaters(int algoInstance, decimal upperLimit, decimal lowerLimit, decimal arg1, decimal arg2, decimal arg3, decimal arg4, decimal arg5, decimal arg6, decimal arg7, decimal arg8, string arg9)
        {
            throw new NotImplementedException();
        }

        void IRDSDAO.UpdateAlgoPnl(int algoInstance, decimal pnl)
        {
            throw new NotImplementedException();
        }

        void IRDSDAO.UpdateArg8(int algoInstance, decimal arg8)
        {
            throw new NotImplementedException();
        }

        bool IRDSDAO.UpdateCandleScore(int algoInstance, DateTime candleTime, decimal candlePrice, int score)
        {
            throw new NotImplementedException();
        }

        Task<int> IRDSDAO.UpdateGeneratedAlertsAsync(Alert alert)
        {
            throw new NotImplementedException();
        }

        void IRDSDAO.UpdateListData(int strangleId, uint currentCallToken, uint currentPutToken, uint prevCallToken, uint prevPutToken, int currentIndex, int prevIndex, decimal[] currentSellPrice, decimal[] prevBuyPrice, AlgoIndex algoIndex, decimal bInstPrice)
        {
            throw new NotImplementedException();
        }

        void IRDSDAO.UpdateListData(int strangleId, uint instrumentToken, int instrumentIndex, int previousInstrumentIndex, string instrumentType, uint previousInstrumentToken, decimal currentNodePrice, decimal previousNodePrice, AlgoIndex algoIndex)
        {
            throw new NotImplementedException();
        }

        void IRDSDAO.UpdateOptionData(int strangleId, uint instrumentToken, string instrumentType, decimal lastPrice, decimal currentPrice, AlgoIndex algoIndex)
        {
            throw new NotImplementedException();
        }

        decimal IRDSDAO.UpdateOrder(Order order, int algoInstance)
        {
            throw new NotImplementedException();
        }

        int IRDSDAO.UpdateOrderTrio(uint optionToken, string mainOrderId, string slOrderId, string tpOrderId, decimal targetprofit, decimal stoploss, DateTime entryTradeTime, decimal baseInstrumentSL, bool tpflag, int algoInstance, bool isActive, int orderTrioId, bool? slflag, string indicatorsValue)
        {
            throw new NotImplementedException();
        }

        int IRDSDAO.UpdateStrangleData(uint ceToken, uint peToken, decimal cePrice, decimal pePrice, decimal bInstPrice, AlgoIndex algoIndex, decimal safetyMargin, double stopLossPoints, int initialQty, int maxQty, int stepQty, string ceOrderId, string peOrderId, string transactionType)
        {
            throw new NotImplementedException();
        }

        int IRDSDAO.UpdateStrangleData(uint ceToken, uint peToken, decimal cePrice, decimal pePrice, AlgoIndex algoIndex, decimal ceMaxProfitPoints, decimal ceMaxLossPoints, decimal peMaxProfitPoints, decimal peMaxLossPoints, decimal ceMaxProfitPercent, decimal ceMaxLossPercent, decimal peMaxProfitPercent, decimal peMaxLossPercent, double stopLossPoints)
        {
            throw new NotImplementedException();
        }

        int IRDSDAO.UpdateStrangleData(uint ceToken, uint peToken, decimal cePrice, decimal pePrice, AlgoIndex algoIndex, double ceLowerDelta, double ceUpperDelta, double peLowerDelta, double peUpperDelta, double stopLossPoints, decimal baseInstrumentPrice)
        {
            throw new NotImplementedException();
        }

        decimal IRDSDAO.UpdateTrade(int strategyID, uint instrumentToken, decimal averageTradePrice, DateTime? exchangeTimeStamp, string orderId, int qty, string transactionType, AlgoIndex algoIndex, int tradedLot, int triggerID)
        {
            throw new NotImplementedException();
        }

        bool IRDSDAO.UpdateUser(User activeUser)
        {
            throw new NotImplementedException();
        }
    }
}
