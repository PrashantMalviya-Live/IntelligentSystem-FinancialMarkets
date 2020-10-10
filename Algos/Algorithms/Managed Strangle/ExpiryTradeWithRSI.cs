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
using System.Runtime.InteropServices.WindowsRuntime;

namespace Algos.TLogics
{
    public class ExpiryTradeWithRSI : IZMQ
    {
        private readonly int _algoInstance;

        SortedList<decimal, Instrument> callOptions;
        SortedList<decimal, Instrument> putOptions;
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }
        //public List<Instrument> CandleOptions { get; set; }

        List<Instrument> _activeOptions;
        List<TradedOption> _tradedOptions;

        public decimal _baseInstrumentPrice;
        public uint _baseInstrumentToken;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        DateTime _endDateTime;
        Dictionary<uint, RelativeStrengthIndex> tokenRSI;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;

        public const int CANDLE_COUNT = 14;
        public const decimal INDEX_PERCENT_CHANGE = 0.001m;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public const int TRADE_QTY = 300;
        public const int RSI_LENGTH = 15;
        public const int RSI_ENTRY_THRESHOLD = 40;
        public const int RSI_EXIT_THRESHOLD = 50;

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

        public const AlgoIndex algoIndex = AlgoIndex.StrangleWithRSI;
        //public List<string> SubscriptionTokens;
        public List<uint> SubscriptionTokens;

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(ExpiryTradeWithRSI source);

        [field: NonSerialized]
        public event OnOptionUniverseChangeHandler OnOptionUniverseChange;

        public ExpiryTradeWithRSI(TimeSpan candleTimeSpan, uint baseInstrumentToken, DateTime endTime, DateTime? expiry)
        {
            //LoadActiveData();
            _endDateTime = endTime;
            _candleTimeSpan = candleTimeSpan;
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _activeOptions = new List<Instrument>();
            _tradedOptions = new List<TradedOption>();

            SubscriptionTokens = new List<uint>();

            //CandleOptions = new List<Instrument>();
            TimeCandles = new Dictionary<uint, List<Candle>>();

            //RSI
            tokenRSI = new Dictionary<uint, RelativeStrengthIndex>();

            CandleSeries candleSeries = new CandleSeries();

            DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            RelativeStrengthIndex rsi = null;

            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

            _algoInstance = Utility.GenerateAlgoInstance(algoIndex, endTime);
        }

       

        ///Steps:
        ///Step 1: Determine position at fixed distance from index. Take initial position based on RSI < 40
        ///Step 2: Monitor till RSI increases to above 50

        private async void LoadOptionsToTrade(uint token, DateTime currentTime)
        {
            decimal minDistanceFromBaseInstrument = 120;
            decimal maxDistanceFromBaseInstrument = 300;

            var ceStrike = Math.Ceiling(_baseInstrumentPrice / 100m) * 100m;
            var peStrike = Math.Floor(_baseInstrumentPrice / 100m) * 100m;

            //if (ActiveOptions.Count > 0 && ActiveOptions.Any(x => Math.Abs(x.Strike - _baseInstrumentPrice) < _strikePriceIncrement * 2.7m))
            if ((_activeOptions.Count > 0
                && _activeOptions.Any(x => ((x.Strike - _baseInstrumentPrice) > minDistanceFromBaseInstrument * 0.67m && (x.Strike - _baseInstrumentPrice) < maxDistanceFromBaseInstrument * 1.2m) && (x.InstrumentType.Trim(' ').ToLower() == "ce"))
                && _activeOptions.Any(x => ((_baseInstrumentPrice - x.Strike) > minDistanceFromBaseInstrument * 0.67m && (_baseInstrumentPrice - x.Strike) < maxDistanceFromBaseInstrument * 1.2m) && (x.InstrumentType.Trim(' ').ToLower() == "pe"))
                ) //|| (_tradedOptions.Count > 0 && (_tradedOptions[(int)InstrumentType.CE].Option.InstrumentToken == token || _tradedOptions[(int)InstrumentType.PE].Option.InstrumentToken == token))
                )
            {
                return;
            }
            else
            {
                DataLogic dl = new DataLogic();

                if (OptionUniverse == null || (OptionUniverse[(int)InstrumentType.CE].Keys.First() >_baseInstrumentPrice + maxDistanceFromBaseInstrument &&
                    OptionUniverse[(int)InstrumentType.CE].Keys.Last() < _baseInstrumentPrice + minDistanceFromBaseInstrument) || (
                   OptionUniverse[(int)InstrumentType.PE].Keys.First() > _baseInstrumentPrice - maxDistanceFromBaseInstrument &&
                   OptionUniverse[(int)InstrumentType.PE].Keys.Last() < _baseInstrumentPrice - minDistanceFromBaseInstrument)
                   )
                {
                    await LoggerCore.PublishLog(_algoInstance, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    OptionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice);
                    await LoggerCore.PublishLog(_algoInstance, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");
                }

                ////TODO: Check for exception if less than 2 items are available.
                //var activeCEs = OptionUniverse[(int)InstrumentType.CE].Where(x => x.Key > _baseInstrumentPrice).Take(1);
                //var activePEs = OptionUniverse[(int)InstrumentType.PE].Where(x => x.Key < _baseInstrumentPrice).Reverse().Take(1);


                var callCandidates = OptionUniverse[(int)InstrumentType.CE].
                Where(x => x.Key > _baseInstrumentPrice + minDistanceFromBaseInstrument && x.Key < _baseInstrumentPrice + maxDistanceFromBaseInstrument).OrderBy(x => x.Key);

                var putCandidates = OptionUniverse[(int)InstrumentType.PE].
                    Where(x => x.Key < _baseInstrumentPrice - minDistanceFromBaseInstrument && x.Key > _baseInstrumentPrice - maxDistanceFromBaseInstrument).OrderBy(x => x.Key);

                KeyValuePair<decimal, Instrument> callNode = callCandidates.First();

                KeyValuePair<decimal, Instrument> putNode = putCandidates.Last();

                Instrument call = callNode.Value;
                Instrument put = putNode.Value;


                ///issue with below code is that if we switch between 30 mins, 
                ///then candle wait for 30 misn to generate
                //TODO: Check for exception if less than 2 items are available.
                //var candleCEs = callCandidates.Take(2);
                //var candlePEs = putCandidates.Reverse().Take(2);
                //CandleOptions.Clear();
                //CandleOptions.AddRange(candleCEs.Select(x => x.Value));
                //CandleOptions.AddRange(candlePEs.Select(x => x.Value));


                //foreach (var tt in _tradedOptions)
                //{
                //    Instrument option = tt.Option;
                //    if (!CandleOptions.Any(x => x.InstrumentToken == option.InstrumentToken))
                //    {
                //        Instrument i;
                //        if (OptionUniverse[(int)InstrumentType.CE].Any(x => x.Value.InstrumentToken == option.InstrumentToken))
                //        {
                //            i = OptionUniverse[(int)InstrumentType.CE].FirstOrDefault(x => x.Value.InstrumentToken == option.InstrumentToken).Value;
                //        }
                //        else
                //        {
                //            i = OptionUniverse[(int)InstrumentType.PE].FirstOrDefault(x => x.Value.InstrumentToken == option.InstrumentToken).Value;
                //        }

                //        CandleOptions.Add(i);
                //    }
                //}

                //First time.
                if (_activeOptions.Count == 0)
                {
                    _activeOptions.Add(call);
                    _activeOptions.Add(put);
                }
                else
                {
                    for (int i = 0; i < _activeOptions.Count; i++)
                    {
                        Instrument option = _activeOptions[i];
                        if (!_tradedOptions.Any(x=>x.Option.InstrumentToken == option.InstrumentToken))
                        {
                            _activeOptions.Remove(option);
                            option = option.InstrumentType.Trim(' ').ToLower() == "ce" ? call : put;
                            _activeOptions.Insert(i, option);

                            ///TODO: after updating this value, check if ActiveOptions gets update or not.
                        }
                    }
                }
            }
        }

        private void ManageTrades(Tick tick)
        {

            //Stopwatch st = new Stopwatch();
            //st.Start();
            //long startTime = st.ElapsedMilliseconds;
            uint token = tick.InstrumentToken;
            DateTime currentTime = tick.Timestamp.Value;
            try
            {
                lock (TimeCandles)
                {
                    if (!GetBaseInstrumentPrice(tick))
                    {
                        return;
                    }
                    
                    LoadOptionsToTrade(token, currentTime);

                    UpdateInstrumentSubscription(currentTime);

                    if (token != _baseInstrumentToken && tick.LastTradeTime.HasValue)
                    {
                        //if (OptionUniverse[0].Any(x => x.Value.InstrumentToken == token) ||
                        //OptionUniverse[1].Any(x => x.Value.InstrumentToken == token)
                        //)
                        //{
                            UpdateTradedOptionPrice(tick);
                            MonitorCandles(tick);
                            UpdateRSI(tick, false);
                        //}
                        if (_activeOptions.Any(x => x.InstrumentToken == token))
                        {
                            //if (TimeCandles.ContainsKey(token))
                            //{
                            TradeEntry(tick);
                            TradeExit(tick);
                            //Trail the market
                            TrailMarket(tick);
                            //}
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
            //long endTime = st.ElapsedMilliseconds;
            //st.Stop();
            //Console.WriteLine(endTime - startTime);

            //if (endTime - startTime > 20)
            //{
                
            //}
        }
        private void UpdateTradedOptionPrice(Tick tick)
        {
            TradedOption to = _tradedOptions.FirstOrDefault(x => x.Option.InstrumentToken == tick.InstrumentToken && x.TradingStatus == PositionStatus.Open);
            if (to != null)
            {
                to.Option.LastPrice = tick.LastPrice;
            }
        }
        private void TrailMarket(Tick tick)
        {
            if (_tradedOptions.Count > 0)
            {
                TradedOption tradedCE = _tradedOptions.FirstOrDefault(x => x.TradingStatus == PositionStatus.Open && x.Option.InstrumentType.Trim(' ').ToLower() == "ce");
                TradedOption tradedPE = _tradedOptions.FirstOrDefault(x => x.TradingStatus == PositionStatus.Open && x.Option.InstrumentType.Trim(' ').ToLower() == "pe");

                Instrument activeCE = _activeOptions.FirstOrDefault(x => x.InstrumentType.Trim(' ').ToLower() == "ce");
                Instrument activePE = _activeOptions.FirstOrDefault(x => x.InstrumentType.Trim(' ').ToLower() == "pe");

                if( tradedCE != null && tradedCE.Option.InstrumentToken != activeCE.InstrumentToken 
                    && tokenRSI.ContainsKey(activeCE.InstrumentToken) && tokenRSI[activeCE.InstrumentToken].GetCurrentValue<decimal>() < RSI_ENTRY_THRESHOLD)
                {
                    PlaceTrailingOrder(tradedCE, activeCE, tick, InstrumentType.CE);
                    tradedCE.TradingStatus = PositionStatus.Closed;
                }
                if (tradedPE != null && tradedPE.Option.InstrumentToken != activePE.InstrumentToken
                    && tokenRSI.ContainsKey(activePE.InstrumentToken) 
                    && tokenRSI[activePE.InstrumentToken].GetCurrentValue<decimal>() < RSI_ENTRY_THRESHOLD)
                {
                    PlaceTrailingOrder(tradedPE, activePE, tick, InstrumentType.PE);
                    tradedPE.TradingStatus = PositionStatus.Closed;
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
                TRADE_QTY * Convert.ToInt32(currentInstrument.LotSize), tick.LastTradeTime);
            
            shortTrade.TradingStatus = TradeStatus.Closed;
            tradedOption.BuyTrade = shortTrade;
            tradedOption.TradingStatus = PositionStatus.Closed;

            //ENTRY Trailing ORDER - SELL ALERT
            //Instrument option = _activeOptions.FirstOrDefault(x => x.InstrumentType.Trim(' ').ToLower() == "pe");

            shortTrade = PlaceOrder(newInstrument.TradingSymbol, newInstrument.InstrumentType, tick.LastPrice,
                newInstrument.InstrumentToken, false, TRADE_QTY * Convert.ToInt32(newInstrument.LotSize), tick.LastTradeTime);

            TradedOption to = new TradedOption();
            to.Option = newInstrument;
            to.SellTrade = shortTrade;
            to.TradingStatus = PositionStatus.Open;
            _tradedOptions.Add(to);
        }
        private void TradeEntry(Tick tick)
        {
            uint token = tick.InstrumentToken;
            Instrument option = _activeOptions.FirstOrDefault(x => x.InstrumentToken == token);

            Candle previousCandle = TimeCandles[token].LastOrDefault(x => x.State == CandleStates.Finished);
            Candle currentCandle = TimeCandles[token].Last();

            if (previousCandle != null
                && option != null
                && tokenRSI[token].IsFormed
                && tokenRSI[token].GetCurrentValue<Decimal>() < RSI_ENTRY_THRESHOLD
                &&! _tradedOptions.Any(x => x.Option.InstrumentType == option.InstrumentType
                && x.TradingStatus != PositionStatus.Closed))
            {
                //ENTRY ORDER - SELL ALERT
                ShortTrade shortTrade = PlaceOrder(option.TradingSymbol, option.InstrumentType, tick.LastPrice,
                    token, false, TRADE_QTY * Convert.ToInt32(option.LotSize), tick.LastTradeTime);

                TradedOption to = new TradedOption();
                to.Option = option;
                to.SellTrade = shortTrade;
                to.TradingStatus = PositionStatus.Open;
                _tradedOptions.Add(to);
            }
        }
        private void TradeExit(Tick tick)
        {
            try
            {
                uint token = 0;
                //foreach (Tick tick in ticks)
                //{
                    token = tick.InstrumentToken;
                    TradedOption option = _tradedOptions.FirstOrDefault(x => x.Option.InstrumentToken == token && x.TradingStatus == PositionStatus.Open);
                   
                    if (option != null &&
                        tokenRSI[token].GetCurrentValue<Decimal>() > RSI_EXIT_THRESHOLD)
                    {
                        //exit trade
                        ShortTrade shortTrade = PlaceOrder(option.Option.TradingSymbol, option.Option.InstrumentType, tick.LastPrice, token, true,
                            TRADE_QTY * Convert.ToInt32(option.Option.LotSize), tick.LastTradeTime);
                        shortTrade.TradingStatus = TradeStatus.Closed;

                        option.BuyTrade = shortTrade;
                        option.TradingStatus = PositionStatus.Closed;
                    }
                //}
            }
            catch (Exception exp)
            {

            }
        }
        private async void MonitorCandles(Tick tick)
        {
            uint token = tick.InstrumentToken;
            //check the below statement, this should not keep on adding to TimeCandles with everycall, as the list doesnt return new candles unless built

            if (TimeCandles.ContainsKey(token))
            {
                //TimeCandles[token] = candleManger.StreamingTimeFrameCandle(ticks, token, _candleTimeSpan, true); // TODO: USING LOCAL VERSION RIGHT NOW
                candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpan, true); // TODO: USING LOCAL VERSION RIGHT NOW

                //tokenRSI[token].Process(TimeCandles[token].Last().ClosePrice, isFinal: false);
            }
            else
            {
                DateTime? candleStartTime = StartCandleStreaming(tick.LastTradeTime.Value);

                //if (StartCandleStreaming(ticks[0].Timestamp.Value))
                if (candleStartTime.HasValue)
                {
                    //TimeCandles.Add(token, candleManger.StreamingTimeFrameCandle(ticks, token, _candleTimeSpan, true)); // TODO: USING LOCAL VERSION RIGHT NOW

                    await LoggerCore.PublishLog(_algoInstance, LogLevel.Info, tick.Timestamp.Value, String.Format("Loading historical candles for {0}", token), "MonitorCandles");
                    //First Add historical candles
                    LoadHistoricalCandles(candleStartTime, token);

                    await LoggerCore.PublishLog(_algoInstance, LogLevel.Info, tick.Timestamp.GetValueOrDefault(DateTime.UtcNow), "Starting first Candle now", "MonitorCandles");
                    candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpan, true, candleStartTime); // TODO: USING LOCAL VERSION RIGHT NOW
                    //tokenRSI.Add(token, new RelativeStrengthIndex());

                    //tokenRSI[token].Process(TimeCandles[token].Last().ClosePrice, isFinal: false);
                }
            }
        }

        private void UpdateRSI(Tick tick, bool isFinal)
        {
            //foreach (Tick tick in ticks)
            //{
                if (tokenRSI.ContainsKey(tick.InstrumentToken))
                {
                    tokenRSI[tick.InstrumentToken].Process(tick.LastPrice, isFinal: isFinal);
                }
            //}
        }

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
        private void LoadHistoricalCandles(DateTime? currentTime)
        {
            DataLogic dl = new DataLogic();

            CandleSeries candleSeries = new CandleSeries();

            //This time should be current time so that all historical candles can be retrieved
            DateTime ydayEndTime = DateTime.Now;

            //TEST: Remove on live ticks
            ydayEndTime = currentTime.Value;
            //ydayEndTime = currentTime.GetValueOrDefault(DateTime.Now);

            RelativeStrengthIndex rsi = null;

            foreach (uint token in _activeOptions.Select(x => x.InstrumentToken))
            {
                List<Candle> historicalCandles = candleSeries.LoadCandles(RSI_LENGTH, CandleType.Time, ydayEndTime, token, _candleTimeSpan);

                TimeCandles.Add(token, historicalCandles);

                rsi = new RelativeStrengthIndex();

                foreach (var candle in historicalCandles)
                {
                    rsi.Process(candle.ClosePrice, isFinal: true);
                }
                tokenRSI.Add(token, rsi);
            }
        }
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
        private DateTime? StartCandleStreaming(DateTime currentTime)
        {
            double mselapsed = (currentTime.TimeOfDay - MARKET_START_TIME).TotalMilliseconds % _candleTimeSpan.TotalMilliseconds;
            DateTime? candleStartTime = null;
            if (mselapsed < 1000) //less than a second
            {
                candleStartTime = currentTime;
            }
            else if (mselapsed < 60 * 1000)
            {
                candleStartTime = currentTime.Date.Add(TimeSpan.FromMilliseconds(currentTime.TimeOfDay.TotalMilliseconds - mselapsed));
            }

            return candleStartTime;
        }
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
            trade.TriggerID = Convert.ToInt32(algoIndex);
            trade.TradingStatus = buyOrder ? TradeStatus.Closed : TradeStatus.Open;
            trade.InstrumentType = instrumenttype;

            UpdateTradeDetails(strategyID: 0, instrument_Token, quantity, trade, Convert.ToInt32(trade.TriggerID));

            return trade;
        }
        private void UpdateTradeDetails(int strategyID, uint instrumentToken, int tradedLot, ShortTrade trade, int triggerID)
        {
            DataLogic dl = new DataLogic();
            dl.UpdateTrade(strategyID, instrumentToken, trade, algoIndex, tradedLot, triggerID);
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
            if (ticks[0].Timestamp.HasValue)
            {
                ManageTrades(ticks[0]);
            }
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
