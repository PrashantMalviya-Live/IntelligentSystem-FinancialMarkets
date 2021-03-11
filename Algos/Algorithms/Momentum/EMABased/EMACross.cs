using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZMQFacade;
using GlobalLayer;
using Algorithms.Indicators;
using Algorithms.Utils;
using System.Runtime.Serialization.Json;
using System.Text.Json;
using Algorithms.Utilities;

namespace Algos.TLogics
{
    public class EMACross : IZMQ //, IObserver<Tick[]>
    {
        TimeSpan timeFrame;
        CandleManger candleManger;
        //List<TimeFrameCandle> timeCandles;
        //List<VolumeCandle> volumeCandles;
        //List<MoneyCandle> moneyCandles;
        Dictionary<int, List<Candle>> TypeCandles;
        ExponentialMovingAverage tema1, tema2, vema1, vema2, mema1, mema2;
        uint _instrumentToken = 0;
        string _tradingSymbol = "";
        AlgoPosition ActivePosition;
        List<AlgoPosition> ActivePositions;
        int _stepQty = 100;
        int _maxQty = 100;
        bool temaFormed = false, vemaFormed = false, memaFormed = false;
        DataLogic dataLogic = new DataLogic();
        public EMACross(uint instrumentToken, TimeSpan candleTimeSpan, int fEma, int sEMA)
        {
            //ActivePositions = new Dictionary<uint, Position>();
            ActivePosition = new AlgoPosition();
            ActivePositions = new List<AlgoPosition>();
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

            tema1 = new ExponentialMovingAverage(fEma);
            tema2 = new ExponentialMovingAverage(sEMA);
            
            vema1 = new ExponentialMovingAverage(fEma);
            vema2 = new ExponentialMovingAverage(sEMA);
            
            mema1 = new ExponentialMovingAverage(fEma);
            mema2 = new ExponentialMovingAverage(sEMA);
        }

        private void CandleManger_MoneyCandleFinished(object sender, Candle e)
        {
            var fema = mema1.Process(e.ClosePrice, isFinal: true);
            var sema = mema2.Process(e.ClosePrice, isFinal: true);

            if (fema.IsFormed && sema.IsFormed)
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
            var fema = vema1.Process(e.ClosePrice, isFinal: true);
            var sema = vema2.Process(e.ClosePrice, isFinal: true);

            if (fema.IsFormed && sema.IsFormed)
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
            var fema = tema1.Process(e.ClosePrice, isFinal: true);
            var sema = tema2.Process(e.ClosePrice, isFinal: true);

            if (fema.IsFormed && sema.IsFormed)
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


            //if (ticks[0].InstrumentToken == _instrumentToken)
            //{
            //    TypeCandles[(int)CandleType.Time] = candleManger.StreamingTimeFrameCandle(ticks[0], _instrumentToken, timeFrame, true); // TODO: USING LOCAL VERSION RIGHT NOW


            //    TypeCandles[(int)CandleType.Volume] = candleManger.StreamingVolumeCandle(ticks, _instrumentToken, 10000); // TODO: USING LOCAL VERSION RIGHT NOW
            //                                                                                                                             //System.IO.File.AppendAllText(@"D:\Prashant\Intelligent Software\2019\MarketData\Log\VolumeCandles.txt", JsonSerializer.Serialize(volumeCandles.LastOrDefault()));

            //    TypeCandles[(int)CandleType.Money] = candleManger.StreamingMoneyCandle(ticks, _instrumentToken, 32000000); // TODO: USING LOCAL VERSION RIGHT NOW
            //                                                                                                                              //System.IO.File.AppendAllText(@"D:\Prashant\Intelligent Software\2019\MarketData\Log\MoneyCandles.txt", JsonSerializer.Serialize(moneyCandles.LastOrDefault()));

            //    var ftema = tema1.Process(TypeCandles[(int)CandleType.Time].Last().ClosePrice, isFinal: false);
            //    var stema = tema2.Process(TypeCandles[(int)CandleType.Time].Last().ClosePrice, isFinal: false);

            //    var vfema = vema1.Process(TypeCandles[(int)CandleType.Volume].Last().ClosePrice, isFinal: false);
            //    var vsema = vema2.Process(TypeCandles[(int)CandleType.Volume].Last().ClosePrice, isFinal: false);

            //    var mfema = mema1.Process(TypeCandles[(int)CandleType.Money].Last().ClosePrice, isFinal: false);
            //    var msema = mema2.Process(TypeCandles[(int)CandleType.Money].Last().ClosePrice, isFinal: false);
            //    //foreach (Tick tick in validTicks)
            //    //{
            //    if (ftema.IsFormed && stema.IsFormed)
            //    {
            //        temaFormed = true;
            //    }
            //    if (vfema.IsFormed && vsema.IsFormed)
            //    {
            //        vemaFormed = true;
            //    }
            //    if (mfema.IsFormed && msema.IsFormed)
            //    {
            //        memaFormed = true;
            //    }
            //    //}
            //}
            return true;
        }

        private void TradeEMACross(decimal ema, List<Candle> candles, CandleType candleType)
        {
            var finishedCandles = candles.Where(x => x.State == CandleStates.Finished);
            var lasttwoFinishedCandles = finishedCandles.Skip(Math.Max(0, finishedCandles.Count() - 2));

            Candle lastFinishedCandle = lasttwoFinishedCandles.First();
            Candle secondlastFinishedCandle = lasttwoFinishedCandles.Last();
            AlgoPosition currentPostion;
            ShortTrade trade;
            int qty = 0;
            //Quantity logic: increase qty with every new candle at step up rate till max qty. Close all together.
            //Last candle crossed the ema from top AND current candle is below ema, then short future or buy put
            if ((secondlastFinishedCandle.OpenPrice > ema && secondlastFinishedCandle.ClosePrice < ema) 
                && (lastFinishedCandle.OpenPrice < ema && lastFinishedCandle.ClosePrice < ema))
            {

                //Close existing all buy and then sell step qty
                qty = ActivePositions.Sum(x => x.Quantity);
                ActivePositions.Clear();

                currentPostion = new AlgoPosition();
                trade = PlaceOrder(_instrumentToken, _tradingSymbol, qty + _stepQty, buyOrder: false, lastFinishedCandle.CloseTime, lastFinishedCandle.ClosePrice, (int)lastFinishedCandle.CandleType);
                currentPostion.InstrumentToken = trade.InstrumentToken;
                currentPostion.BuyQuantity = 0;
                currentPostion.SellPrice = trade.AveragePrice;
                currentPostion.SellQuantity = _stepQty;
                currentPostion.Quantity -= trade.Quantity;
                currentPostion.TradingSymbol = _tradingSymbol;

                ActivePositions.Add(currentPostion);
            }

            //Last candle crossed the ema from bottom AND current candle is above ema, then long future or buy call
            if ((secondlastFinishedCandle.OpenPrice < ema && secondlastFinishedCandle.ClosePrice > ema)
                && (lastFinishedCandle.OpenPrice > ema && lastFinishedCandle.ClosePrice > ema))
            {
                //Close existing all sell and then buy step qty
                qty = ActivePositions.Sum(x => x.Quantity);
                ActivePositions.Clear();

                currentPostion = new AlgoPosition();
                trade = PlaceOrder(_instrumentToken, _tradingSymbol, qty + _stepQty, buyOrder: true, lastFinishedCandle.CloseTime, lastFinishedCandle.ClosePrice, (int) lastFinishedCandle.CandleType);
                currentPostion.InstrumentToken = trade.InstrumentToken;
                currentPostion.SellQuantity = 0;
                currentPostion.BuyPrice = trade.AveragePrice;
                currentPostion.BuyQuantity = _stepQty;
                currentPostion.Quantity += trade.Quantity;
                currentPostion.TradingSymbol = _tradingSymbol;

                ActivePositions.Add(currentPostion);
            }

        }

        //all ticks related to 1 token only
        private void TradeEMACross(decimal fema, decimal sema, Candle candle, CandleType candleType)
        {
            //Candle should open and close at the same side


            //Step1 : Check the zone: buy or sell.
            //Step2: take trade based on faster EMA, cut the trade when tick crosses faster ema.
            //Step 3: Check for the ADX level to optimize the trade

            //if(candleType == CandleType.Time)
            //TimeFrameCandle currentCandle = candle as TimeFrameCandle;

            int groupId = (int)candleType;
            ShortTrade trade;
            //decimal currentPrice = ticks.Average(x => x.LastPrice);
            decimal currentPrice = candle.ClosePrice;

            TradeZone currentZone = TradeZone.Hold;
            if (currentPrice > sema)
            {
                currentZone = TradeZone.Long;
            }
            else
            {
                currentZone = TradeZone.Short;
            }

            //CurrentPostion currentPosition = ActivePosition.OutOfMarket;
            //foreach (var activePosition in ActivePositions)
            //{
            // currentPosition = activePosition.Value.ActiveState;
            //Entry strategies
            if (currentZone == TradeZone.Long && ActivePosition.Quantity < _maxQty)
            {
                //currentPosition = CurrentPostion.Bought;

                //Buy
                trade = PlaceOrder(_instrumentToken, _tradingSymbol, _maxQty, buyOrder: true, candle.CloseTime, currentPrice, groupId);
                ActivePosition.InstrumentToken = trade.InstrumentToken;
                ActivePosition.BuyPrice = trade.AveragePrice;
                ActivePosition.BuyQuantity += trade.Quantity;
                ActivePosition.Quantity += trade.Quantity;
                ActivePosition.TradingSymbol = _tradingSymbol;

                //ActivePositions.Add(_instrumentToken, position);

            }
            if (currentZone == TradeZone.Short && ActivePosition.Quantity > -1 * _maxQty)
            {
                //currentPosition = CurrentPostion.Sold;
                //Short

                //Place new order for PE or short future
                //Buy
                trade = PlaceOrder(_instrumentToken, _tradingSymbol, _maxQty, buyOrder: false, candle.CloseTime, currentPrice, groupId);
                ActivePosition.InstrumentToken = trade.InstrumentToken;
                ActivePosition.SellPrice = trade.AveragePrice;
                ActivePosition.SellQuantity += trade.Quantity;
                ActivePosition.Quantity -= trade.Quantity;
                ActivePosition.TradingSymbol = _tradingSymbol;

                // ActivePositions.Add(_instrumentToken, position);
            }

            //Exit Stretegies
            if (ActivePosition.Quantity > 0 && (candle.OpenPrice > currentPrice && candle.ClosePrice < currentPrice))
            {
                //place order to sell the bought position
                //currentPosition = CurrentPostion.OutOfMarket;
                //Close position
                trade = PlaceOrder(_instrumentToken, _tradingSymbol, _maxQty, buyOrder: false, candle.CloseTime, currentPrice, groupId);

                ActivePosition.SellPrice = trade.AveragePrice;
                ActivePosition.SellQuantity += trade.Quantity;
                ActivePosition.Quantity -= trade.Quantity;
                ActivePosition.TradingSymbol = _tradingSymbol;
                //ActivePositions.Remove(groupId);

            }
            if (ActivePosition.Quantity < 0 && (candle.OpenPrice < currentPrice && candle.ClosePrice > currentPrice))
            {
                //place order to sell the bought position
                //currentPosition = CurrentPostion.OutOfMarket;

                //Close position
                trade = PlaceOrder(_instrumentToken, _tradingSymbol, _maxQty, buyOrder: true, candle.CloseTime, currentPrice, groupId);

                ActivePosition.BuyPrice = trade.AveragePrice;
                ActivePosition.BuyQuantity += trade.Quantity;
                ActivePosition.Quantity += trade.Quantity;
                ActivePosition.TradingSymbol = _tradingSymbol;

                //ActivePositions.Remove(groupId);
            }
            //}

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
