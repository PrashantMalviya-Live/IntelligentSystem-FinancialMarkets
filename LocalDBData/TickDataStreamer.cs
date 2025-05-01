using Algorithms.Utilities;
using Algos.Utilities.Views;
using BrokerConnectWrapper;
using DataAccess;
using DBAccess;
using FyersConnect;
using GlobalLayer;
//using KafkaFacade;
using KiteConnect;
using LocalDBData.Test;
using Newtonsoft.Json.Linq;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LocalDBData
{
    public class TickDataStreamer// : IObservable<Tick>
    {

        DateTime? prevTickTime, currentTickTime;
        uint counter = 0;
        //public static Dictionary<UInt32, Queue<Tick>> LiveTickData;
        //public static List<Tick> LiveTicks;
        SortedList<DateTime, Tick> LiveTicks = new SortedList<DateTime, Tick>();
        SortedList<DateTime, Tick> LocalTicks = new SortedList<DateTime, Tick>();
        //DataAccess.QuestDB qsDB = new QuestDB();
        List<Tick> ticks;

        //Kafka
        //Dictionary<uint, TopicPartition> instrumentPartitions = new Dictionary<uint, TopicPartition>();

        int partitionCount = 0;
        private readonly IRDSDAO _irdsDAO;
        private readonly ITimeStreamDAO _itimestreamDAO;

        public TickDataStreamer(IRDSDAO irdsDAO, ITimeStreamDAO timeStreamDAO)
        {
            _irdsDAO = irdsDAO;
            _itimestreamDAO = timeStreamDAO;
            //Subscriber list
            //observers = new List<IObserver<Tick[]>>();
        }
        

        public async Task<long> BeginStreaming()
        {
            try
            {
                LoadDataFromDatabase();
                //RetrieveHistoricalTicksFromKite();
                //RetrieveHistoricalTicksFromFyers();
                //LoadHistoricalDataFromKiteToDatabase();
                //LoadHistoricalDataFromFyersToDatabase();
                return 1;
            }
            catch (Exception ex)
            {
                return -1;
            }
        }
        public async Task<long> LoadDataFromDatabase()
        {
            //ZLogin.Login();
           // //ZConnect.Login();
            //ZObjects.ticker = new Ticker(ZConnect.UserAPIkey, ZConnect.UserAccessToken, null);//State:zSessionState.Current);
            // Fy.Login();


            DateTime fromDate = Convert.ToDateTime("2023-10-06");
            DateTime toDate = Convert.ToDateTime("2023-10-06");

            //BNF
            //decimal fromStrike = 40000;
            //decimal toStrike = 55000;
            //uint btoken = 260105; //257801;

            ////NF
            decimal fromStrike = 18000;
            decimal toStrike = 22500;
            uint btoken = 256265;

            //FINNITY
            //decimal fromStrike = 19000;
            //decimal toStrike = 20500;
            //uint btoken = 257801;e

            //MIDCP NIFTY
            //decimal fromStrike = 6000;
            //decimal toStrike = 9500;
            //uint btoken = 288009;

            bool futuresData = false;
            bool optionsData = true;



            //InitialRangeBreakoutTest testAlgo = new InitialRangeBreakoutTest();
            //PAWithLevelsTest testAlgo = new PAWithLevelsTest();
            //StockMomentumTest testAlgo = new StockMomentumTest();
            //CandleWickScalpingOptionsTest testAlgo = new CandleWickScalpingOptionsTest();
            //CandleWickScalpingTest testAlgo = new CandleWickScalpingTest();
            //ActiveBuyStrangleWithVariableQtyTest testAlgo = new ActiveBuyStrangleWithVariableQtyTest();
            //OptionBuyOnEMACrossTimeVolumeTest testAlgo = new OptionBuyOnEMACrossTimeVolumeTest();
            //StraddleWithEachLegSLTest testAlgo = new StraddleWithEachLegSLTest();
            //EMAScalpingKBTest testAlgo = new EMAScalpingKBTest();
            //MultipleEMAPriceActionTest testAlgo = new MultipleEMAPriceActionTest();
            //ManageStrangleDeltaTest testAlgo = new ManageStrangleDeltaTest();
            //StopAndReverseTest testAlgo = new StopAndReverseTest();
            //TJ4Test testAlgo = new TJ4Test();
            //OptionSellonHTTest testAlgo = new OptionSellonHTTest();
            //MultiTimeFrameSellOnHTTest testAlgo = new MultiTimeFrameSellOnHTTest();
            //TJ3Test testAlgo = new TJ3Test();
            //TJ5Test testAlgo = new TJ5Test();
            //BCTest testAlgo = new BCTest();

            //Latest
            //RangeBreakoutCandleTest testAlgo = new RangeBreakoutCandleTest();
            //StockTrendTest testAlgo = new StockTrendTest();

            OptionSellOnEMATest testAlgo = new OptionSellOnEMATest();
            //PriceVolumeRateOfChangeTest testAlgo = new PriceVolumeRateOfChangeTest();
            //AlertTest testAlgo = new AlertTest();


            //DirectionalWithStraddleShiftTest testAlgo = new DirectionalWithStraddleShiftTest();
            //CalendarSpreadValueScalpingTest testAlgo = new CalendarSpreadValueScalpingTest();
            //ExpiryTradeTest testAlgo = new ExpiryTradeTest();
            //StraddleExpiryTradeTest testAlgo = new StraddleExpiryTradeTest();
            //ExpiryTradeICTest testAlgo = new ExpiryTradeICTest();
            //ManageStrangleWithLevelsTest testAlgo = new ManageStrangleWithLevelsTest();


            
            List<string> dates = _irdsDAO.GetActiveTradingDays(fromDate, toDate);

            DateTime currentDate = fromDate;
            //ZMQServer zmqServer = new ZMQServer();

            using (SqlConnection sqlConnection = new SqlConnection( DataAccess.Utility.GetConnectionString()))
            {
                while (currentDate.Date <= toDate.Date)
                {
                    if (sqlConnection.State == ConnectionState.Closed)
                    {
                        sqlConnection.Open();
                    }
                    if (dates.Contains(currentDate.ToShortDateString()))
                    {
                        DateTime expiry = DateTime.Now;
                        //DateTime expiry2 = DateTime.Now;

                        if (optionsData)
                        {
                            if (currentDate.DayOfWeek == DayOfWeek.Thursday || currentDate.DayOfWeek == DayOfWeek.Wednesday)
                            {
                                expiry = _irdsDAO.GetCurrentWeeklyExpiry(currentDate, btoken);

                                //expiry = _irdsDAO.GetCurrentWeeklyExpiry(currentDate.AddDays(3), btoken);
                            }

                            else
                            {
                                expiry = _irdsDAO.GetCurrentWeeklyExpiry(currentDate, btoken);
                                //expiry = _irdsDAO.GetNexWeeklyExpiry(currentDate, btoken);

                                //expiry = _irdsDAO.GetCurrentWeeklyExpiry(currentDate.AddDays(3), btoken);
                                // expiry = _irdsDAO.GetCurrentMonthlyExpiry(currentDate, btoken);
                            }

                            //if (currentDate.Date != expiry.Date)
                            //{
                            //    currentDate = currentDate.AddDays(1);
                            //    continue;
                            //}

                            //expiry2 = _irdsDAO.GetCurrentWeeklyExpiry(currentDate.AddDays(1), btoken);
                        }
                        else if (futuresData)
                        {
                            expiry = _irdsDAO.GetCurrentMonthlyExpiry(currentDate, btoken);
                        }


                        //PriceActionInput inputs = new PriceActionInput()
                        //{
                        //    BToken = Convert.ToUInt32(btoken),
                        //    TP = 40,
                        //    SL = 30,
                        //    CTF = 3,
                        //    CurrentDate = currentDate,
                        //    Qty = 4,
                        //    Expiry = expiry,
                        //    UID = "PM27031981"
                        //};

                        // PriceActionInput inputs = new PriceActionInput() { BToken = Convert.ToUInt32(btoken), CTF = 1, CurrentDate = currentDate, Qty = 1 };
                        //OptionBuyOnEMACrossInputs inputs = new OptionBuyOnEMACrossInputs()
                        //{
                        //    BToken = btoken,
                        //    CTF = 5,
                        //    LEMA = 13,
                        //    Qty = 1,
                        //    SEMA = 5,
                        //    SL = 20,
                        //    TP = 50
                        //};

                        //StraddleInput inputs = new StraddleInput()
                        //{
                        //    BToken = btoken,
                        //    Qty = 1,
                        //    CTF = 15,
                        //    Expiry = expiry,
                        //    TP = 50,
                        //    SL = 2000,
                        //    UID = "PM27031981",
                        //    SPI = 25,
                        //    IntraDay = true,
                        //    TR = 2,
                        //    SS = true
                        //};
                        //StrangleWithDeltaandLevelInputs inputs = new StrangleWithDeltaandLevelInputs()
                        //{
                        //    BToken = Convert.ToUInt32(btoken),
                        //    CTF = 15,
                        //    Expiry = expiry,
                        //    CurrentDate = currentDate,
                        //    IDelta = 0.17m,
                        //    IQty = 2,
                        //    MaxDelta = 0.27m,
                        //    MinDelta = 0.10m,
                        //    MaxQty = 4,
                        //    StepQty = 1,
                        //    L1 = 44500,
                        //    L2 = 44100,
                        //    U1 = 45100,
                        //    U2 = 45600,
                        //    TP = 100000,
                        //    SL = 5000,
                        //    UID = "PM27031981",
                        //    IntraDay = false
                        //};

                        //OptionIVSpreadInput inputs = new OptionIVSpreadInput()
                        //{
                        //    BToken = Convert.ToUInt32(btoken),
                        //    OpenVar = 10,
                        //    CloseVar = 10,
                        //    Expiry1 = expiry,
                        //    Expiry2 = expiry2,
                        //    StepQty = 1,
                        //    MaxQty = 10,
                        //    Straddle = false,
                        //    TO = 30
                        //};

                        //expiry = Convert.ToDateTime("2025-04-09");
                        PriceActionInput inputs = new PriceActionInput()
                        {
                            BToken = Convert.ToUInt32(btoken),
                            CTF = 5,
                            Expiry = expiry,
                            CurrentDate = currentDate,
                            Qty = 1,
                            TP = 3000,
                            SL = 20,
                            UID = "PM27031981",
                        };


                        //10 lot size 

                        //OptionExpiryStrangleInput inputs = new OptionExpiryStrangleInput()
                        //{
                        //    BToken = Convert.ToUInt32(btoken),
                        //    Expiry = expiry,
                        //    IDFBI = 400,
                        //    IQty = 4,
                        //    MDFBI = 200,
                        //    MPTT = 7,
                        //    MQty = 10,
                        //    SL = 30000,
                        //    SQty = 2,
                        //    TP = 600000,
                        //    UID = "PM27031981",
                        //    SPI = 100





                        //};
#if !LOCAL

                        testAlgo.Execute(inputs);
                        //testAlgo.Execute();
                        testAlgo.StopTrade(false);


#endif

                        //Task<long> allDone = RetrieveTicksFromDB(btoken, expiry, expiry2, fromStrike, toStrike, futuresData, 
                        //    optionsData, currentDate, testAlgo, sqlConnection);

                        //Below is Original calling method.
                        //Task<long> allDone = RetrieveTicksFromDB(btoken, expiry, fromStrike, toStrike, futuresData,
                        //   optionsData, currentDate, testAlgo, sqlConnection);

                        //Below method is for alerts only
                        Task<long> allDone = RetrieveTicksFromDB(btoken, expiry, fromStrike, toStrike, futuresData,
                           optionsData, currentDate, testAlgo, sqlConnection);
                        allDone.Wait();
                        sqlConnection.Close();
                    }
                    currentDate = currentDate.AddDays(1);
                }

            }
            return 1;
        }

        //private void LoadHistoricalDataFromFyersToDatabase()
        //{
        //    //FyLogin.Login();
        //    FyConnect.Login();

        //    DateTime fromDate = Convert.ToDateTime("2024-11-22 09:15:00");
        //    DateTime toDate = Convert.ToDateTime("2024-11-23 15:30:00");

        //    string token = Constants.NIFTY_TOKEN;
        //    string symbol = "NSE:NIFTY50-INDEX;256265";

            
        //    string interval = "5";
        //    List<string> dates = _irdsDAO.GetActiveTradingDays(fromDate, toDate);
        //    //List<string> tokens = new List<string>();
        //    List<string> symbols = new List<string>();
        //    DataSet dsIndexStocks = _irdsDAO.LoadIndexStocks(Convert.ToUInt32(token));


        //    foreach (DataRow row in dsIndexStocks.Tables[0].Rows)
        //    {
        //        symbols.Add(string.Format("{0}:{1}-{2};{3}", Convert.ToString(row["exchange"]), Convert.ToString(row["TradingSymbol"]), Convert.ToString(row["InstrumentType"]), Convert.ToString(row["InstrumentToken"])));
        //    }


        //    symbols.Add(symbol);

        //    symbols = symbols.Distinct<string>().ToList();


        //    //foreach (var tknsymbl in symbols)
        //    //{
        //    //    string tkn = tknsymbl.Split(';')[1];
        //    //    string symbl = tknsymbl.Split(';')[0];
        //    //    //for (DateTime tmpdate = fromDate; tmpdate < toDate; tmpdate = tmpdate.AddDays(59))
        //        //{

        //        ////if (tmpStartDate.Date == tmpEndDate.Date)
        //        ////{
        //        //tmpEndDate = tmpEndDate.Date + toDate.TimeOfDay;
        //        ////}

        //        //#region Load From Kite
        //        //List<Historical> btokenPrices = ZObjects.fy.GetHistoricalData(symbl, tmpStartDate, tmpEndDate, interval);
        //        //for (int i = 0; i < btokenPrices.Count; i++)
        //        //{
        //        //    Historical historical = btokenPrices[i];
        //        //    historical.InstrumentToken = Convert.ToUInt32(tkn);
        //        //    historicalPrices.Add(historical);

        //        //    //_irdsDAO.InsertHistoricals(historical);
        //        //}









        //        //DateTime tmpdate = fromDate;
        //        DateTime tmpStartDate = fromDate, tmpEndDate = fromDate;

        //    while (tmpEndDate < toDate)
        //    {
        //        List<Historical> historicalPrices = new List<Historical>();

        //        tmpEndDate = tmpStartDate.AddDays(99);

        //        if (toDate < tmpEndDate)
        //        {
        //            tmpEndDate = toDate;
        //        }

        //        tmpEndDate = tmpEndDate.Date + toDate.TimeOfDay;


        //        foreach (var tknsymbl in symbols)
        //        {
        //            try
        //            {
        //                string tkn = tknsymbl.Split(';')[1];
        //                string symbl = tknsymbl.Split(';')[0];
        //                //    //for (DateTime tmpdate = fromDate; tmpdate < toDate; tmpdate = tmpdate.AddDays(59))

        //                if (symbl != "NSE:M&M-EQ")
        //                {
        //                    List<Historical> btokenPrices = ZObjects.fy.GetHistoricalData(symbl, tmpStartDate, tmpEndDate, interval);
        //                    for (int i = 0; i < btokenPrices.Count; i++)
        //                    {
        //                        Historical historical = btokenPrices[i];
        //                        historical.InstrumentToken = Convert.ToUInt32(tkn);
        //                        historicalPrices.Add(historical);

        //                        _irdsDAO.InsertHistoricals(historical, 0);
        //                    }
        //                }
        //            }
        //            catch(Exception ex)
        //            {
        //                Console.WriteLine(ex.StackTrace);
        //            }
        //        }

        //        tmpStartDate = tmpEndDate.AddDays(1);
        //        tmpStartDate = tmpStartDate.Date + fromDate.TimeOfDay;
        //    }
        //}
       
        //private void RetrieveHistoricalTicksFromFyers()
        //{
        //    try
        //    {
        //        //FyLogin.Login();
        //        FyConnect.Login();

        //        decimal fromStrike = 37000;
        //        decimal toStrike = 43500;
        //        DateTime fromDate = Convert.ToDateTime("2024-09-01 09:15:00");
        //        DateTime toDate = Convert.ToDateTime("2024-11-25 15:30:00");
        //        //DateTime fromDate = toDate.AddDays(-1);

        //        //string token = Constants.NIFTY_TOKEN;
        //        string token = Constants.NIFTY_TOKEN;
        //        string symbol = "NSE:NIFTY50-INDEX;256265";
        //        bool optionsData = false;
        //        bool futuresData = true;
                

        //        //var dates = new List<DateTime>();
        //        string interval = "1";
        //        //zmqServer = new ZMQServer();


        //        List<string> dates = _irdsDAO.GetActiveTradingDays(fromDate, toDate);
        //        //for (var dt = fromDate; dt <= toDate; dt = dt.AddDays(1))
        //        //{
        //        //    dates.Add(dt);
        //        //}
        //        //SortedList<DateTime, Historical> historicalPrices = new SortedList<DateTime, Historical>();
        //        List<string> symbols = new List<string>();
        //        //PAWithLevelsTest testAlgo = new PAWithLevelsTest();
        //        //SchScalingTest testAlgo = new SchScalingTest();
        //        //CandleWickScalpingTest testAlgo = new CandleWickScalpingTest();
        //        //StockMomentumTest testAlgo = new StockMomentumTest();
        //        //MultipleEMAPriceActionTest testAlgo = new MultipleEMAPriceActionTest();
        //        //MultipleEMALevelsScoreTest testAlgo = new MultipleEMALevelsScoreTest();
        //        //ManageStrangleDeltaTest testAlgo = new ManageStrangleDeltaTest();
        //        //TJ2Test testAlgo = new TJ2Test();

        //        //AlertTest testAlgo = new AlertTest();
        //        //RangeBreakoutCandleTest testAlgo = new RangeBreakoutCandleTest();
        //        //StockTrendTest testAlgo = new StockTrendTest();
        //        SuperDuperTrendTest testAlgo = new SuperDuperTrendTest();
        //        //TJ3Test testAlgo = new TJ3Test();
        //        //TJ4Test testAlgo = new TJ4Test();
        //        //OptionSellonHTTest testAlgo = new OptionSellonHTTest();
        //        //StraddleWithEachLegSLTest testAlgo = new StraddleWithEachLegSLTest();
        //        //MultiTimeFrameSellOnHTTest testAlgo = new MultiTimeFrameSellOnHTTest();
        //        //MultiEMADirectionalLevelsTest testAlgo = new MultiEMADirectionalLevelsTest();
        //        //StopAndReverseTest testAlgo = new StopAndReverseTest();
        //        //DateTime currentExpiry = _irdsDAO.GetCurrentMonthlyExpiry(DateTime.Now);
        //        //Instrument fut = GetInstrument(currentExpiry, Convert.ToUInt32(token), 0, "FUT");
        //        //DirectionalWithStraddleShiftTest testAlgo = new DirectionalWithStraddleShiftTest();
        //        DateTime? tradeDate = null;

        //        //historicalPrices = new List<Historical>();
        //        //tokens = new List<string>();

        //        DataSet dsIndexStocks = _irdsDAO.LoadIndexStocks(Convert.ToUInt32(token));

        //        foreach (DataRow row in dsIndexStocks.Tables[0].Rows)
        //        {
        //            symbols.Add(string.Format("{0}:{1}-{2};{3}", Convert.ToString(row["exchange"]), Convert.ToString(row["TradingSymbol"]), Convert.ToString(row["InstrumentType"]), Convert.ToString(row["InstrumentToken"])));
        //        }
        //        //    var allOptions = dl.LoadOptions(tradeDateExpiry, token, _baseInstrumentPrice, _maxDistanceFromBInstrument, out calls, out puts, out mappedTokens);


        //        symbols.Add(symbol);
        //        //tokens.Add(fut.InstrumentToken.ToString());
        //        //tokens.Add(rtoken);

        //        symbols = symbols.Distinct<string>().ToList();

        //        //DateTime tmpdate = fromDate;
        //        DateTime tmpStartDate = fromDate, tmpEndDate = fromDate;

        //        while (tmpEndDate < toDate)
        //        {
        //            #region Data from Kite

        //            List<Historical> historicalPrices = new List<Historical>();

        //            tmpEndDate = tmpStartDate.AddDays((int)tmpStartDate.DayOfWeek <= 4 ? 4 - (int)tmpStartDate.DayOfWeek : 11 - (int)tmpStartDate.DayOfWeek);

        //            if (toDate < tmpEndDate)
        //            {
        //                tmpEndDate = toDate;
        //            }



        //            foreach (var tknsymbl in symbols)
        //            {
        //                try
        //                {
        //                    string tkn = tknsymbl.Split(';')[1];
        //                    string symbl = tknsymbl.Split(';')[0];
        //                    //for (DateTime tmpdate = fromDate; tmpdate < toDate; tmpdate = tmpdate.AddDays(59))
        //                    //{

        //                    //if (tmpStartDate.Date == tmpEndDate.Date)
        //                    //{
        //                    tmpEndDate = tmpEndDate.Date + toDate.TimeOfDay;
        //                    //}

        //                    #region Load From Kite
        //                    //List<Historical> btokenPrices = ZObjects.fy.GetHistoricalData(symbl, tmpStartDate, tmpEndDate, interval);
        //                    //for (int i = 0; i < btokenPrices.Count; i++)
        //                    //{
        //                    //    Historical historical = btokenPrices[i];
        //                    //    historical.InstrumentToken = Convert.ToUInt32(tkn);
        //                    //    historicalPrices.Add(historical);

        //                    //    //_irdsDAO.InsertHistoricals(historical);
        //                    //}

        //                    //if (optionsData)
        //                    //{
        //                    //    DateTime tradeDateExpiry = _irdsDAO.GetCurrentWeeklyExpiry(tmpStartDate, Convert.ToUInt32(token));
        //                    //    //DateTime tradeDateExpiry = _irdsDAO.GetCurrentMonthlyExpiry(tmpStartDate);
        //                    //    List<uint> instruments = _irdsDAO.GetInstrumentTokens(Convert.ToUInt32(tkn), fromStrike, toStrike, tradeDateExpiry);
        //                    //    foreach (var instrumentToken in instruments)
        //                    //    {
        //                    //        List<Historical> futurePrices = ZObjects.fy.GetHistoricalData(instrumentToken.ToString(), tmpStartDate, tmpEndDate, interval);
        //                    //        for (int i = 0; i < futurePrices.Count; i++)
        //                    //        {

        //                    //            Historical historical = futurePrices[i];
        //                    //            historical.InstrumentToken = instrumentToken;
        //                    //            historicalPrices.Add(historical);
        //                    //            historical.InstrumentToken = instrumentToken;
        //                    //            //_irdsDAO.InsertHistoricals(historical);
        //                    //        }
        //                    //    }
        //                    //}
        //                    //else if (futuresData)
        //                    //{
        //                    //    DateTime tradeDateExpiry = _irdsDAO.GetCurrentMonthlyExpiry(tmpStartDate, Convert.ToUInt32(token));
        //                    //    //Instrument fut = GetInstrument(tradeDateExpiry, Convert.ToUInt32(token), 0, "FUT");
        //                    //    Instrument fut = GetInstrument(null, Convert.ToUInt32(token), 0, "EQ");
        //                    //    //tokens.Add(fut.InstrumentToken.ToString());
        //                    //    List<Historical> futurePrices = ZObjects.fy.GetHistoricalData(fut.InstrumentToken.ToString(), tmpStartDate, tmpEndDate, interval);
        //                    //    for (int i = 0; i < futurePrices.Count; i++)
        //                    //    {
        //                    //        Historical historical = futurePrices[i];
        //                    //        historical.InstrumentToken = fut.InstrumentToken;
        //                    //        historicalPrices.Add(historical);
        //                    //    }

        //                    //}
        //                    #endregion

        //                    #region Load From Database
        //                    historicalPrices.AddRange(_irdsDAO.GetHistoricals(Convert.ToUInt32(tkn), tmpStartDate, tmpEndDate));
        //                    //historicalPrices.Sort();

        //                    #endregion
        //                }
        //                catch (Exception ex) { 
        //                    Console.WriteLine(ex.StackTrace);
        //                }
        //            }

        //            tmpStartDate = tmpEndDate.AddDays(1);
        //            tmpStartDate = tmpStartDate.Date + fromDate.TimeOfDay;
        //            historicalPrices.Sort();

        //            #endregion



        //            //Instrument fut = GetInstrument(expiry, Convert.ToUInt32(token), 0, "FUT");
        //            //List<Historical> btokenPrices = ZObjects.kite.GetHistoricalData(token, date, date.AddDays(1), interval);

        //            //for (int i = 0; i < btokenPrices.Count; i++)
        //            //{
        //            //    Historical historical = btokenPrices[i];
        //            //    historical.InstrumentToken = Convert.ToUInt32(token);
        //            //    historicalPrices.Add(historical);
        //            //}

        //            ////GlobalObjects.InstrumentTokenSymbolCollection = dao.GetInstrumentListToSubscribe(0, btokenPrices[10].Close);
        //            ////List<Historical> historicalPrices = new List<Historical>();
        //            //tokens.Add(fut.InstrumentToken.ToString());
        //            ////continuous = tradeDateExpiry.Date.ToShortDateString() != currentExpiry.Date.ToShortDateString();
        //            //List<Historical> futurePrices = ZObjects.kite.GetHistoricalData(fut.InstrumentToken.ToString(), date, date.AddDays(1), interval);
        //            //if (futurePrices.Count < 2)
        //            //{
        //            //    continue;
        //            //}
        //            //for (int i = 0; i < futurePrices.Count; i++)
        //            //{

        //            //    Historical historical = futurePrices[i];
        //            //    historical.InstrumentToken = fut.InstrumentToken;
        //            //    historicalPrices.Add(historical);
        //            //}

        //            //historicalPrices.Sort();
        //            //foreach (var date in dates)
        //            //{
        //            //DateTime currentExpiry = DateTime.Now;
        //            foreach (var historicalPrice in historicalPrices)
        //            {
        //                if (dates.Contains(historicalPrice.TimeStamp.ToShortDateString()))
        //                {
        //                    if (!tradeDate.HasValue || tradeDate.Value.ToShortDateString() != historicalPrice.TimeStamp.ToShortDateString())
        //                    {
        //                        //testAlgo = new StockMomentumTest();
        //                        tradeDate = historicalPrice.TimeStamp;
        //                        DateTime currentExpiry = _irdsDAO.GetCurrentMonthlyExpiry(tradeDate.Value, Convert.ToUInt32(token));
        //                        //currentExpiry = _irdsDAO.GetCurrentWeeklyExpiry(tradeDate.Value);
        //                        PriceActionInput inputs = new PriceActionInput()
        //                        {
        //                            BToken = Convert.ToUInt32(token),
        //                            RToken = 0,//Convert.ToUInt32(rtoken),
        //                            CTF = 5, //60
        //                            Expiry = currentExpiry,
        //                            CurrentDate = tradeDate.Value,
        //                            Qty = 1,
        //                            TP = 40,
        //                            SL = 2000,
        //                            UID = "client1188"
        //                        };
        //                        //StraddleInput inputs = new StraddleInput()
        //                        //{
        //                        //    BToken = Convert.ToUInt32(token),
        //                        //    Qty = 1,
        //                        //    uid = "NJ18111985",
        //                        //    CTF = 5,
        //                        //    Expiry = currentExpiry,
        //                        //    TP = 25000,
        //                        //    SL = 10000,
        //                        //    IntraDay = false,
        //                        //    SS = true,
        //                        //    TR = 1.67m
        //                        //};


        //                        //testAlgo = new TJ3Test();
        //                        //testAlgo = new DirectionalWithStraddleShiftTest();
        //                        // testAlgo.Execute(inputs);
        //                        //testAlgo.StopTrade(false);

        //                        //StrangleWithDeltaandLevelInputs inputs = new StrangleWithDeltaandLevelInputs()
        //                        //{
        //                        //    BToken = Convert.ToUInt32(token),
        //                        //    Expiry = currentExpiry,
        //                        //    IDelta = 0.2m,
        //                        //    IQty = 4,
        //                        //    MaxDelta = 0.24m,
        //                        //    MinDelta = 0.10m,
        //                        //    MaxQty = 8,
        //                        //    StepQty = 2,
        //                        //    TP = 100000,
        //                        //    SL = 500,
        //                        //};
        //                        //testAlgo = new ManageStrangleDeltaTest();
        //                        //testAlgo = new PAWithLevelsTest();
        //                        //testAlgo = new RangeBreakoutCandleTest();
        //                        //testAlgo = new StockTrendTest();
        //                        testAlgo = new SuperDuperTrendTest();
        //                        //testAlgo = new AlertTest();
        //                        //testAlgo = new StopAndReverseTest();
        //                        testAlgo.Execute(inputs);
        //                        //testAlgo.Execute();
        //                        testAlgo.StopTrade(false);
        //                    }

        //                    //foreach (var historicalPrice in historicalPrices)
        //                    //{
        //                    List<Tick> ticks = new List<Tick>();
        //                    ticks.AddRange(ConvertToTicks(historicalPrice, shortenedTick: true));

        //                    ticks.Sort(new TickComparer());

        //                    foreach (Tick tick in ticks)
        //                    {
        //                        testAlgo.OnNext(tick);
        //                    }
        //                    //KProducer kfkPublisher = new KProducer();
        //                    //kfkPublisher.PublishAllTicks(ticks, shortenedTick: true);
        //                    //ZObjects.ticker.zmqServer.PublishAllTicks(ticks, shortenedTick: true);
        //                }
        //            }
        //        }
        //        //var observableFactory = GlobalObjects.ObservableFactory;
        //        //foreach (var historicalPrice in historicalPrices)
        //        //{
        //        //    List<Tick> ticks = new List<Tick>();
        //        //    ticks.AddRange(ConvertToTicks(historicalPrice, shortenedTick: true));
        //        //    foreach (Tick tick in ticks)
        //        //    {
        //        //        foreach (var observer in observableFactory.observers)
        //        //            observer.OnNext(tick);
        //        //    }
        //        //}
        //        //KAdmin.DeleteTopics(tokens);
        //        //}
        //    }
        //    catch (Exception ex)
        //    {

        //    }

        //}

        //private void LoadHistoricalDataFromKiteToDatabase()
        //{
        //    ZLogin.Login();
        //    //ZConnect.Login();
        //    DateTime fromDate = Convert.ToDateTime("2022-01-01 09:15:00");
        //    DateTime toDate = Convert.ToDateTime("2024-10-25 15:30:00");

        //    string token = Constants.NIFTY_TOKEN;
        //    
        //    string interval = "day";
        //    List<string> dates = _irdsDAO.GetActiveTradingDays(fromDate, toDate);
        //    List<string> tokens = new List<string>();

        //    DataSet dsIndexStocks = _irdsDAO.LoadIndexStocks(Convert.ToUInt32(token));

        //    foreach (DataRow row in dsIndexStocks.Tables[0].Rows)
        //    {
        //        tokens.Add(Convert.ToString(row["InstrumentToken"]));
        //    }
        //    tokens.Add(token);

        //    //DateTime tmpdate = fromDate;
        //    DateTime tmpStartDate = fromDate, tmpEndDate = fromDate;

        //    while (tmpEndDate < toDate)
        //    {
        //        List<Historical> historicalPrices = new List<Historical>();

        //        tmpEndDate = tmpStartDate.AddDays((int)tmpStartDate.DayOfWeek <= 4 ? 4 - (int)tmpStartDate.DayOfWeek : 11 - (int)tmpStartDate.DayOfWeek);

        //        if (toDate < tmpEndDate)
        //        {
        //            tmpEndDate = toDate;
        //        }

        //        tmpEndDate = tmpEndDate.Date + toDate.TimeOfDay;
        //        foreach (var tkn in tokens)
        //        {
        //            //for (DateTime tmpdate = fromDate; tmpdate < toDate; tmpdate = tmpdate.AddDays(59))
        //            //{

        //            //if (tmpStartDate.Date == tmpEndDate.Date)
        //            //{

        //            //}

        //            List<Historical> btokenPrices = ZObjects.kite.GetHistoricalData(tkn, tmpStartDate, tmpEndDate, interval);
        //            for (int i = 0; i < btokenPrices.Count; i++)
        //            {
        //                Historical historical = btokenPrices[i];
        //                historical.InstrumentToken = Convert.ToUInt32(tkn);
        //                historicalPrices.Add(historical);

        //                _irdsDAO.InsertHistoricals(historical, 1);
        //            }
        //        }

        //        tmpStartDate = tmpEndDate.AddDays(1);
        //        tmpStartDate = tmpStartDate.Date + fromDate.TimeOfDay;
        //    }
        //}

        private void RetrieveHistoricalTicksFromKite()
        {
            try
            {
                // ZLogin.Login();
                ////ZConnect.Login();
                //ZObjects.ticker = new Ticker(ZConnect.UserAPIkey, ZConnect.UserAccessToken, null);//State:zSessionState.Current);
                //FyLogin.Login();
                //FyConnect.Login();

                decimal fromStrike = 37000;
                decimal toStrike = 43500;
                DateTime fromDate = Convert.ToDateTime("2024-10-01 09:15:00");
                DateTime toDate = Convert.ToDateTime("2024-10-25 15:30:00");
                //DateTime fromDate = toDate.AddDays(-1);

                //string token = Constants.NIFTY_TOKEN;
                string token = Constants.NIFTY_TOKEN;
                bool optionsData = false;
                bool futuresData = true;
                

                //var dates = new List<DateTime>();
                string interval = "minute";
                //zmqServer = new ZMQServer();


                List<string> dates = _irdsDAO.GetActiveTradingDays(fromDate, toDate);
                //for (var dt = fromDate; dt <= toDate; dt = dt.AddDays(1))
                //{
                //    dates.Add(dt);
                //}
                //SortedList<DateTime, Historical> historicalPrices = new SortedList<DateTime, Historical>();
                List<string> tokens = new List<string>();
                //PAWithLevelsTest testAlgo = new PAWithLevelsTest();
                //SchScalingTest testAlgo = new SchScalingTest();
                //CandleWickScalpingTest testAlgo = new CandleWickScalpingTest();
                //StockMomentumTest testAlgo = new StockMomentumTest();
                //MultipleEMAPriceActionTest testAlgo = new MultipleEMAPriceActionTest();
                //MultipleEMALevelsScoreTest testAlgo = new MultipleEMALevelsScoreTest();
                //ManageStrangleDeltaTest testAlgo = new ManageStrangleDeltaTest();
                //TJ2Test testAlgo = new TJ2Test();

                //AlertTest testAlgo = new AlertTest();
                //RangeBreakoutCandleTest testAlgo = new RangeBreakoutCandleTest();
                StockTrendTest testAlgo = new StockTrendTest();

                //TJ3Test testAlgo = new TJ3Test();
                //TJ4Test testAlgo = new TJ4Test();
                //OptionSellonHTTest testAlgo = new OptionSellonHTTest();
                //StraddleWithEachLegSLTest testAlgo = new StraddleWithEachLegSLTest();
                //MultiTimeFrameSellOnHTTest testAlgo = new MultiTimeFrameSellOnHTTest();
                //MultiEMADirectionalLevelsTest testAlgo = new MultiEMADirectionalLevelsTest();
                //StopAndReverseTest testAlgo = new StopAndReverseTest();
                //DateTime currentExpiry = _irdsDAO.GetCurrentMonthlyExpiry(DateTime.Now);
                //Instrument fut = GetInstrument(currentExpiry, Convert.ToUInt32(token), 0, "FUT");
                //DirectionalWithStraddleShiftTest testAlgo = new DirectionalWithStraddleShiftTest();
                DateTime? tradeDate = null;

                //historicalPrices = new List<Historical>();
                //tokens = new List<string>();

                DataSet dsIndexStocks = _irdsDAO.LoadIndexStocks(Convert.ToUInt32(token));

                foreach(DataRow row in dsIndexStocks.Tables[0].Rows)
                {
                    tokens.Add(Convert.ToString(row["InstrumentToken"]));
                }
                //    var allOptions = dl.LoadOptions(tradeDateExpiry, token, _baseInstrumentPrice, _maxDistanceFromBInstrument, out calls, out puts, out mappedTokens);


                tokens.Add(token);
                //tokens.Add(fut.InstrumentToken.ToString());
                //tokens.Add(rtoken);

                tokens = tokens.Distinct<string>().ToList();

                //DateTime tmpdate = fromDate;
                DateTime tmpStartDate = fromDate, tmpEndDate = fromDate;

                while (tmpEndDate < toDate)
                {
                    #region Data from Kite

                    List<Historical> historicalPrices = new List<Historical>();

                    tmpEndDate = tmpStartDate.AddDays((int)tmpStartDate.DayOfWeek <= 4 ? 4 - (int)tmpStartDate.DayOfWeek : 11 - (int)tmpStartDate.DayOfWeek);

                    if (toDate < tmpEndDate)
                    {
                        tmpEndDate = toDate;
                    }

                    

                    foreach (var tkn in tokens)
                    {
                        //for (DateTime tmpdate = fromDate; tmpdate < toDate; tmpdate = tmpdate.AddDays(59))
                        //{

                        //if (tmpStartDate.Date == tmpEndDate.Date)
                        //{
                        tmpEndDate = tmpEndDate.Date + toDate.TimeOfDay;
                        //}

                        #region Load From Kite
                        //List<Historical> btokenPrices = ZObjects.kite.GetHistoricalData(tkn, tmpStartDate, tmpEndDate, interval);
                        //for (int i = 0; i < btokenPrices.Count; i++)
                        //{
                        //    Historical historical = btokenPrices[i];
                        //    historical.InstrumentToken = Convert.ToUInt32(tkn);
                        //    historicalPrices.Add(historical);

                        //    //_irdsDAO.InsertHistoricals(historical);
                        //}

                        //if (optionsData)
                        //{
                        //    DateTime tradeDateExpiry = _irdsDAO.GetCurrentWeeklyExpiry(tmpStartDate, Convert.ToUInt32(token));
                        //    //DateTime tradeDateExpiry = _irdsDAO.GetCurrentMonthlyExpiry(tmpStartDate);
                        //    List<uint> instruments = _irdsDAO.GetInstrumentTokens(Convert.ToUInt32(tkn), fromStrike, toStrike, tradeDateExpiry);
                        //    foreach (var instrumentToken in instruments)
                        //    {
                        //        List<Historical> futurePrices = ZObjects.kite.GetHistoricalData(instrumentToken.ToString(), tmpStartDate, tmpEndDate, interval);
                        //        for (int i = 0; i < futurePrices.Count; i++)
                        //        {

                        //            Historical historical = futurePrices[i];
                        //            historical.InstrumentToken = instrumentToken;
                        //            historicalPrices.Add(historical);
                        //            historical.InstrumentToken = instrumentToken;
                        //            //_irdsDAO.InsertHistoricals(historical);
                        //        }
                        //    }
                        //}
                        //else if (futuresData)
                        //{
                        //    DateTime tradeDateExpiry = _irdsDAO.GetCurrentMonthlyExpiry(tmpStartDate, Convert.ToUInt32(token));
                        //    //Instrument fut = GetInstrument(tradeDateExpiry, Convert.ToUInt32(token), 0, "FUT");
                        //    Instrument fut = GetInstrument(null, Convert.ToUInt32(token), 0, "EQ");
                        //    //tokens.Add(fut.InstrumentToken.ToString());
                        //    List<Historical> futurePrices = ZObjects.kite.GetHistoricalData(fut.InstrumentToken.ToString(), tmpStartDate, tmpEndDate, interval);
                        //    for (int i = 0; i < futurePrices.Count; i++)
                        //    {
                        //        Historical historical = futurePrices[i];
                        //        historical.InstrumentToken = fut.InstrumentToken;
                        //        historicalPrices.Add(historical);
                        //    }

                        //}
                        #endregion

                        #region Load From Database
                        historicalPrices.AddRange(_itimestreamDAO.GetHistoricals(Convert.ToUInt32(tkn), tmpStartDate, tmpEndDate));
                        //historicalPrices.Sort();

                        #endregion
                    }

                    tmpStartDate = tmpEndDate.AddDays(1);
                    tmpStartDate = tmpStartDate.Date + fromDate.TimeOfDay;
                    historicalPrices.Sort();

                    #endregion

                   

                    //Instrument fut = GetInstrument(expiry, Convert.ToUInt32(token), 0, "FUT");
                    //List<Historical> btokenPrices = ZObjects.kite.GetHistoricalData(token, date, date.AddDays(1), interval);

                    //for (int i = 0; i < btokenPrices.Count; i++)
                    //{
                    //    Historical historical = btokenPrices[i];
                    //    historical.InstrumentToken = Convert.ToUInt32(token);
                    //    historicalPrices.Add(historical);
                    //}

                    ////GlobalObjects.InstrumentTokenSymbolCollection = dao.GetInstrumentListToSubscribe(0, btokenPrices[10].Close);
                    ////List<Historical> historicalPrices = new List<Historical>();
                    //tokens.Add(fut.InstrumentToken.ToString());
                    ////continuous = tradeDateExpiry.Date.ToShortDateString() != currentExpiry.Date.ToShortDateString();
                    //List<Historical> futurePrices = ZObjects.kite.GetHistoricalData(fut.InstrumentToken.ToString(), date, date.AddDays(1), interval);
                    //if (futurePrices.Count < 2)
                    //{
                    //    continue;
                    //}
                    //for (int i = 0; i < futurePrices.Count; i++)
                    //{

                    //    Historical historical = futurePrices[i];
                    //    historical.InstrumentToken = fut.InstrumentToken;
                    //    historicalPrices.Add(historical);
                    //}

                    //historicalPrices.Sort();
                    //foreach (var date in dates)
                    //{
                    //DateTime currentExpiry = DateTime.Now;
                    foreach (var historicalPrice in historicalPrices)
                    {
                        if (dates.Contains(historicalPrice.TimeStamp.ToShortDateString()))
                        {
                            if (!tradeDate.HasValue || tradeDate.Value.ToShortDateString() != historicalPrice.TimeStamp.ToShortDateString())
                            {
                                //testAlgo = new StockMomentumTest();
                                tradeDate = historicalPrice.TimeStamp;
                                DateTime currentExpiry = _irdsDAO.GetCurrentMonthlyExpiry(tradeDate.Value, Convert.ToUInt32(token));
                                //currentExpiry = _irdsDAO.GetCurrentWeeklyExpiry(tradeDate.Value);
                                PriceActionInput inputs = new PriceActionInput()
                                {
                                    BToken = Convert.ToUInt32(token),
                                    RToken = 0,//Convert.ToUInt32(rtoken),
                                    CTF = 60,
                                    Expiry = currentExpiry,
                                    CurrentDate = tradeDate.Value,
                                    Qty = 1,
                                    TP = 40,
                                    SL = 2000,
                                    UID = "client1188"
                                };
                                //StraddleInput inputs = new StraddleInput()
                                //{
                                //    BToken = Convert.ToUInt32(token),
                                //    Qty = 1,
                                //    uid = "NJ18111985",
                                //    CTF = 5,
                                //    Expiry = currentExpiry,
                                //    TP = 25000,
                                //    SL = 10000,
                                //    IntraDay = false,
                                //    SS = true,
                                //    TR = 1.67m
                                //};


                                //testAlgo = new TJ3Test();
                                //testAlgo = new DirectionalWithStraddleShiftTest();
                                // testAlgo.Execute(inputs);
                                //testAlgo.StopTrade(false);

                                //StrangleWithDeltaandLevelInputs inputs = new StrangleWithDeltaandLevelInputs()
                                //{
                                //    BToken = Convert.ToUInt32(token),
                                //    Expiry = currentExpiry,
                                //    IDelta = 0.2m,
                                //    IQty = 4,
                                //    MaxDelta = 0.24m,
                                //    MinDelta = 0.10m,
                                //    MaxQty = 8,
                                //    StepQty = 2,
                                //    TP = 100000,
                                //    SL = 500,
                                //};
                                //testAlgo = new ManageStrangleDeltaTest();
                                //testAlgo = new PAWithLevelsTest();
                                //testAlgo = new RangeBreakoutCandleTest();
                                testAlgo = new StockTrendTest();
                                //testAlgo = new AlertTest();
                                //testAlgo = new StopAndReverseTest();
                                testAlgo.Execute(inputs);
                                //testAlgo.Execute();
                                testAlgo.StopTrade(false);
                            }

                            //foreach (var historicalPrice in historicalPrices)
                            //{
                            List<Tick> ticks = new List<Tick>();
                            ticks.AddRange(ConvertToTicks(historicalPrice, shortenedTick: true));

                            ticks.Sort(new TickComparer());

                            foreach (Tick tick in ticks)
                            {
                                testAlgo.OnNext(tick);
                            }
                            //KProducer kfkPublisher = new KProducer();
                            //kfkPublisher.PublishAllTicks(ticks, shortenedTick: true);
                            //ZObjects.ticker.zmqServer.PublishAllTicks(ticks, shortenedTick: true);
                        }
                    }
                }
                //var observableFactory = GlobalObjects.ObservableFactory;
                //foreach (var historicalPrice in historicalPrices)
                //{
                //    List<Tick> ticks = new List<Tick>();
                //    ticks.AddRange(ConvertToTicks(historicalPrice, shortenedTick: true));
                //    foreach (Tick tick in ticks)
                //    {
                //        foreach (var observer in observableFactory.observers)
                //            observer.OnNext(tick);
                //    }
                //}
                //KAdmin.DeleteTopics(tokens);
                //}
            }
            catch (Exception ex)
            {

            }

        }


        private async Task<long> RetrieveTicksFromDB(uint btoken, DateTime expiry, decimal fromStrike, decimal toStrike,
            bool futuresData, bool optionsData, DateTime currentDate, ITest testAlgo, SqlConnection sqlConnection, DateTime? expiry2 = null)
        {

#if LOCAL
            //ZMQServer zmqServer = new ZMQServer();
           // Tick[] ticks = new Tick[500];
#endif
            long counter = 0;
            object[] data = new object[19];


            //KProducer producer = new KProducer();

            try
            {

                //await sqlConnection.OpenAsync();

                using (SqlCommand command = new SqlCommand("GetTickDataTest", sqlConnection))
                {
                    command.CommandTimeout = 1000000;
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@BToken", Convert.ToInt64(btoken));
                    command.Parameters.AddWithValue("@Date", currentDate);
                    command.Parameters.AddWithValue("@Expiry", expiry);
                    command.Parameters.AddWithValue("@Expiry2", expiry2);
                    command.Parameters.AddWithValue("@FromStrike", fromStrike);
                    command.Parameters.AddWithValue("@ToStrike", toStrike);
                    command.Parameters.AddWithValue("@FuturesData", futuresData);
                    command.Parameters.AddWithValue("@OptionsData", optionsData);

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

                                while (reader.Read())
                                {
                                    try
                                    {
                                        reader.GetValues(data);
                                        Tick tick = ConvertToTicks(data, shortenedTick: true);
                                        currentTickTime = tick.Timestamp;

                                        ticks = new List<Tick>();
                                        ticks.Add(tick);

#if BACKTEST
                                        //Below is original calling method
                                        testAlgo.OnNext(tick);

#endif

#if LOCAL


                                        //zmqServer.PublishAllTicks(ticks, shortenedTick: true);
                                        //Thread.Sleep(1);
                                        if (counter > 5000)
                                        {
                                            counter = 0;
                                            Task.Delay(100).Wait();
                                        }
                                        counter++;
#endif
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.LogWrite(ex.Message);
                                    }
                                }
                            }
                        }
                    }
                }
                //await sqlConnection.CloseAsync();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return counter;
        }
        //private void ExecuteAlgo()
        //{
        //    if (!tradeDate.HasValue || tradeDate.Value.ToShortDateString() != historicalPrice.TimeStamp.ToShortDateString())
        //    {
        //        tradeDate = historicalPrice.TimeStamp;
        //        currentExpiry = _irdsDAO.GetCurrentMonthlyExpiry(tradeDate.Value);
        //        PriceActionInput inputs = new PriceActionInput() { BToken = Convert.ToUInt32(token), CTF = 3, Expiry = currentExpiry, CurrentDate = tradeDate.Value, Qty = 1, TP = 50, SL = 10 };
        //        testAlgo.Execute(inputs);
        //        testAlgo.StopTrade(false);
        //    }

        //    //foreach (var historicalPrice in historicalPrices)
        //    //{
        //    List<Tick> ticks = new List<Tick>();
        //    ticks.AddRange(ConvertToTicks(historicalPrice, shortenedTick: true));

        //    foreach (Tick tick in ticks)
        //    {
        //        testAlgo.OnNext(tick);
        //    }
        //}
        /// <summary>
        /// Store raw ticks
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        //void StoreRawTicks(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    if (LocalTicks == null || LocalTicks.Count == 0 || LocalTicks.Values.ToList() == null)
        //    {
        //        return;
        //    }


        //    lock (LocalTicks)
        //    {
        //        //zmqServer.PublishAllTicks(LocalTicks.Values.ToList(), shortenedTick: true);
        //        _irdsDAO.StoreTestTickData(LocalTicks.Values.ToList(), shortenedTick: true);
        //        LocalTicks.Clear();
        //    }
        //}
        //public void InitTimer()
        //{
        //    GlobalObjects.OHLCTimer = new System.Timers.Timer(1);
        //    GlobalObjects.OHLCTimer.Start();

        //    GlobalObjects.OHLCTimer.Elapsed += StoreRawTicks;
        //}
        public Tick ConvertToTicks(object[] data, bool shortenedTick = false)
        {
            Tick tick = new Tick();
            tick.InstrumentToken = Convert.ToUInt32(data[1]);
            tick.LastPrice = Convert.ToDecimal(data[2]);
            tick.Volume = Convert.ToUInt32(data[3]);
            tick.Mode = Constants.MODE_FULL;
            tick.Tradable = (tick.InstrumentToken & 0xff) != 9;
            tick.LastTradeTime = data[4] != System.DBNull.Value ? Convert.ToDateTime(data[4]) : DateTime.MinValue;
            tick.OI = Convert.ToUInt32(data[5]);
            tick.Timestamp = data[6] != System.DBNull.Value ? Convert.ToDateTime(data[6]) : DateTime.MinValue;



            if (shortenedTick)
            {
                //DATA STOPPED TO REDUCE DB SPACE. THIS CAN BE RE ENABLED LATER.
                tick.LastQuantity = 0;
                tick.AveragePrice = 0;
                tick.BuyQuantity = 0;
                tick.SellQuantity = 0;
                tick.Open = 0;//Convert.ToDecimal(data[8]);
                tick.High = 0;// Convert.ToDecimal(data[9]);
                tick.Low = 0;// Convert.ToDecimal(data[10]);
                tick.Close = 0;// Convert.ToDecimal(data[11]);
                tick.OIDayHigh = 0;// Convert.ToUInt32(data[16]);
                tick.OIDayLow = 0;// Convert.ToUInt32(data[17]);

                //tempTickTime = tick.Timestamp;

                //DepthItem[][] backlogTicks = GetTickDataFromBacklog(Convert.ToString(data[14]));
                //tick.Bids = backlogTicks[0];
                //tick.Offers = backlogTicks[1];
            }

            return tick;
        }

        public Tick[] ConvertToTicks(Historical historical, bool shortenedTick = false)
        {
            Tick[] ticks = new Tick[4];
            try
            {
                Decimal[] prices = new Decimal[] { historical.Open, historical.High, historical.Low, historical.Close };
                TimeSpan[] timeAdjustments = new TimeSpan[] { TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15) };
                for (int i = 0; i < ticks.Length; i++)
                {
                    Tick tick = new Tick();
                    tick.InstrumentToken = historical.InstrumentToken;
                    tick.LastPrice = prices[i];
                    tick.Volume = historical.Volume;
                    tick.Mode = Constants.MODE_FULL;
                    tick.Tradable = (tick.InstrumentToken & 0xff) != 9;
                    tick.LastTradeTime = historical.TimeStamp + timeAdjustments[i];
                    tick.OI = 0;
                    tick.Timestamp = historical.TimeStamp + timeAdjustments[i];



                    if (shortenedTick)
                    {
                        //DATA STOPPED TO REDUCE DB SPACE. THIS CAN BE RE ENABLED LATER.
                        tick.LastQuantity = 0;
                        tick.AveragePrice = 0;
                        tick.BuyQuantity = 0;
                        tick.SellQuantity = 0;
                        tick.Open = 0;//Convert.ToDecimal(data[8]);
                        tick.High = 0;// Convert.ToDecimal(data[9]);
                        tick.Low = 0;// Convert.ToDecimal(data[10]);
                        tick.Close = 0;// Convert.ToDecimal(data[11]);
                        tick.OIDayHigh = 0;// Convert.ToUInt32(data[16]);
                        tick.OIDayLow = 0;// Convert.ToUInt32(data[17]);

                        //tempTickTime = tick.Timestamp;

                        //DepthItem[][] backlogTicks = GetTickDataFromBacklog(Convert.ToString(data[14]));
                        //tick.Bids = backlogTicks[0];
                        //tick.Offers = backlogTicks[1];
                    }
                    ticks[i] = tick;
                }
            }
            catch (Exception ex)
            {

            }

            return ticks;
        }
        public Instrument GetInstrument(DateTime? expiry, uint bInstrumentToken, decimal strike, string instrumentType)
        {
            
            DataSet dsInstrument = _irdsDAO.GetInstrument(expiry, bInstrumentToken, strike, instrumentType);
            DataRow data = dsInstrument.Tables[0].Rows[0];

            Instrument instrument = new Instrument();
            instrument.InstrumentToken = Convert.ToUInt32(data["Instrument_Token"]);
            instrument.TradingSymbol = Convert.ToString(data["TradingSymbol"]);
            instrument.Strike = Convert.ToDecimal(data["Strike"]);
            if (data["Expiry"] != DBNull.Value)
            {
                instrument.Expiry = Convert.ToDateTime(data["Expiry"]);
            }
            instrument.InstrumentType = Convert.ToString(data["Instrument_Type"]);
            instrument.LotSize = Convert.ToUInt32(data["Lot_Size"]);
            instrument.Exchange = Convert.ToString(data["Exchange"]);
            instrument.Segment = Convert.ToString(data["Segment"]);
            if (data["BToken"] != DBNull.Value)
            {
                instrument.BaseInstrumentToken = Convert.ToUInt32(data["BToken"]);
            }
            if (data["KToken"] != DBNull.Value)
            {
                instrument.KToken = Convert.ToUInt32(data["KToken"]);
            }
            return instrument;
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


                data["LastTradeTime"] = data["LastTradeTime"] != System.DBNull.Value ? Convert.ToDateTime(data["LastTradeTime"]) : DateTime.MinValue;

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
