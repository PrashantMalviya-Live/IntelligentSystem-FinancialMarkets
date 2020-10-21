using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GlobalLayer;
using DataAccess;
using System.Data;
//using Algorithms.Utils;
using Algorithms.Utils;
using ZConnectWrapper;

namespace Algorithms.Utilities
{
    public class DataLogic
    {
        public bool UpdateUser(User activeUser)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.UpdateUser(activeUser);
        }
        public User GetActiveUser()
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsUser = marketDAO.GetActiveUser();

            return new User(dsUser.Tables[0]);
        }
        public void SaveCandle(Candle candle)
        {
            MarketDAO marketDAO = new MarketDAO();

            CandlePriceLevel maxPriceLevel = candle.MaxPriceLevel;
            CandlePriceLevel minPriceLevel = candle.MinPriceLevel;
            IEnumerable<CandlePriceLevel> priceLevels = candle.PriceLevels;

            int candleId = marketDAO.SaveCandle(candle.Arg, candle.ClosePrice, candle.CloseTime, candle.CloseVolume, candle.DownTicks,
                candle.HighPrice, candle.HighTime, candle.HighVolume, candle.InstrumentToken,
                candle.LowPrice, candle.LowTime, candle.LowVolume,
                maxPriceLevel.BuyCount, maxPriceLevel.BuyVolume, maxPriceLevel.Money, maxPriceLevel.Price,
                maxPriceLevel.SellCount, maxPriceLevel.SellVolume, maxPriceLevel.TotalVolume,
                minPriceLevel.BuyCount, minPriceLevel.BuyVolume, minPriceLevel.Money, minPriceLevel.Price,
                minPriceLevel.SellCount, minPriceLevel.SellVolume, minPriceLevel.TotalVolume,
                candle.OpenInterest, candle.OpenPrice, candle.OpenTime, candle.OpenVolume,
                candle.RelativeVolume, candle.State, candle.TotalPrice, candle.TotalTicks, candle.TotalVolume, candle.UpTicks, candle.CandleType);

            foreach (CandlePriceLevel candlePriceLevel in candle.PriceLevels)
            {
                marketDAO.SaveCandlePriceLevels(candleId, candlePriceLevel.BuyCount, candlePriceLevel.BuyVolume, candlePriceLevel.Money, candlePriceLevel.Price,
                candlePriceLevel.SellCount, candlePriceLevel.SellVolume, candlePriceLevel.TotalVolume);
            }


        }

        public void LoadTokens()
        {
            ZConnect.Login();
            List<Instrument> instruments = ZObjects.kite.GetInstruments();

            MarketDAO marketDAO = new MarketDAO();
            marketDAO.StoreInstrumentList(instruments);
        }

        public List<Order> GetOrders(AlgoIndex algoIndex = 0, int orderid = 0)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet ds = marketDAO.GetOrders(algoIndex, orderid);

            List<Order> orders = new List<Order>();
            foreach(DataRow dr in ds.Tables[0].Rows)
            {
                Order order = new Order
                {
                    InstrumentToken = Convert.ToUInt32(dr["InstrumentToken"]),
                    Tradingsymbol = (string) dr["TradingSymbol"],
                    TransactionType = (string) dr["TransactionType"],
                    AveragePrice = Convert.ToDecimal(dr["AveragePrice"]),
                    Quantity = (int) dr["Quantity"],
                    TriggerPrice = Convert.ToDecimal(dr["TriggerPrice"]),
                    Status = (string) dr["Status"],
                    StatusMessage = Convert.ToString(dr["StatusMessage"]),
                    OrderType = Convert.ToString(dr["OrderType"]),
                    OrderTimestamp = Convert.ToDateTime(dr["OrderTimeStamp"]),
                    AlgoIndex = Convert.ToInt32(dr["AlgoIndex"]),
                    AlgoInstance = Convert.ToInt32(dr["AlgoInstance"])
                };
                orders.Add(order);
            }
            return orders;
        }
        public DataSet GetDailyOHLC(IEnumerable<uint> tokens, DateTime date)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.GetDailyOHLC(tokens, date);
        }

        public Dictionary<uint, Instrument> LoadInstruments(AlgoIndex algoIndex, DateTime? expiry, uint baseInstrumentToken, decimal baseInstrumentPrice)
            //,decimal FromStrikePrice = 0, decimal ToStrikePrice = 0, InstrumentType instrumentType = InstrumentType.ALL)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.LoadInstruments(algoIndex, expiry, baseInstrumentToken, baseInstrumentPrice);
                  //,FromStrikePrice = 0, ToStrikePrice = 0, instrumentType);


            Dictionary<uint, Instrument> activeInstruments = new Dictionary<uint, Instrument>();

            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
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


                //activeInstruments.Add(String.Format("{0}{1}", instrument.InstrumentType, instrument.Strike) , instrument);
                activeInstruments.Add(instrument.InstrumentToken, instrument);
            }
            return activeInstruments;
        }
        public SortedList<decimal, Instrument>[] LoadCloseByOptions(DateTime? expiry, uint baseInstrumentToken, decimal baseInstrumentPrice)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.LoadCloseByOptions(expiry, baseInstrumentToken, baseInstrumentPrice);

            List<Instrument> options = new List<Instrument>();
            SortedList<decimal, Instrument> ceList = new SortedList<decimal, Instrument>();
            SortedList<decimal, Instrument> peList = new SortedList<decimal, Instrument>();

            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
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


                if(instrument.InstrumentType.Trim(' ').ToLower() == "ce")
                {
                    ceList.Add(instrument.Strike, instrument);
                }
                else if (instrument.InstrumentType.Trim(' ').ToLower() == "pe")
                {
                    peList.Add(instrument.Strike, instrument);
                }

                //activeInstruments.Add(String.Format("{0}{1}", instrument.InstrumentType, instrument.Strike) , instrument);
                //options.Add(instrument);
            }
            //return options;
            SortedList<decimal, Instrument>[] optionUniverse = new SortedList<decimal, Instrument>[2];
            optionUniverse[(int)InstrumentType.CE] = ceList;
            optionUniverse[(int)InstrumentType.PE] = peList;

            return optionUniverse;
        }
        
        public DataSet LoadHistoricalTokenVolume(AlgoIndex algoIndex, DateTime startTime,
            DateTime endTime, TimeSpan candleTimeSpan, int numberofCandles)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet ds = marketDAO.LoadHistoricalTokenVolume(algoIndex, startTime,
                endTime, candleTimeSpan, numberofCandles);

            return ds;
        }
        
        public Dictionary<uint, List<decimal>> GetHistoricalCandlePrices(int candlesCount, DateTime lastCandleEndTime, string tokenList, TimeSpan _candleTimeSpan)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet ds = marketDAO.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, tokenList, _candleTimeSpan);

            Dictionary<uint, List<decimal>> tokenPrices = new Dictionary<uint, List<decimal>>();
            foreach(DataRow dr in ds.Tables[0].Rows)
            {
                uint token = Convert.ToUInt32(dr["InstrumentToken"]);
                if (tokenPrices.ContainsKey(token))
                {
                    tokenPrices[token].Add(Convert.ToDecimal(dr["ClosePrice"]));
                }
                else
                {
                    List<decimal> prices = new List<decimal>();
                    prices.Add(Convert.ToDecimal(dr["ClosePrice"]));
                    tokenPrices.Add(token, prices);
                }
                
            }
            return tokenPrices;
        }

        public Dictionary<uint, List<decimal>> GetHistoricalClosePricesFromCandles(int candlesCount, DateTime lastCandleEndTime, string tokenList, TimeSpan _candleTimeSpan)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet ds = marketDAO.GetHistoricalClosePricesFromCandles(candlesCount, lastCandleEndTime, tokenList, _candleTimeSpan);

            //List<decimal> prices = new List<decimal>();
            //foreach (DataRow dr in ds.Tables[0].Rows)
            //{
            //    prices.Add((decimal)dr[0]);
            //}
            //return prices;
            Dictionary<uint, List<decimal>> tokenPrices = new Dictionary<uint, List<decimal>>();
            foreach (DataRow dr in ds.Tables[0].Rows)
            {
                uint token = Convert.ToUInt32(dr["InstrumentToken"]);
                if (tokenPrices.ContainsKey(token))
                {
                    tokenPrices[token].Add(Convert.ToDecimal(dr["ClosePrice"]));
                }
                else
                {
                    List<decimal> prices = new List<decimal>();
                    prices.Add(Convert.ToDecimal(dr["ClosePrice"]));
                    tokenPrices.Add(token, prices);
                }

            }
            return tokenPrices;
        }

        public DataSet LoadCandles(int numberofCandles, CandleType candleType, DateTime endDateTime, uint instrumentToken, TimeSpan timeFrame)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsCandles = marketDAO.LoadCandles(numberofCandles, candleType, endDateTime, instrumentToken, timeFrame);

            return dsCandles;

            //List<Candle> candleList = new List<Candle>();

            //foreach (DataRow candle in dsCandles.Tables[0].Rows)
            //{
            //    Candle instrument = new Candle();
            //    instrument.InstrumentToken = Convert.ToUInt32(optionData["Token"]);
            //    instrument.TradingSymbol = Convert.ToString(optionData["Symbol"]);
            //    instrument.Strike = Convert.ToDecimal(optionData["StrikePrice"]);
            //    instrument.Expiry = Convert.ToDateTime(optionData["ExpiryDate"]);
            //    instrument.InstrumentType = Convert.ToString(optionData["Type"]);
            //    instrument.LotSize = Convert.ToUInt32(optionData["LotSize"]);




            //    updateCMD.Parameters.AddWithValue("@instrumentToken", (Int64)instrumentToken);
            //    updateCMD.Parameters.AddWithValue("@closePrice", closePrice);
            //    updateCMD.Parameters.AddWithValue("@CloseTime", CloseTime);
            //    updateCMD.Parameters.AddWithValue("@closeVolume", closeVolume);
            //    updateCMD.Parameters.AddWithValue("@downTicks", downTicks);
            //    updateCMD.Parameters.AddWithValue("@highPrice", highPrice);
            //    updateCMD.Parameters.AddWithValue("@highTime", highTime);
            //    updateCMD.Parameters.AddWithValue("@highVolume", highVolume);
            //    updateCMD.Parameters.AddWithValue("@lowPrice", lowPrice);
            //    updateCMD.Parameters.AddWithValue("@lowTime", lowTime);
            //    updateCMD.Parameters.AddWithValue("@lowVolume", lowVolume);
            //    updateCMD.Parameters.AddWithValue("@maxPriceLevelBuyCount", (int)maxPriceLevelBuyCount);
            //    updateCMD.Parameters.AddWithValue("@maxPriceLevelBuyVolume", (int)maxPriceLevelBuyVolume);
            //    updateCMD.Parameters.AddWithValue("@maxPriceLevelMoney", maxPriceLevelMoney);
            //    updateCMD.Parameters.AddWithValue("@maxPriceLevelPrice", maxPriceLevelPrice);
            //    updateCMD.Parameters.AddWithValue("@maxPriceLevelSellCount", (int)maxPriceLevelSellCount);
            //    updateCMD.Parameters.AddWithValue("@maxPriceLevelSellVolume", (int)maxPriceLevelSellVolume);
            //    updateCMD.Parameters.AddWithValue("@maxPriceLevelTotalVolume", (int)maxPriceLevelTotalVolume);
            //    updateCMD.Parameters.AddWithValue("@minPriceLevelBuyCount", (int)minPriceLevelBuyCount);
            //    updateCMD.Parameters.AddWithValue("@minPriceLevelBuyVolume", (int)minPriceLevelBuyVolume);
            //    updateCMD.Parameters.AddWithValue("@minPriceLevelMoney", minPriceLevelMoney);
            //    updateCMD.Parameters.AddWithValue("@minPriceLevelPrice", minPriceLevelPrice);
            //    updateCMD.Parameters.AddWithValue("@minPriceLevelSellCount", (int)minPriceLevelSellCount);
            //    updateCMD.Parameters.AddWithValue("@minPriceLevelSellVolume", (int)minPriceLevelSellVolume);
            //    updateCMD.Parameters.AddWithValue("@minPriceLevelTotalVolume", (int)minPriceLevelTotalVolume);
            //    updateCMD.Parameters.AddWithValue("@openInterest", openInterest);
            //    updateCMD.Parameters.AddWithValue("@openPrice", openPrice);
            //    updateCMD.Parameters.AddWithValue("@openTime", openTime);
            //    updateCMD.Parameters.AddWithValue("@openVolume", openVolume);
            //    updateCMD.Parameters.AddWithValue("@relativeVolume", (int)relativeVolume);
            //    updateCMD.Parameters.AddWithValue("@totalPrice", totalPrice);
            //    updateCMD.Parameters.AddWithValue("@totalTicks", totalTicks);
            //    updateCMD.Parameters.AddWithValue("@totalVolume", totalVolume);
            //    updateCMD.Parameters.AddWithValue("@upTicks", upTicks);
            //    updateCMD.Parameters.AddWithValue("@Arg", Arg.ToString());
            //    updateCMD.Parameters.AddWithValue("@CandleType", (int)candleType);




            //    options.Add(instrument);



        }



            //}

            public List<Instrument> RetrieveOptions(UInt32 baseInstrumentToken, DateTime expiryDate)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveOptions(baseInstrumentToken, expiryDate);
        }
        public List<Instrument> RetrieveBaseInstruments()
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveBaseInstruments();
        }

        public DataSet RetrieveActiveStrangles(AlgoIndex algoIndex)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveActiveStrangles(algoIndex);
        }
        public DataSet RetrieveActiveData(AlgoIndex algoIndex)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveActiveData(algoIndex);
        }
        public DataSet GetActiveAlgos(AlgoIndex algoIndex)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.GetActiveAlgos(algoIndex);
        }
        //public DataSet GetInstrument(decimal strikePrice, string InstrumentType)
        //{
        //    MarketDAO marketDAO = new MarketDAO();
        //    return marketDAO.GetInstrument(strikePrice, InstrumentType);
        //}

        public DataSet RetrieveActiveStrangleData(AlgoIndex algoIndex)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveActiveStrangleData(algoIndex);
        }

        public DataSet RetrieveActivePivotInstruments(AlgoIndex algoIndex)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveActivePivotInstruments(algoIndex);
        }

        public DataSet TestRetrieveActivePivotInstruments(AlgoIndex algoIndex)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.TestRetrieveActivePivotInstruments(algoIndex);
        }

        


        public decimal RetrieveLastPrice(UInt32 token, DateTime? time = null, bool buyOrder = false)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveLastPrice(token, time, buyOrder);
        }
       
        public decimal UpdateTrade(int strategyID, UInt32 instrumentToken, ShortTrade trade, AlgoIndex algoIndex, int tradedLot = 0, int triggerID = 0)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.UpdateTrade(strategyID, instrumentToken, trade.AveragePrice, trade.ExchangeTimestamp, 
                trade.OrderId, trade.Quantity, trade.TransactionType, algoIndex, tradedLot, triggerID);
        }

        public void UpdateOrder(int algoInstance, Order order, int strategyId = 0)
        {
            MarketDAO marketDAO = new MarketDAO();
            marketDAO.UpdateOrder(order, algoInstance);
        }

        public int StoreBoxData(int boxID, UInt32 firstCallToken, UInt32 secondCallToken, UInt32 firstPutToken, UInt32 secondPutToken,
            decimal firstCallPrice, decimal secondCallPrice, decimal firstPutPrice, decimal secondPutPrice, decimal boxValue)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.StoreBoxData(boxID, firstCallToken, secondCallToken, firstPutToken, secondPutToken,
            firstCallPrice, secondCallPrice, firstPutPrice, secondPutPrice, boxValue);
        }

        public DataSet RetrieveBoxData(int boxID)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveBoxData(boxID);
        }

        public List<string> RetrieveOptionExpiries(UInt32 baseInstrumentToken)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveOptionExpiries(baseInstrumentToken);
        }
        public SortedList<Decimal, Instrument> RetrieveNextNodes(UInt32 baseInstrumentToken,
            string instrumentType, decimal currentStrikePrice, DateTime expiry, int updownboth)
        {

            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.RetrieveNextNodes(baseInstrumentToken, instrumentType, currentStrikePrice, expiry, updownboth);

            SortedList<Decimal, Instrument> dinstruments = new SortedList<Decimal, Instrument>();

            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Instrument instrument = new Instrument();
                instrument.InstrumentToken = Convert.ToUInt32(data["Token"]);
                instrument.TradingSymbol = Convert.ToString(data["Symbol"]);
                instrument.Strike = Convert.ToDecimal(data["StrikePrice"]);
                instrument.Expiry = Convert.ToDateTime(data["ExpiryDate"]);
                instrument.InstrumentType = Convert.ToString(data["Type"]);
                instrument.LotSize = Convert.ToUInt32(data["LotSize"]);
                instrument.BaseInstrumentToken = Convert.ToUInt32(data["BInstrumentToken"]);
                ///TODO:This field is causing delay. See what you can do.
                ///Commenting again. Query it from Zerodha as GetLTP for instrument if this is not updated when required
                // instrument.LastPrice = Convert.ToUInt32(data["LastPrice"]);

                dinstruments.Add(instrument.Strike, instrument);
            }
            return dinstruments;
        }
        public SortedList<Decimal, Option> RetrieveNextNodes(UInt32 baseInstrumentToken, decimal bInstrumentPrice,
           string instrumentType, decimal currentStrikePrice, DateTime expiry, int updownboth, int noOfRecords = 10)
        {

            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.RetrieveNextNodes(baseInstrumentToken, instrumentType, currentStrikePrice, expiry, updownboth, noOfRecords: noOfRecords);

            SortedList<Decimal, Option> options = new SortedList<Decimal, Option>();

            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Option option = new Option();
                option.InstrumentToken = Convert.ToUInt32(data["Token"]);
                option.TradingSymbol = Convert.ToString(data["Symbol"]);
                option.Strike = Convert.ToDecimal(data["StrikePrice"]);
                option.Expiry = Convert.ToDateTime(data["ExpiryDate"]);
                option.InstrumentType = Convert.ToString(data["Type"]);
                option.LotSize = Convert.ToUInt32(data["LotSize"]);
                option.BaseInstrumentToken = Convert.ToUInt32(data["BInstrumentToken"]);
                option.BaseInstrumentPrice = bInstrumentPrice;
                ///TODO:This field is causing delay. See what you can do.
                ///Commenting again. Query it from Zerodha as GetLTP for instrument if this is not updated when required
                // instrument.LastPrice = Convert.ToUInt32(data["LastPrice"]);

                options.Add(option.Strike, option);
            }
            return options;
        }

        public SortedList<Decimal, Instrument>[] RetrieveNextStrangleNodes(UInt32 baseInstrumentToken,
           DateTime expiry, decimal callStrikePrice, decimal putStrikePrice, int updownboth)
        {

            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveNextStrangleNodes(baseInstrumentToken, expiry, callStrikePrice, putStrikePrice, updownboth);
        }

        public void UpdateListData(int strangleId, UInt32 instrumentToken, int instrumentIndex,int previousInstrumentIndex, string instrumentType, 
            UInt32 previousInstrumentToken, decimal currentNodePrice, decimal previousNodePrice, AlgoIndex algoIndex)
        {
            MarketDAO marketDAO = new MarketDAO();
            marketDAO.UpdateListData(strangleId, instrumentToken, instrumentIndex,previousInstrumentIndex, instrumentType, 
                previousInstrumentToken, currentNodePrice, previousNodePrice, algoIndex);
        }
        public void UpdateListData(int strangleId, UInt32 currentCallToken, UInt32 currentPutToken, UInt32 prevCallToken, UInt32 prevPutToken,
            int  currentIndex, int prevIndex, decimal[] currentSellPrice, decimal[] prevBuyPrice, AlgoIndex algoIndex, decimal bInstPrice)
        {
            MarketDAO marketDAO = new MarketDAO();
            marketDAO.UpdateListData(strangleId, currentCallToken, currentPutToken, prevCallToken, prevPutToken,
            currentIndex, prevIndex, currentSellPrice, prevBuyPrice, algoIndex, bInstPrice);
        }
       
        public void UpdateOptionData(int strategyId, UInt32 instrumentToken, string instrumentType, decimal lastPrice, decimal currentPrice, AlgoIndex algoIndex)
        {
            MarketDAO marketDAO = new MarketDAO();
            marketDAO.UpdateOptionData(strategyId, instrumentToken, instrumentType, lastPrice, currentPrice, algoIndex);
        }

        ///// <summary>
        ///// This method is used to update information of newly created strangle on the database.
        ///// </summary>
        ///// <param name="ceToken"></param>
        ///// <param name="peToken"></param>
        ///// <param name="ceLowerValue"></param>
        ///// <param name="ceLowerDelta"></param>
        ///// <param name="ceUpperValue"></param>
        ///// <param name="ceUpperDelta"></param>
        ///// <param name="peLowerValue"></param>
        ///// <param name="peLowerDelta"></param>
        ///// <param name="peUpperValue"></param>
        ///// <param name="peUpperDelta"></param>
        ///// <param name="stopLossPoints"></param>
        ///// <returns></returns>
        //public int UpdateStrangleData(UInt32 ceToken, UInt32 peToken, decimal ceLowerValue = 0, double ceLowerDelta = 0, decimal ceUpperValue = 0, double ceUpperDelta = 0,
        //    decimal peLowerValue = 0, double peLowerDelta = 0, decimal peUpperValue = 0, double peUpperDelta = 0, decimal stopLossPoints = 0)
        //{
        //    MarketDAO marketDAO = new MarketDAO();
        //    return marketDAO.UpdateStrangleData(ceToken, peToken, ceLowerValue, ceLowerDelta, ceUpperValue, ceUpperDelta,
        //    peLowerValue, peLowerDelta, peUpperValue, peUpperDelta, stopLossPoints);

        //}

        public int StoreStrangleData(UInt32 ceToken, UInt32 peToken, decimal cePrice, decimal pePrice, AlgoIndex algoIndex, 
            double ceLowerDelta = 0, double ceUpperDelta = 0, double peLowerDelta = 0, double peUpperDelta = 0,  
            double stopLossPoints = 0)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.UpdateStrangleData(ceToken, peToken, cePrice, pePrice, algoIndex, ceLowerDelta, ceUpperDelta,
            peLowerDelta, peUpperDelta, stopLossPoints);
        }
        public int StorePivotInstruments(UInt32 primaryInstrumentToken, DateTime expiry, AlgoIndex algoIndex, int maxlot, int lotunit, PivotFrequency pivotFrequency, int pivotWindow)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.StorePivotInstruments(primaryInstrumentToken, expiry, algoIndex, maxlot, lotunit, pivotFrequency, pivotWindow);

        }
        public int StoreStrangleData(UInt32 ceToken, UInt32 peToken, decimal cePrice, decimal pePrice, decimal bInstPrice, AlgoIndex algoIndex,
            decimal safetyMargin = 10, double stopLossPoints = 0, int initialQty=0, int maxQty=0, int stepQty=0, string ceOrderId = "", string peOrderId = "", string transactionType = "")
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.UpdateStrangleData(ceToken, peToken, cePrice, pePrice, bInstPrice, algoIndex, safetyMargin, stopLossPoints, initialQty, maxQty, stepQty, ceOrderId, peOrderId, transactionType);
        }

        public int CreateOptionStrategy(OptionStrategy optionStrategy)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.CreateOptionStrategy(parentToken: optionStrategy.ParentInstToken, algoIndex: optionStrategy.AlgoIndex, 
                stoplosspoints: optionStrategy.StopLossPoints, initialQty: optionStrategy.InitialQty, maxQty: optionStrategy.MaxQty, 
                stepQty: optionStrategy.StepQty);
        }

        public int StoreOptionStrategy(OptionStrategy optionStrategy)
        {

            StringBuilder tokens = new StringBuilder();
            StringBuilder prices = new StringBuilder();
            StringBuilder qtys = new StringBuilder();
            StringBuilder tags = new StringBuilder();
            StringBuilder transactionTypes = new StringBuilder();
            StringBuilder orderIds = new StringBuilder();
            foreach (ShortOrder shortOrder in optionStrategy.Orders)
            {
                tokens.AppendFormat("{0},", shortOrder.InstrumentToken);
                prices.AppendFormat("{0},", shortOrder.Price);
                qtys.AppendFormat("{0},", shortOrder.Quantity);
                tags.AppendFormat("{0},", shortOrder.Tag);
                transactionTypes.AppendFormat("{0},", shortOrder.TransactionType);
                orderIds.AppendFormat("{0},", shortOrder.OrderId);

            }
            tokens.Remove(tokens.Length - 2, 1);
            prices.Remove(prices.Length - 2, 1);
            qtys.Remove(qtys.Length - 2, 1);
            tags.Remove(tags.Length - 2, 1);
            transactionTypes.Remove(transactionTypes.Length - 2, 1);
            orderIds.Remove(orderIds.Length - 2, 1);


            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.StoreStrategyData(tokens.ToString(), prices.ToString(), qtys.ToString(), tags.ToString(), 
                transactionTypes.ToString(), orderIds.ToString(), parentToken: optionStrategy.ParentInstToken, parentPrice: optionStrategy.ParentInstPrice,
                algoIndex: optionStrategy.AlgoIndex, stoplosspoints: optionStrategy.StopLossPoints, 
                initialQty: optionStrategy.InitialQty, maxQty: optionStrategy.MaxQty, stepQty: optionStrategy.StepQty);
        }

        public int StoreStrangleData(UInt32 ceToken, UInt32 peToken, decimal cePrice, decimal pePrice, AlgoIndex algoIndex, 
            decimal ceMaxProfitPoints = 0, decimal ceMaxLossPoints = 0, decimal peMaxProfitPoints = 0, 
            decimal peMaxLossPoints = 0,  double stopLossPoints = 0)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.UpdateStrangleData(ceToken, peToken, cePrice, pePrice, algoIndex, ceMaxProfitPoints, 
                ceMaxLossPoints, peMaxProfitPoints, peMaxLossPoints, stopLossPoints: stopLossPoints);
        }
        public int StoreIndexForMainPainStrangle(uint bToken, DateTime expiry, int strikePriceIncrement, AlgoIndex algoIndex,
            int tradingQty, decimal maxLossThreshold, decimal profitTarget, DateTime timeOfOrder)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.StoreIndexForMainPain(bToken, expiry, strikePriceIncrement, algoIndex, tradingQty, maxLossThreshold, profitTarget, timeOfOrder);
        }

        public int StoreStrangleData(UInt32 ceToken, UInt32 peToken, decimal cePrice, decimal pePrice, AlgoIndex algoIndex,
           decimal ceMaxProfitPoints = 0, decimal ceMaxLossPoints = 0, decimal peMaxProfitPoints = 0,
           decimal peMaxLossPoints = 0, decimal ceMaxProfitPercent=0, decimal ceMaxLossPercent=0, decimal peMaxProfitPercent=0, 
           decimal peMaxLossPercent=0, double stopLossPoints = 0)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.UpdateStrangleData(ceToken, peToken, cePrice, pePrice, algoIndex, ceMaxProfitPoints,
                ceMaxLossPoints, peMaxProfitPoints, peMaxLossPoints, ceMaxProfitPercent, ceMaxLossPercent, 
                peMaxProfitPercent, peMaxLossPercent, stopLossPoints);

        }
    }
}
