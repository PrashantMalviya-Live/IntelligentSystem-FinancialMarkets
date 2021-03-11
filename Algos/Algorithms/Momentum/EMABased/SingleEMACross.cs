using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZMQFacade;
using GlobalLayer;
using Algorithms.Indicators;
using Algorithms.Utils;
using Algorithms.Candles;
using System.Runtime.Serialization.Json;
using System.Text.Json;
using Algorithms.Utilities;
//using Apache.Ignite.Core.Messaging;
//using Apache.Ignite.Core.Cache.Event;
namespace Algos.TLogics
{
    public class SingleEMACross : IZMQ//, ITibcoListener//, ICacheEntryEventListener<TickKey, Tick>, IMessageListener<Tick> //, IObserver<Tick[]>
    {
        TimeSpan timeFrame;
        CandleManger candleManger;
        //List<TimeFrameCandle> timeCandles;
        //List<VolumeCandle> volumeCandles;
        //List<MoneyCandle> moneyCandles;
        Dictionary<int, List<Candle>> TypeCandles;
        
        Dictionary<int, ExponentialMovingAverage> shortEMA;
        Dictionary<int, ExponentialMovingAverage> longEMA;

        //ExponentialMovingAverage tema1, tema2, vema1, vema2, mema1, mema2;

        //public List<string> SubscriptionTokens;
        public List<uint> SubscriptionTokens;

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(SingleEMACross source);

        [field: NonSerialized]
        public event OnOptionUniverseChangeHandler OnOptionUniverseChange;

        uint _instrumentToken = 0;
        string _tradingSymbol = "";
        AlgoPosition ActivePosition;
        Dictionary<int, List<AlgoPosition>>ActivePositions;
        int _stepQty = 100;
        int _maxQty = 100;
        bool temaFormed = false, vemaFormed = false, memaFormed = false;
        DataLogic dataLogic = new DataLogic();
        public SingleEMACross(uint instrumentToken, TimeSpan candleTimeSpan, int fEma, int sEMA)
        {
            //ActivePositions = new Dictionary<uint, Position>();
            ActivePosition = new AlgoPosition();
            ActivePositions = new Dictionary<int, List<AlgoPosition>>(); //List<AlgoPosition>();
            _instrumentToken = instrumentToken;
            timeFrame = candleTimeSpan;
            candleManger = new CandleManger();
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;
            candleManger.VolumeCandleFinished += CandleManger_VolumeCandleFinished;
            candleManger.MoneyCandleFinished += CandleManger_MoneyCandleFinished;

            TypeCandles = new Dictionary<int, List<Candle>>();
            TypeCandles.Add((int)CandleType.Time, new List<Candle>());
            TypeCandles.Add((int)CandleType.Volume, new List<Candle>());
            TypeCandles.Add((int)CandleType.Money, new List<Candle>());

            //timeCandles = new List<TimeFrameCandle>();
            //volumeCandles = new List<VolumeCandle>();

            shortEMA = new Dictionary<int, ExponentialMovingAverage>();
            longEMA = new Dictionary<int, ExponentialMovingAverage>();

            shortEMA.Add((int)CandleType.Time, new ExponentialMovingAverage(fEma));
            shortEMA.Add((int)CandleType.Volume, new ExponentialMovingAverage(fEma));
            shortEMA.Add((int)CandleType.Money, new ExponentialMovingAverage(fEma));

            longEMA.Add((int)CandleType.Time, new ExponentialMovingAverage(sEMA));
            longEMA.Add((int)CandleType.Volume, new ExponentialMovingAverage(sEMA));
            longEMA.Add((int)CandleType.Money, new ExponentialMovingAverage(sEMA));

            //tema1 = new ExponentialMovingAverage(fEma);
            //tema2 = new ExponentialMovingAverage(sEMA);

            //vema1 = new ExponentialMovingAverage(fEma);
            //vema2 = new ExponentialMovingAverage(sEMA);

            //mema1 = new ExponentialMovingAverage(fEma);
            //mema2 = new ExponentialMovingAverage(sEMA);

            DateTime currentDatetime = DateTime.Now; // Convert.ToDateTime("2019-12-26 10:30:00"); //DateTime.Now //TODO: To be updated
            CandleSeries candleSeries = new CandleSeries();
            TypeCandles[(int)CandleType.Time] = candleSeries.LoadCandles(Math.Max(sEMA, fEma), CandleType.Time, currentDatetime, _instrumentToken.ToString(), timeFrame);

            foreach (var typeCandle in TypeCandles)
            {
                var candles = typeCandle.Value;
                foreach (var candle in candles)
                {
                    var fema = shortEMA[(int)candle.CandleType].Process(candle.ClosePrice, isFinal: true);
                    var sema = longEMA[(int)candle.CandleType].Process(candle.ClosePrice, isFinal: true);
                }
            }
        }

        ///// <summary>
        ///// Load historical 1 min candles from the DB.
        ///// </summary>
        ///// <param name="numberofCandles"></param>
        ///// <param name="candleType"></param>
        //public void LoadCandles(int numberofCandles, CandleType candleType)
        //{
        //    DataLogic dl = new DataLogic();
        //    dl.LoadCandles(numberofCandles, candleType);
        //}

        //public void OnEvent(IEnumerable<ICacheEntryEvent<TickKey, Tick>> events)
        //{
        //    //foreach (var e in events)
        //    //{
        //    //    OnNext(new Tick[] { e.Value });
        //    //}
        //}
        //public bool Invoke(Guid nodeId, Tick tick)
        //{
        //    OnNext(new Tick[] { tick });
        //    return true;
        //}

        private void CandleManger_MoneyCandleFinished(object sender, Candle e)
        {
            var fema = shortEMA[(int)CandleType.Money].Process(e.ClosePrice, isFinal: true);
            var sema = longEMA[(int)CandleType.Money].Process(e.ClosePrice, isFinal: true);

            //var sema = mema2.Process(e.ClosePrice, isFinal: true);

            if (fema.IsFormed)// && sema.IsFormed)
            {
                TradeEMACross(fema.GetValue<Decimal>(), sema.GetValue<Decimal>(), e, CandleType.Money);
            }

            #region Write to File - Temp
            //System.IO.File.AppendAllText(@"D:\Prashant\Intelligent Software\2019\MarketData\Log\MoneyCandles.txt", JsonSerializer.Serialize(e));
            dataLogic.SaveCandle(e);
            #endregion
        }

        private void CandleManger_VolumeCandleFinished(object sender, Candle e)
        {
            var fema = shortEMA[(int)CandleType.Volume].Process(e.ClosePrice, isFinal: true);
            var sema = longEMA[(int)CandleType.Volume].Process(e.ClosePrice, isFinal: true);

            if (fema.IsFormed)// && sema.IsFormed)
            {
                TradeEMACross(fema.GetValue<Decimal>(), sema.GetValue<Decimal>(), e, CandleType.Volume);
            }

            #region Write to File - Temp
            //System.IO.File.AppendAllText(@"D:\Prashant\Intelligent Software\2019\MarketData\Log\VolumeCandles.txt", JsonSerializer.Serialize(e));
            dataLogic.SaveCandle(e);
            #endregion
        }

        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            var fema = shortEMA[(int)CandleType.Time].Process(e.ClosePrice, isFinal: true);
            var sema = longEMA[(int)CandleType.Time].Process(e.ClosePrice, isFinal: true);

            if (fema.IsFormed)// && sema.IsFormed)
            {
                TradeEMACross(fema.GetValue<Decimal>(), sema.GetValue<Decimal>(), e, CandleType.Time);
            }

            #region Write to File - Temp
            //System.IO.File.AppendAllText(@"D:\Prashant\Intelligent Software\2019\MarketData\Log\TimeCandles.txt", JsonSerializer.Serialize(e));
            //System.IO.File.AppendAllText(@"D:\Prashant\Intelligent Software\2019\MarketData\Log\EMA1.txt", JsonSerializer.Serialize(ema1.GetValue()));
            //System.IO.File.AppendAllText(@"D:\Prashant\Intelligent Software\2019\MarketData\Log\EMA2.txt", JsonSerializer.Serialize(ema2.GetCurrentValue()));
            dataLogic.SaveCandle(e);
            #endregion
        }

        public virtual async Task<bool> OnNext(Tick[] ticks)
        {
            //_instrumentToken = 256265;
            //var fema; var sema;

            //List<Tick> validTicks = new List<Tick>();
            //for (int i = 0; i < ticks.Count(); i++)
            //{
            //    Tick tick = ticks[i];
            //    if (ticks[i].InstrumentToken == _instrumentToken)
            //    {
            //        validTicks.Add(tick);
            //    }
            //}
            //if (validTicks.Count == 0)
            //{
            //    return false;
            //}

            if (ticks[0].InstrumentToken == _instrumentToken)
            {

                TypeCandles[(int)CandleType.Time].AddRange(candleManger.StreamingTimeFrameCandle(ticks[0], _instrumentToken, timeFrame, true)); // TODO: USING LOCAL VERSION RIGHT NOW
                //TypeCandles[(int)CandleType.Volume] = candleManger.StreamingVolumeCandle(validTicks.ToArray(), _instrumentToken, 10000); // TODO: USING LOCAL VERSION RIGHT NOW
                //TypeCandles[(int)CandleType.Money] = candleManger.StreamingMoneyCandle(validTicks.ToArray(), _instrumentToken, 32000000); // TODO: USING LOCAL VERSION RIGHT NOW

                var ftema = shortEMA[(int)CandleType.Time].Process(TypeCandles[(int)CandleType.Time].Last().ClosePrice, isFinal: false);
                var stema = longEMA[(int)CandleType.Time].Process(TypeCandles[(int)CandleType.Time].Last().ClosePrice, isFinal: false);

                //var vfema = vema1.Process(TypeCandles[(int)CandleType.Volume].Last().ClosePrice, isFinal: false);
                //var vsema = vema2.Process(TypeCandles[(int)CandleType.Volume].Last().ClosePrice, isFinal: false);

                //var mfema = mema1.Process(TypeCandles[(int)CandleType.Money].Last().ClosePrice, isFinal: false);
                //var msema = mema2.Process(TypeCandles[(int)CandleType.Money].Last().ClosePrice, isFinal: false);


                TradeExisting(ticks[0]);
            }

            //if (ftema.IsFormed)// && stema.IsFormed)
            //{
            //    temaFormed = true;
            //}
            //if (vfema.IsFormed)// && vsema.IsFormed)
            //{
            //    vemaFormed = true;
            //}
            //if (mfema.IsFormed)// && msema.IsFormed)
            //{
            //    memaFormed = true;
            //}

            return true;
        }


        private void TradeExisting(Tick tick)
        {
            ShortTrade trade;

            for (int i = 0; i < ActivePositions.Keys.Count; i++) // This is temporary till we finalize a candle for this analysis.
            {
                if(!ActivePositions.ContainsKey(i))
                {
                    continue;
                }
                var activePositionList = ActivePositions[i];

            //foreach(var algoPosition in ActivePositions)
            //{
                //var activePositionList = algoPosition.Value;

                for (int j = 0; j < activePositionList.Count; j++)
                {
                    AlgoPosition currentPosition = activePositionList[j];

                    if(currentPosition.Quantity == 0)
                    {
                        activePositionList.Remove(currentPosition);
                    }

                    //foreach (Tick tick in ticks)
                    //{
                        if (currentPosition.BuySLPrice >= tick.LastPrice && currentPosition.Quantity > 0)
                        {
                            //close this position
                            //currentPosition.BuyQuantity = 0;
                            //currentPosition.Quantity = 0;

                            trade = PlaceOrder(_instrumentToken, _tradingSymbol, currentPosition.BuyQuantity, buyOrder: false, tick.LastTradeTime.Value, tick.LastPrice, i); //algoPosition.Key);

                            currentPosition.SellPrice = trade.AveragePrice;
                            currentPosition.SellQuantity = trade.Quantity;
                            currentPosition.Quantity -= trade.Quantity;

                            break;
                        }
                        if (currentPosition.SellSLPrice <= tick.LastPrice && currentPosition.Quantity < 0)
                        {
                            //close this position
                            //currentPosition.SellQuantity = 0;
                            //currentPosition.Quantity = 0;
                            trade = PlaceOrder(_instrumentToken, _tradingSymbol, currentPosition.SellQuantity, buyOrder: true, tick.LastTradeTime.Value, tick.LastPrice,i);// algoPosition.Key);

                            currentPosition.BuyPrice = trade.AveragePrice;
                            currentPosition.BuyQuantity = trade.Quantity;
                            currentPosition.Quantity += trade.Quantity;

                            break;
                       // }
                    }
                    activePositionList[j] = currentPosition;
                }
            }
        }
        /// <summary>
        /// Trade at EMA cross. Variable qty & SL to be programmed
        /// </summary>
        /// <param name="fema">Trade when price crosses this EMA</param>
        /// <param name="sema">Overall Trend</param>
        /// <param name="candles"></param>
        /// <param name="candleType"></param>
        private void TradeEMACross(decimal fema, decimal sema, Candle lastFinishedCandle, CandleType candleType)
        {
            var finishedCandles = TypeCandles[(int)candleType].Where(x => x.State == CandleStates.Finished);
            var lasttwoFinishedCandles = finishedCandles.Skip(Math.Max(0, finishedCandles.Count() - 2));

            // Candle lastFinishedCandle = lasttwoFinishedCandles.Last();
            Candle secondlastFinishedCandle = lasttwoFinishedCandles.First();
            AlgoPosition currentPostion;
            ShortTrade trade;
            int qty = 0;

            //This is a temporary arrangement till we decide which candle gives better results with EMA. 
            //Active position can then be made a List instead of dictionary.

            if (!ActivePositions.ContainsKey((int)candleType))
            {
                ActivePositions[(int)candleType] = new List<AlgoPosition>();
            }

            //Quantity logic: increase qty with every new candle at step up rate till max qty. Close all together.
            //Last candle crossed the ema from top AND current candle is below ema, then short future or buy put
            if ((secondlastFinishedCandle.OpenPrice > fema && secondlastFinishedCandle.ClosePrice < fema)
                && (lastFinishedCandle.OpenPrice < fema && lastFinishedCandle.ClosePrice < fema))
            {

                //Close existing all buy and then sell step qty
                qty = ActivePositions[(int)candleType].Sum(x => x.Quantity);
                //ActivePositions[(int)candleType].Clear();

                if (qty * -1 < _maxQty)
                {

                    if (qty > 0)
                    {
                        //There are bought positions active. Close them first
                        currentPostion = ActivePositions[(int)candleType].Where(x => x.Quantity > 0).First();
                    }
                    else
                    {
                        currentPostion = new AlgoPosition();
                        ActivePositions[(int)candleType].Add(currentPostion);
                    }


                    trade = PlaceOrder(_instrumentToken, _tradingSymbol, Math.Abs(qty) + _stepQty, buyOrder: false, lastFinishedCandle.CloseTime, lastFinishedCandle.ClosePrice, (int)lastFinishedCandle.CandleType);
                    currentPostion.InstrumentToken = trade.InstrumentToken;
                    //currentPostion.BuyQuantity = 0;
                    currentPostion.SellPrice = trade.AveragePrice;
                    currentPostion.SellQuantity = _stepQty;
                    currentPostion.Quantity -= trade.Quantity;
                    currentPostion.TradingSymbol = _tradingSymbol;
                    currentPostion.SellSLPrice = secondlastFinishedCandle.HighPrice + 10; //TEMP FOR TESTING

                    if (currentPostion.Quantity == 0)
                        ActivePositions[(int)candleType].Remove(currentPostion);
                }
            }

            //Last candle crossed the ema from bottom AND current candle is above ema, then long future or buy call
            if ((secondlastFinishedCandle.OpenPrice < fema && secondlastFinishedCandle.ClosePrice > fema)
                && (lastFinishedCandle.OpenPrice > fema && lastFinishedCandle.ClosePrice > fema))
            {
                //Close existing all sell and then buy step qty
                qty = ActivePositions[(int)candleType].Sum(x => x.Quantity);
                //ActivePositions[(int)candleType].Clear();

                if (qty < _maxQty)
                {
                    if (qty < 0)
                    {
                        //There are sold positions active. Close them first
                        currentPostion = ActivePositions[(int)candleType].Where(x => x.Quantity < 0).First();
                    }
                    else
                    {
                        currentPostion = new AlgoPosition();
                        ActivePositions[(int)candleType].Add(currentPostion);
                    }



                    trade = PlaceOrder(_instrumentToken, _tradingSymbol, Math.Abs(qty) + _stepQty, buyOrder: true, lastFinishedCandle.CloseTime, lastFinishedCandle.ClosePrice, (int)lastFinishedCandle.CandleType);
                    currentPostion.InstrumentToken = trade.InstrumentToken;
                    //currentPostion.SellQuantity = 0;
                    currentPostion.BuyPrice = trade.AveragePrice;
                    currentPostion.BuySLPrice = secondlastFinishedCandle.LowPrice - 10; //TEMP
                    currentPostion.BuyQuantity = _stepQty;
                    currentPostion.Quantity += trade.Quantity;
                    currentPostion.TradingSymbol = _tradingSymbol;

                    //ActivePositions[(int)candleType].Add(currentPostion);

                    if (currentPostion.Quantity == 0)
                        ActivePositions[(int)candleType].Remove(currentPostion);
                }
            }

        }

        
        //private decimal PlaceOrder(string Symbol, bool buyOrder)
        //{
        //    //Dictionary<string, dynamic> orderStatus;

        //    //orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, Symbol,
        //    //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_BUY, 75, Product: Constants.PRODUCT_MIS,
        //    //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

        //    //string orderId = orderStatus["data"]["order_id"];
        //    //List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
        //    //return orderInfo[orderInfo.Count - 1].AveragePrice;
        //}

        private ShortTrade PlaceOrder(uint token, string tradingSymbol, int quantity, bool buyOrder, DateTime tickTime, decimal lastPrice, int groupID, int trigggerID = 0)
        {
            decimal currentPrice = lastPrice;
            //Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
            //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, quantity, Product: Constants.PRODUCT_MIS,
            //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            ///TEMP, REMOVE Later
            if (currentPrice == 0)
            {
                DataLogic dl = new DataLogic();
                currentPrice = dl.RetrieveLastPrice(token, tickTime, buyOrder);
            }

            string orderId = "0";
            decimal averagePrice = 0;
            //if (orderStatus["data"]["order_id"] != null)
            //{
            //    orderId = orderStatus["data"]["order_id"];
            //}
            if (orderId != "0")
            {
                //System.Threading.Thread.Sleep(200);
                //List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
                //averagePrice = orderInfo[orderInfo.Count - 1].AveragePrice;
            }
            //TODO: if average price doesnt come back, then raise exception in life case
            if (averagePrice == 0)
                averagePrice = buyOrder ? currentPrice : currentPrice;
            // averagePrice = buyOrder ? averagePrice * -1 : averagePrice;

            ShortTrade trade = new ShortTrade();
            trade.AveragePrice = averagePrice;
            trade.ExchangeTimestamp = tickTime;// DateTime.Now;
            trade.Quantity = quantity;
            trade.OrderId = orderId;
            trade.TransactionType = buyOrder ? "Buy" : "Sell";
            trade.TriggerID = trigggerID;
            trade.TradingStatus = buyOrder ? TradeStatus.Closed : TradeStatus.Open;
            UpdateTradeDetails(groupID, token, quantity, trade, trade.TriggerID);

            return trade;
        }

        private void UpdateTradeDetails(int strategyID, uint instrumentToken, int tradedLot, ShortTrade trade, int triggerID)
        {
            DataLogic dl = new DataLogic();
            dl.UpdateTrade(strategyID, instrumentToken, trade, AlgoIndex.EMACross, tradedLot, triggerID);
        }
    }
}
