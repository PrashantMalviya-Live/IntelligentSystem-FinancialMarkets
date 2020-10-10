using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Algorithms.Utilities;
using GlobalLayer;
using GlobalCore;
using KiteConnect;
using ZConnectWrapper;
using System.Data;
using ZMQFacade;
using Algorithms.Indicators;
using Algorithms.Utils;
using Algorithms.Candles;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Algos.TLogics
{
    public class OptionSellOnRSICross : IZMQ
    {
        private readonly int _algoInstance;

        IOrderedEnumerable<KeyValuePair<decimal, Instrument>> callOptions;
        IOrderedEnumerable<KeyValuePair<decimal, Instrument>> putOptions;
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }
        //public List<Instrument> CandleOptions { get; set; }

        /// <summary>
        /// algo instance and traded option if any. This will be kept in the DB for next day retrieval.
        /// </summary>
        public Dictionary<int, TradedOption> _tradedOptionInstance;

        List<Instrument> _activeOptions;
        List<TradedOption> _tradedOptions;
        private bool _stopTrade;
        public decimal _baseInstrumentPrice;
        public uint _baseInstrumentToken;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        DateTime _endDateTime;
        Dictionary<uint, RelativeStrengthIndex> tokenRSI;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;
        List<uint> _RSILoaded;
        List<uint> _SQLLoading;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;

        public const int CANDLE_COUNT = 14;
        public const decimal INDEX_PERCENT_CHANGE = 0.001m;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public const int TRADE_QTY = 300;
        public const int RSI_LENGTH = 15;
        public const int RSI_ENTRY_THRESHOLD = 40;
        public const int RSI_EXIT_THRESHOLD = 50;
        public readonly int MAX_DISTANCE_FOR_TRAIL = 400;

        private const int INSTRUMENT_TOKEN = 0;
        private const int INITIAL_TRADED_PRICE = 1;
        private const int CURRENT_PRICE = 2;
        private const int QUANTITY = 3;
        private const int TRADE_ID = 4;
        private const int TRADING_STATUS = 5;
        private const int POSITION_PnL = 6;
        private const int STRIKE = 7;
        private const int PRICEDELTA = 8;
        private bool ContinueTrade = false;
        private bool InitialOI = false;
        private const int CE = 0;
        private const int PE = 1;
        public const AlgoIndex algoIndex = AlgoIndex.SellOnRSICross;
        //public List<string> SubscriptionTokens;
        public List<uint> SubscriptionTokens;

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(OptionSellOnRSICross source);

        [field: NonSerialized]
        public event OnOptionUniverseChangeHandler OnOptionUniverseChange;

        /// TODO: All orders initially should go to database and then it should be loaded here.
        
        public OptionSellOnRSICross(TimeSpan candleTimeSpan, uint baseInstrumentToken, 
            DateTime endTime, DateTime? expiry, int maxDistanceForTrail = 400)
        {
            //TODO: This method would be used to load previously held , but not yet closed, options
            //LoadActiveData();
            //_tradedOptionInstance = new Dictionary<int, TradedOption>();

            _endDateTime = endTime;
            _candleTimeSpan = candleTimeSpan;
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _activeOptions = new List<Instrument>();
            _tradedOptions = new List<TradedOption>();
            MAX_DISTANCE_FOR_TRAIL = maxDistanceForTrail;

            SubscriptionTokens = new List<uint>();

            //CandleOptions = new List<Instrument>();
            TimeCandles = new Dictionary<uint, List<Candle>>();

            //RSI
            tokenRSI = new Dictionary<uint, RelativeStrengthIndex>();

            _RSILoaded = new List<uint>();
            _SQLLoading = new List<uint>();
            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();

            CandleSeries candleSeries = new CandleSeries();

            DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            RelativeStrengthIndex rsi = null;

            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;
            
            _stopTrade = false;
            
            _algoInstance = Utility.GenerateAlgoInstance(algoIndex, endTime);
            ZConnect.ZerodhaLogin();
        }



        ///Steps:
        ///Step 1: Determine position at fixed distance from index. Take initial position based on RSI < 40
        ///Step 2: Monitor till RSI increases to above 50


        private async void LoadOptionsToMonitor(DateTime currentTime)
        {
            int _strikePriceIncrement = 100;
            int maxDistanceFromBaseInstrument = _strikePriceIncrement * 4;

            DataLogic dl = new DataLogic();
            if (OptionUniverse == null ||
            (OptionUniverse[(int)InstrumentType.CE].Keys.First() >= _baseInstrumentPrice + maxDistanceFromBaseInstrument || OptionUniverse[(int)InstrumentType.CE].Keys.Last() <= _baseInstrumentPrice - _strikePriceIncrement * 0
               || OptionUniverse[(int)InstrumentType.PE].Keys.First() >= _baseInstrumentPrice - _strikePriceIncrement * 0 || OptionUniverse[(int)InstrumentType.PE].Keys.Last() <= _baseInstrumentPrice - maxDistanceFromBaseInstrument))
            {
                await LoggerCore.PublishLog(_algoInstance, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                //Load options asynchronously
                OptionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice);

                await LoggerCore.PublishLog(_algoInstance, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");

            }

            //Call & put candidates can be used during any time. So take it fresh everytime and take positions only on these
            callOptions = OptionUniverse[(int)InstrumentType.CE].
            Where(x => x.Key >= _baseInstrumentPrice && x.Key <= _baseInstrumentPrice + maxDistanceFromBaseInstrument).OrderBy(x => x.Key);

            putOptions = OptionUniverse[(int)InstrumentType.PE].
                Where(x => x.Key <= _baseInstrumentPrice && x.Key >= _baseInstrumentPrice - maxDistanceFromBaseInstrument).OrderBy(x => x.Key);

            LoadActiveOptions(_baseInstrumentPrice, callOptions, putOptions);
        }
        private void LoadActiveOptions(decimal baseInstrumentPrice, IOrderedEnumerable<KeyValuePair<decimal, Instrument>> calls, IOrderedEnumerable<KeyValuePair<decimal, Instrument>> puts)
        {
            KeyValuePair<decimal, Instrument> callNode = calls.First();
            KeyValuePair<decimal, Instrument> putNode = puts.Last();
            
            Instrument call = callNode.Value;
            Instrument put = putNode.Value;

            
            _activeOptions.Clear();
            _activeOptions.Add(call);
            _activeOptions.Add(put);
            
            if (_tradedOptions.Count != 0)
            {
                for (int i = 0; i < _activeOptions.Count; i++)
                {
                    Instrument option = _activeOptions[i];
                    if (!_tradedOptions.Any(x => x.Option.InstrumentToken == option.InstrumentToken))
                    {
                        _activeOptions.Remove(option);
                        option = option.InstrumentType.Trim(' ').ToLower() == "ce" ? call : put;
                        _activeOptions.Insert(i, option);
                    }
                }
            }
        }
        


        /// <summary>
        ///
        /// </summary>
        /// <param name="tick"></param>
        private async void ManageTrades(Tick tick)
        {
            DateTime currentTime = tick.Timestamp.Value;
            try
            {
                lock (TimeCandles)
                {
                    //Step 1: Check the base instrument price to load options
                    if (!GetBaseInstrumentPrice(tick))
                    {
                        return;
                    }
                    uint token = tick.InstrumentToken;

                    //Step 2: Load all potentional call/put options for trade. Loading 4+- options
                    LoadOptionsToMonitor(currentTime);

                    UpdateInstrumentSubscription(currentTime);

                    if (token != _baseInstrumentToken && tick.LastTradeTime.HasValue)
                    {
                        //Step 3: If a trade is on, keep updating LTP
                        UpdateTradedOptionPrice(tick);

                        //Step 4: Create/Update Candles
                        MonitorCandles(tick);


                        //Step 5: Update RSI
                        UpdateRSI(tick, false);

                        //Step 6: Check for trailing option
                        TrailMarket(tick);
                    }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.StackTrace);
                Logger.LogWrite("Closing Application");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                await LoggerCore.PublishLog(_algoInstance, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ActiveTradeIntraday");
                //Environment.Exit(0);
                throw ex;
            }

        }
        private void UpdateTradedOptionPrice(Tick tick)
        {
            TradedOption to = _tradedOptions.FirstOrDefault(x => x.Option.InstrumentToken == tick.InstrumentToken && x.TradingStatus == PositionStatus.Open);
            if (to != null)
            {
                to.Option.LastPrice = tick.LastPrice;
            }
        }
        private async void TrailMarket(Tick tick)
        {
            if (_tradedOptions.Count > 0)
            {
                TradedOption tradedCE = _tradedOptions.FirstOrDefault(x => x.TradingStatus == PositionStatus.Open && x.Option.InstrumentType.Trim(' ').ToLower() == "ce");
                TradedOption tradedPE = _tradedOptions.FirstOrDefault(x => x.TradingStatus == PositionStatus.Open && x.Option.InstrumentType.Trim(' ').ToLower() == "pe");

                Instrument atmCE = callOptions.First().Value;
                Instrument atmPE = putOptions.Last().Value;

                RelativeStrengthIndex ce_rsi, pe_rsi;

                if (tradedCE != null && tradedCE.Option.Strike > _baseInstrumentPrice + MAX_DISTANCE_FOR_TRAIL
                    && tokenRSI.TryGetValue(atmCE.InstrumentToken, out ce_rsi) && tokenRSI.TryGetValue(atmPE.InstrumentToken, out pe_rsi)
                    && ce_rsi.GetCurrentValue<decimal>() < pe_rsi.GetCurrentValue<decimal>())
                {
                    PlaceTrailingOrder(tradedCE, atmCE, tick, InstrumentType.CE);
                    tradedCE.TradingStatus = PositionStatus.Closed;
                    //await LoggerCore.PublishLog(_algoInstance, LogLevel.Info, tick.Timestamp.Value, "Trailing Order placed", "TrailMarket");
                }
                else if (tradedPE != null && tradedPE.Option.Strike < _baseInstrumentPrice - MAX_DISTANCE_FOR_TRAIL
                    && tokenRSI.TryGetValue(atmPE.InstrumentToken, out pe_rsi) && tokenRSI.TryGetValue(atmCE.InstrumentToken, out ce_rsi)
                    && pe_rsi.GetCurrentValue<decimal>() < ce_rsi.GetCurrentValue<decimal>())
                {
                    PlaceTrailingOrder(tradedPE, atmPE, tick, InstrumentType.PE);
                    tradedPE.TradingStatus = PositionStatus.Closed;
                    //await LoggerCore.PublishLog(_algoInstance, LogLevel.Info, tick.Timestamp.Value, "Trailing Order placed", "TrailMarket");
                }
            }
        }
        private void PlaceTrailingOrder(TradedOption tradedOption, Instrument newInstrument, Tick tick, InstrumentType instrumentType)
        {
            //exit current Trade
            //Instrument currentInstrument = _tradedOptions[(int)InstrumentType.PE].Option;
            Instrument currentInstrument = tradedOption.Option;

            ShortTrade shortTrade = PlaceOrder(currentInstrument.TradingSymbol, currentInstrument.InstrumentType,
                tradedOption.Option.LastPrice, currentInstrument.InstrumentToken, true,
                TRADE_QTY * Convert.ToInt32(currentInstrument.LotSize), tick.Timestamp);

            shortTrade.TradingStatus = TradeStatus.Closed;
            tradedOption.BuyTrade = shortTrade;
            tradedOption.TradingStatus = PositionStatus.Closed;
            _tradedOptions.Remove(tradedOption);

            //ENTRY Trailing ORDER - SELL ALERT
            //Instrument option = _activeOptions.FirstOrDefault(x => x.InstrumentType.Trim(' ').ToLower() == "pe");

            shortTrade = PlaceOrder(newInstrument.TradingSymbol, newInstrument.InstrumentType, tick.LastPrice,
                newInstrument.InstrumentToken, false, TRADE_QTY * Convert.ToInt32(newInstrument.LotSize), tick.Timestamp);

            TradedOption to = new TradedOption();
            to.Option = newInstrument;
            to.SellTrade = shortTrade;
            to.TradingStatus = PositionStatus.Open;
            _tradedOptions.Add(to);
            
        }

        /// <summary>
        /// Check for RSI crossover
        /// </summary>
        /// <param name="previousCandle"></param>
        /// <param name="option"></param>
        private void TradeEntry(Candle previousCandle, Instrument current)
        {
            uint token = previousCandle.InstrumentToken;

            //Candle previousCandle = TimeCandles[token].LastOrDefault(x => x.State == CandleStates.Finished);
            //Candle currentCandle = TimeCandles[token].Last();

            Instrument opposite = _activeOptions.FirstOrDefault(x => x.InstrumentToken != current.InstrumentToken);

            if (tokenRSI[token].IsFormed && tokenRSI[opposite.InstrumentToken].IsFormed
                && CheckRSICross(current, opposite)
                && !_tradedOptions.Any(x => x.Option.InstrumentType == current.InstrumentType
                && x.TradingStatus != PositionStatus.Closed))
            {
                //ENTRY ORDER - SELL ALERT
                ShortTrade shortTrade = PlaceOrder(current.TradingSymbol, current.InstrumentType, previousCandle.ClosePrice,
                    current.InstrumentToken, false, TRADE_QTY * Convert.ToInt32(current.LotSize), previousCandle.CloseTime);


                if (_tradedOptions.Count > 0)
                {
                    Instrument existingOption = _tradedOptions[0].Option;

                    //Close opposite option type
                    ShortTrade buyTrade = PlaceOrder(existingOption.TradingSymbol, existingOption.InstrumentType, existingOption.LastPrice,
                        existingOption.InstrumentToken, false, TRADE_QTY * Convert.ToInt32(existingOption.LotSize), previousCandle.CloseTime);

                    _tradedOptions.Clear();
                }

                TradedOption to = new TradedOption();
                to.Option = current;
                to.SellTrade = shortTrade;
                to.TradingStatus = PositionStatus.Open;
                _tradedOptions.Add(to);
            }
        }
        private bool CheckRSICross(Instrument current, Instrument opposite)
        {
            uint t1 = current.InstrumentToken;
            uint t2 = opposite.InstrumentToken;

            if ((tokenRSI[t1].GetValue<Decimal>(0) < tokenRSI[t2].GetValue<Decimal>(0)) && (tokenRSI[t1].GetValue<Decimal>(1) > tokenRSI[t2].GetValue<Decimal>(1)))
            {
                return true;
            }
            return false;
        }
        //private void TradeExit(Tick tick)
        //{
        //    try
        //    {
        //        uint token = 0;
        //        //foreach (Tick tick in ticks)
        //        //{
        //        token = tick.InstrumentToken;
        //        TradedOption option = _tradedOptions.FirstOrDefault(x => x.Option.InstrumentToken == token && x.TradingStatus == PositionStatus.Open);

        //        if (option != null &&
        //            tokenRSI[token].GetCurrentValue<Decimal>() > RSI_EXIT_THRESHOLD)
        //        {
        //            //exit trade
        //            ShortTrade shortTrade = PlaceOrder(option.Option.TradingSymbol, option.Option.InstrumentType, tick.LastPrice, token, true,
        //                TRADE_QTY * Convert.ToInt32(option.Option.LotSize), tick.Timestamp);
        //            shortTrade.TradingStatus = TradeStatus.Closed;

        //            option.BuyTrade = shortTrade;
        //            option.TradingStatus = PositionStatus.Closed;
        //        }
        //        //}
        //    }
        //    catch (Exception exp)
        //    {

        //    }
        //}
        private async void MonitorCandles(Tick tick)
        {
            uint token = tick.InstrumentToken;
            //Check the below statement, this should not keep on adding to 
            //TimeCandles with everycall, as the list doesnt return new candles unless built
            if (TimeCandles.ContainsKey(token))
            {
                candleManger.StreamingShortTimeFrameCandle(tick, token, _candleTimeSpan, true); // TODO: USING LOCAL VERSION RIGHT NOW
            }
            else
            {
                DateTime lastCandleEndTime;
                DateTime? candleStartTime = CheckCandleStartTime(tick.Timestamp.Value, out lastCandleEndTime);

                if (candleStartTime.HasValue)
                {
                    await LoggerCore.PublishLog(_algoInstance, LogLevel.Info, tick.Timestamp.Value, "Starting first Candle now", "MonitorCandles");
                    //candle starts from there
                    candleManger.StreamingShortTimeFrameCandle(tick, token, _candleTimeSpan, true, candleStartTime); // TODO: USING LOCAL VERSION
                }
            }
        }

        private void UpdateRSI(Tick tick, bool isFinal)
        {
            //foreach (Tick tick in ticks)
            //{
            if (!_RSILoaded.Contains(tick.InstrumentToken))
            {
                //this method can now be slow doenst matter. 
                //make it async
                LoadHistoricalRSIs(tick.Timestamp.Value);
            }

            if (tokenRSI.ContainsKey(tick.InstrumentToken))
            {
                tokenRSI[tick.InstrumentToken].Process(tick.LastPrice, isFinal: isFinal);
            }
            //}
        }

        private async void LoadHistoricalRSIs(DateTime currentTime)
        {
            DateTime lastCandleEndTime;
            DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

            var tokens = SubscriptionTokens.Where(x => x != _baseInstrumentToken && !_RSILoaded.Contains(x));

            StringBuilder sb = new StringBuilder();
            foreach (uint t in tokens)
            {
                if (!_firstCandleOpenPriceNeeded.ContainsKey(t))
                {
                    _firstCandleOpenPriceNeeded.Add(t, candleStartTime != lastCandleEndTime);
                }
                if (!tokenRSI.ContainsKey(t) && !_SQLLoading.Contains(t))
                {
                    sb.AppendFormat("{0},", t);
                    _SQLLoading.Add(t);
                }
            }
            string tokenList = sb.ToString().TrimEnd(',');

            int firstCandleFormed = 0; //historicalPricesLoaded = 0;
                                       //if (!tokenRSI.ContainsKey(token) && !_SQLLoading.Contains(token))
                                       //{
                                       //_SQLLoading.Add(token);
            if (tokenList != string.Empty)
            {
                Task task = Task.Run(() => LoadHistoricalCandles(tokenList, 15, lastCandleEndTime));
            }

            //LoadHistoricalCandles(token, LONG_EMA, lastCandleEndTime);
            //historicalPricesLoaded = 1;
            //}
            foreach (uint tkn in tokens)
            {
                //if (tk != string.Empty)
                //{
                   // uint tkn = Convert.ToUInt32(tk);


                    if (TimeCandles.ContainsKey(tkn) && tokenRSI.ContainsKey(tkn))
                    {
                        if (_firstCandleOpenPriceNeeded[tkn])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            tokenRSI[tkn].Process(TimeCandles[tkn].First().OpenPrice, isFinal: true);
                            firstCandleFormed = 1;

                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[tkn].Count > 1)
                        {
                            foreach (var price in TimeCandles[tkn])
                            {
                                tokenRSI[tkn].Process(TimeCandles[tkn].First().ClosePrice, isFinal: true);
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[tkn]) && tokenRSI.ContainsKey(tkn))
                    {
                        _RSILoaded.Add(tkn);
                    await LoggerCore.PublishLog(_algoInstance, LogLevel.Info, currentTime, String.Format("RSI loaded from DB for {0}", tkn), "MonitorCandles");
                }
                    //}
               // }
            }
        }

        private DateTime? CheckCandleStartTime(DateTime currentTime, out DateTime lastEndTime)
        {
            double mselapsed = (currentTime.TimeOfDay - MARKET_START_TIME).TotalMilliseconds % _candleTimeSpan.TotalMilliseconds;
            DateTime? candleStartTime = null;
            //if(mselapsed < 1000) //less than a second
            //{
            //    candleStartTime =  currentTime;
            //}
            if (mselapsed < 60 * 1000)
            {
                candleStartTime = currentTime.Date.Add(TimeSpan.FromMilliseconds(currentTime.TimeOfDay.TotalMilliseconds - mselapsed));
            }
            //else
            //{
            lastEndTime = currentTime.Date.Add(TimeSpan.FromMilliseconds(currentTime.TimeOfDay.TotalMilliseconds - mselapsed));
            //}

            return candleStartTime;
        }

        #region Historical Candle 
        private void LoadHistoricalCandles(string tokenList, int candlesCount, DateTime lastCandleEndTime)
        {
           
                DataLogic dl = new DataLogic();

                Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, tokenList, _candleTimeSpan);
                RelativeStrengthIndex rsi;

            lock (tokenRSI)
            {
                foreach (uint t in historicalCandlePrices.Keys)
                {
                    rsi = new RelativeStrengthIndex();
                    foreach (var price in historicalCandlePrices[t])
                    {
                        rsi.Process(price, isFinal: true);
                    }
                    tokenRSI.Add(t, rsi);
                }
            }
        }
        #endregion

        private void LoadHistoricalCandles(DateTime? currentTime, uint token)
        {
            DataLogic dl = new DataLogic();

            CandleSeries candleSeries = new CandleSeries();

            //This time should be current time so that all historical candles can be retrieved
            DateTime ydayEndTime = DateTime.Now;

            //TEST: Remove on live ticks
            ydayEndTime = currentTime.Value;
            //ydayEndTime = currentTime.GetValueOrDefault(DateTime.Now);

            RelativeStrengthIndex rsi = null;

            //foreach (uint token in _activeOptions.Select(x => x.InstrumentToken))
            //{
            List<Candle> historicalCandles = candleSeries.LoadCandles(RSI_LENGTH, CandleType.Time, ydayEndTime, token, _candleTimeSpan);

            TimeCandles.Add(token, historicalCandles);

            rsi = new RelativeStrengthIndex();

            foreach (var candle in historicalCandles)
            {
                rsi.Process(candle.ClosePrice, isFinal: true);
            }
            tokenRSI.Add(token, rsi);
            //}
        }
        //private void LoadHistoricalCandles(DateTime? currentTime)
        //{
        //    DataLogic dl = new DataLogic();

        //    CandleSeries candleSeries = new CandleSeries();

        //    //This time should be current time so that all historical candles can be retrieved
        //    DateTime ydayEndTime = DateTime.Now;

        //    //TEST: Remove on live ticks
        //    ydayEndTime = currentTime.Value;
        //    //ydayEndTime = currentTime.GetValueOrDefault(DateTime.Now);

        //    RelativeStrengthIndex rsi = null;

        //    foreach (uint token in _activeOptions.Select(x => x.InstrumentToken))
        //    {
        //        List<Candle> historicalCandles = candleSeries.LoadCandles(RSI_LENGTH, CandleType.Time, ydayEndTime, token, _candleTimeSpan);

        //        TimeCandles.Add(token, historicalCandles);

        //        rsi = new RelativeStrengthIndex();

        //        foreach (var candle in historicalCandles)
        //        {
        //            rsi.Process(candle.ClosePrice, isFinal: true);
        //        }
        //        tokenRSI.Add(token, rsi);
        //    }
        //}
        private bool GetBaseInstrumentPrice(Tick tick)
        {
            //Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == _baseInstrumentToken);

            if (tick.InstrumentToken == _baseInstrumentToken && tick.LastPrice != 0)  //(strangleNode.BaseInstrumentPrice == 0)// * callOption.LastPrice * putOption.LastPrice == 0)
            {
                _baseInstrumentPrice = tick.LastPrice;
            }
            if (_baseInstrumentPrice == 0)
            {
                return false;
            }
            return true;
        }
        //private DateTime? StartCandleStreaming(DateTime currentTime)
        //{
        //    double mselapsed = (currentTime.TimeOfDay - MARKET_START_TIME).TotalMilliseconds % _candleTimeSpan.TotalMilliseconds;
        //    DateTime? candleStartTime = null;
        //    if (mselapsed < 1000) //less than a second
        //    {
        //        candleStartTime = currentTime;
        //    }
        //    else if (mselapsed < 60 * 1000)
        //    {
        //        candleStartTime = currentTime.Date.Add(TimeSpan.FromMilliseconds(currentTime.TimeOfDay.TotalMilliseconds - mselapsed));
        //    }

        //    return candleStartTime;
        //}
        private async void UpdateInstrumentSubscription(DateTime currentTime)
        {
            bool dataUpdated = false;
            //foreach (var option in CandleOptions)
            foreach (var optionList in OptionUniverse)
                foreach (var option in optionList)
                {
                    if (!SubscriptionTokens.Contains(option.Value.InstrumentToken))
                    {
                        SubscriptionTokens.Add(option.Value.InstrumentToken);
                        dataUpdated = true;
                    }
                }
            if (dataUpdated)
            {
                await LoggerCore.PublishLog(_algoInstance, LogLevel.Info, currentTime, "Subscribing to new tokens", "UpdateInstrumentSubscription");
                Task task = Task.Run(() => OnOptionUniverseChange(this));
            }
        }
        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            tokenRSI[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);

            ///putting trade entry at candle close only

            Instrument option = _activeOptions.FirstOrDefault(x => x.InstrumentToken == e.InstrumentToken);
            if (option != null)
            {
                TradeEntry(e, option);
            }
        }

        private ShortTrade PlaceOrder(string instrument_tradingsymbol, string instrumenttype, decimal instrument_currentPrice, uint instrument_Token,
           bool buyOrder, int quantity, DateTime? tickTime = null)
        {
            string tradingSymbol = instrument_tradingsymbol;
            decimal currentPrice = instrument_currentPrice;
            //Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
            //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, quantity, Product: Constants.PRODUCT_MIS,
            //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            ///TEMP, REMOVE Later
            if (currentPrice == 0)
            {
                DataLogic dl = new DataLogic();
                currentPrice = dl.RetrieveLastPrice(instrument_Token, tickTime, buyOrder);
            }

            string orderId = "0";
            decimal averagePrice = 0;
            //if (orderStatus["data"]["order_id"] != null)
            //{
            //    orderId = orderStatus["data"]["order_id"];
            //}
            if (orderId != "0")
            {
                System.Threading.Thread.Sleep(200);
                List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
                averagePrice = orderInfo[orderInfo.Count - 1].AveragePrice;
            }
            if (averagePrice == 0)
                averagePrice = buyOrder ? currentPrice : currentPrice;
            // averagePrice = buyOrder ? averagePrice * -1 : averagePrice;

            ShortTrade trade = new ShortTrade();
            trade.InstrumentToken = instrument_Token;
            trade.AveragePrice = averagePrice;
            trade.ExchangeTimestamp = tickTime;// DateTime.Now;
            trade.TradeTime = tickTime ?? DateTime.Now;
            trade.Quantity = quantity;
            trade.OrderId = orderId;
            trade.TransactionType = buyOrder ? "Buy" : "Sell";
            trade.TriggerID = Convert.ToInt32(AlgoIndex.VolumeThreshold);
            trade.TradingStatus = buyOrder ? TradeStatus.Closed : TradeStatus.Open;
            trade.InstrumentType = instrumenttype;

            UpdateTradeDetails(strategyID: 0, instrument_Token, quantity, trade, Convert.ToInt32(trade.TriggerID));

            return trade;
        }
        private void UpdateTradeDetails(int strategyID, uint instrumentToken, int tradedLot, ShortTrade trade, int triggerID)
        {
            DataLogic dl = new DataLogic();
            dl.UpdateTrade(strategyID, instrumentToken, trade, AlgoIndex.ExpiryTrade, tradedLot, triggerID);
        }

        private void UpdateTokenSubcription()
        {
            //ZMQClient.ZMQSubcribebyToken(this, SubscriptionTokens.ToArray());
        }
        public SortedList<Decimal, Instrument>[] GetNewStrikes(uint baseInstrumentToken, decimal baseInstrumentPrice, DateTime? expiry)
        {
            DataLogic dl = new DataLogic();
            SortedList<Decimal, Instrument>[] nodeData = dl.RetrieveNextStrangleNodes(baseInstrumentToken, expiry.GetValueOrDefault(DateTime.Now), baseInstrumentPrice, baseInstrumentPrice, 0);
            return nodeData;
        }

        public virtual async Task<bool> OnNext(Tick[] ticks)
        {
            ManageTrades(ticks[0]);
            return true;
        }


        public virtual void OnError(Exception ex)
        {
            ///TODO: Log the error. Also handle the error.
        }


        public virtual void OnCompleted()
        {
        }
    }
}
