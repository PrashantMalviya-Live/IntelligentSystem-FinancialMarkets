using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZMQFacade;
using GlobalLayer;
using Algorithms.Indicators;
using Algorithms.Utils;

namespace Algorithms.Algorithms
{
    public class MultipleTimeFrameMACD : IZMQ
    {
        TimeSpan stimeFrame, ltimeFrame;
        CandleManger candleManager;
       // List<Candle> stimeCandles, ltimeCandles;
        MovingAverageConvergenceDivergence smacd, lmacd;
        ExponentialMovingAverage ssignalema, lsignalema;
        uint _instrumentToken = 0;
        List<Position> ActivePositions;

        public MultipleTimeFrameMACD(uint instrumentToken, TimeSpan stimeSpan, TimeSpan ltimeSpan)
        {
            //ActivePositions = new List<Position>();

            //_instrumentToken = instrumentToken;
            //stimeFrame = stimeSpan;
            //ltimeFrame = ltimeSpan;

            //candleManager = new CandleManger();
            //candleManager.TimeCandleFinished += CandleManger_TimeCandleFinished;

            //stimeCandles = new List<TimeFrameCandle>();
            //ltimeCandles = new List<TimeFrameCandle>();

            //smacd = new MovingAverageConvergenceDivergence();
            //lmacd = new MovingAverageConvergenceDivergence();
        }

        private void CandleManger_TimeCandleFinished(object sender, TimeFrameCandle e)
        {
            if (e.TimeFrame == stimeFrame)
            {
                var smacdV = smacd.Process(e.ClosePrice, isFinal: true);
                decimal smacdvalue = smacdV.GetValue<decimal>();
                ssignalema.Process(smacdvalue, isFinal: true);

            }
            else
            {
                var lmacdV = lmacd.Process(e.ClosePrice, isFinal: true);
                decimal lmacdvalue = lmacdV.GetValue<decimal>();
                lsignalema.Process(lmacdvalue, isFinal: true);
            }
        }

        public virtual async Task<bool> OnNext(Tick[] ticks)
        {
            //_instrumentToken = 256265;

            //foreach (Tick tick in ticks)
            //{
            //    stimeCandles = candleManager.StreamingTimeFrameCandle(ticks, _instrumentToken, stimeFrame);
            //    var smacdV = smacd.Process(stimeCandles.Last().ClosePrice, isFinal: false);
            //    decimal smacdvalue = smacdV.GetValue<decimal>();
            //    var ssignalValue = ssignalema.Process(smacdvalue, isFinal: false);

            //    ltimeCandles = candleManager.StreamingTimeFrameCandle(ticks, _instrumentToken, ltimeFrame);
            //    var lmacdV = lmacd.Process(ltimeCandles.Last().ClosePrice, isFinal: false);
            //    decimal lmacdvalue = lmacdV.GetValue<decimal>();
            //    var lsignalValue = lsignalema.Process(lmacdvalue, isFinal: false);

            //    TradeMultipleTimeFrameMACD(smacdvalue, lmacdvalue, 
            //        ssignalValue.GetValue<decimal>(), lsignalValue.GetValue<decimal>(),  
            //        tick);
            //}
            return true;
        }

        //all ticks related to 1 token only
        private void TradeMultipleTimeFrameMACD(decimal smacd, 
            decimal lmacd, decimal ssignal, decimal lsignal, Tick tick)
        {
            ////Step1 : Check the zone: buy or sell.
            ////Step2: take trade based on faster EMA, cut the trade when tick crosses faster ema.
            ////Step 3: Check for the ADX level to optimize the trade
            //Position position = new Position();
            //TimeFrameCandle sCandle = stimeCandles.Last();
            //TimeFrameCandle lCandle = ltimeCandles.Last();

            //decimal currentPrice = tick.LastPrice;

            //TradeZone currentZone = TradeZone.Hold;
            //CurrentPostion currentPosition = CurrentPostion.OutOfMarket;


            //if (lmacd > lsignal && smacd > ssignal)
            //{
            //    currentZone = TradeZone.Long;
            //}
            //else if (lmacd > lsignal && smacd < ssignal)
            //{
            //    currentZone = TradeZone.Short;
            //}
            //else
            //{
            //    currentZone = TradeZone.DoNotTrade;
            //}

            ////Entry strategies
            //if (currentZone == TradeZone.Long && currentPosition == CurrentPostion.OutOfMarket)
            //{
            //    currentPosition = CurrentPostion.Bought;
            //    //Buy
            //}
            //if (currentZone == TradeZone.Short && currentPosition == CurrentPostion.Bought)
            //{
            //    currentPosition = CurrentPostion.OutOfMarket;
            //    //Close
            //}

        }
    }
}
