using Algorithms.Indicators;
using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Algos.Indicators
{
    /// <summary>
    /// Time-frame candle.
    /// </summary>
    [IndicatorIn(typeof(CandleIndicatorValue))]
    [Serializable]
    public class TimeFrameCandleIndicator : BaseIndicator
    {
        public TimeFrameCandle TimeFrameCandle { get; set; }
        
        public CandlePrices CandlePriceType {get; set; }

        public TimeFrameCandleIndicator(CandlePrices candlePriceType)
        {
            CandlePriceType = candlePriceType;
        }

        public override bool IsFormed => TimeFrameCandle == null?false: TimeFrameCandle.Final.Value;

        /// <summary>
        /// Time-frame.
        /// </summary>
        [DataMember]
        public int TimeSpan { get; set; }



        protected override IIndicatorValue OnProcess(IIndicatorValue input)
        {
            TimeFrameCandle = input.GetValue<Candle>() as TimeFrameCandle;
            IsFormed = TimeFrameCandle.Final.Value;
            TimeFrameCandle.CandlePrices = CandlePriceType;
            
            return new CandleIndicatorValue(this, TimeFrameCandle, getPart: CandleIndicatorValueType());
        }
        public decimal GetCurrentValue<T>()
        {
            decimal currentValue;
            switch (CandlePriceType)
            {
                case CandlePrices.Open:
                    currentValue = TimeFrameCandle.OpenPrice;
                    break;
                case CandlePrices.High:
                    currentValue = TimeFrameCandle.HighPrice;
                    break;
                case CandlePrices.Low:
                    currentValue = TimeFrameCandle.LowPrice;
                    break;
                case CandlePrices.Close:
                    currentValue = TimeFrameCandle.ClosePrice;
                    break;
                default:
                    currentValue = TimeFrameCandle.ClosePrice;
                    break;
            }

            return currentValue;
        }
        public Func<Candle, decimal> CandleIndicatorValueType ()
        {
            Func<Candle, decimal> candleGetPart;
            switch (CandlePriceType)
            {
                case CandlePrices.Open:
                    candleGetPart = CandleIndicatorValue.ByOpen;
                    break;
                case CandlePrices.High:
                    candleGetPart = CandleIndicatorValue.ByMiddle;
                    break;
                case CandlePrices.Low:
                    candleGetPart = CandleIndicatorValue.ByMiddle;
                    break;
                case CandlePrices.Close:
                    candleGetPart = CandleIndicatorValue.ByClose;
                    break;
                default:
                    candleGetPart = CandleIndicatorValue.ByClose;
                    break;
            }

            return candleGetPart;
        }
        public decimal GetValue<T>()
        {
            decimal currentValue;
            switch (CandlePriceType)
            {
                case CandlePrices.Open:
                    currentValue = TimeFrameCandle.OpenPrice;
                    break;
                case CandlePrices.High:
                    currentValue = TimeFrameCandle.HighPrice;
                    break;
                case CandlePrices.Low:
                    currentValue = TimeFrameCandle.LowPrice;
                    break;
                case CandlePrices.Close:
                    currentValue = TimeFrameCandle.ClosePrice;
                    break;
                default:
                    currentValue = TimeFrameCandle.ClosePrice;
                    break;
            }

            return currentValue;
        }


    }
}
