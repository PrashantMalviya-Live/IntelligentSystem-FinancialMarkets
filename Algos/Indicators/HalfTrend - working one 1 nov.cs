using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Text;

namespace Algorithms.Indicators
{
    public class HalfTrend : BaseComplexIndicator// LengthIndicator<IIndicatorValue>
    {
        //private AverageTrueRange _atr;
        private int _amplitude;
        private SimpleMovingAverage _maHigh;
        private SimpleMovingAverage _maLow;
        private decimal _multiplier;
        private int _length;
        private FixedSizedQueue<decimal> _high;
        private FixedSizedQueue<decimal> _low;
        private FixedSizedQueue<decimal> _preFinalHigh;
        private FixedSizedQueue<decimal> _preFinalLow;
        private decimal _highPrice;
        private decimal _lowPrice;
        private decimal _upperBand;
        private decimal _lowerBand;
        private decimal _previousUpperBand;
        private decimal _previousLowerBand;
        private decimal _value;
        private decimal _prevFinalValue;
        private decimal _previousValue;
        private decimal _up;
        private decimal _down;
        private decimal? _previousup = null;
        private decimal? _previousdown = null;
        IList<decimal> Buffer;
        private decimal _previousCandleClose;
        private bool _isFormed;
        private int _nextTrend = 0;
        private int _trend = 1;
        private int? _previousTrend = null;
        private decimal maxLowPrice = 0;// = nz(low[1], low);
        private decimal minHighPrice = 1000000;// = nz(high[1], high)
        public HalfTrend(decimal multiplier, int amplitude)
        {
            //_atr = new AverageTrueRange(100);
            _maHigh = new SimpleMovingAverage(amplitude);
            _maLow = new SimpleMovingAverage(amplitude);
            _multiplier = multiplier;
            _length = amplitude;
            _previousCandleClose = 0;
            _high = new FixedSizedQueue<decimal>();
            _high.Limit = _length;
            _low = new FixedSizedQueue<decimal>();
            _low.Limit = _length;
            Buffer = new List<decimal>();



            _preFinalHigh = new FixedSizedQueue<decimal>();
            _preFinalHigh.Limit = _length;
            _preFinalLow = new FixedSizedQueue<decimal>();
            _preFinalLow.Limit = _length;
        }
        public override bool IsFormed => _isFormed;
        public override void Reset()
        {
            base.Reset();
            _isFormed = false;
            //_atr.Reset();
            //this.Length = _length;
        }

        //public void Process (Candle e)
        protected override IIndicatorValue OnProcess(IIndicatorValue input)
        {
            //_atr.Process(input);

            var candle = ((SingleIndicatorValue<GlobalLayer.Candle>)input).Value;

            #region commented code
            //if (candle.State == CandleStates.Inprogress)
            //{
            //    //If candle is not file, then pull value from queue leaving first value behind, but do not change the queue
            //    _preFinalHigh.Enqueue(candle.HighPrice);
            //    _preFinalLow.Enqueue(candle.LowPrice);

            //    _highPrice = _preFinalHigh.Max;
            //    _lowPrice = _preFinalLow.Min;

            //    _maHigh.Process(candle.HighPrice, candle.Final.HasValue ? candle.Final.Value : false);
            //    _maLow.Process(candle.LowPrice, candle.Final.HasValue ? candle.Final.Value : false);

            //    //maxLowPrice = _low.SecondLastValue != 0 ? _low.SecondLastValue : candle.LowPrice;
            //    //minHighPrice = _high.SecondLastValue != 0 ? _high.SecondLastValue : candle.HighPrice;

            //    if (_nextTrend == 1)
            //    {
            //        maxLowPrice = Math.Max(_lowPrice, maxLowPrice);

            //        if (_maHigh.GetCurrentValue<decimal>() < maxLowPrice && candle.ClosePrice < (_preFinalLow.SecondLastValue == 0 ? _preFinalLow.LastValue : _preFinalLow.SecondLastValue))
            //        {
            //            _trend = 1;
            //            _nextTrend = 0;
            //            minHighPrice = _highPrice;
            //        }
            //    }
            //    else
            //    {
            //        minHighPrice = Math.Min(_highPrice, minHighPrice);
            //        if (_maLow.GetCurrentValue<decimal>() > minHighPrice && candle.ClosePrice > (_preFinalHigh.SecondLastValue == 0 ? _preFinalHigh.LastValue : _preFinalHigh.SecondLastValue))
            //        {
            //            _trend = 0;
            //            _nextTrend = 1;
            //            maxLowPrice = _lowPrice;
            //        }
            //    }

            //    if (_trend == 0)
            //    {
            //        if (_previousTrend != null && _previousTrend != 0)
            //        {
            //            _up = _previousdown == null ? _down : _previousdown.Value;
            //        }
            //        else
            //        {
            //            _up = _previousup == null ? maxLowPrice : Math.Max(maxLowPrice, _previousup.Value);
            //        }
            //    }
            //    else
            //    {
            //        if (_previousTrend != null && _previousTrend != 1)
            //        {
            //            _down = _previousup == null ? _up : _previousup.Value;
            //        }
            //        else
            //        {

            //            _down = _previousdown == null ? minHighPrice : Math.Min(minHighPrice, _previousdown.Value);
            //        }
            //    }

            //    _value = _trend == 0 ? _up : _down;

            //    //decimal _basicUpperBand = ((candle.HighPrice + candle.LowPrice) / 2) + _multiplier * _atr.GetCurrentValue<decimal>();
            //    //decimal _basicLowerBand = ((candle.HighPrice + candle.LowPrice) / 2) - _multiplier * _atr.GetCurrentValue<decimal>();

            //    //_upperBand = _previousCandleClose == 0 ? 0 : _basicUpperBand < _previousUpperBand || _previousCandleClose > _previousUpperBand ? _basicUpperBand : _previousUpperBand;
            //    //_lowerBand = _previousCandleClose == 0 ? 0 : _basicLowerBand > _previousLowerBand || _previousCandleClose < _previousLowerBand ? _basicLowerBand : _previousLowerBand;


            //    ////_value = newValue <= _upperBand ? _upperBand : _lowerBand;

            //    //_value = (_value == _previousUpperBand && candle.ClosePrice <= _upperBand) ? _upperBand : 
            //    //    (_value == _previousUpperBand && candle.ClosePrice >= _upperBand) ? _lowerBand : 
            //    //    (_value == _previousLowerBand && candle.ClosePrice >= _lowerBand) ? _lowerBand : 
            //    //    (_value == _previousLowerBand && candle.ClosePrice <= _lowerBand) ? _upperBand : 0;

            //    //_previousLowerBand = _lowerBand;
            //    //_previousUpperBand = _upperBand;
            //    //_previousCandleClose = candle.ClosePrice;

            //    //_previousup = _up;
            //    //_previousdown = _down;
            //    //_previousTrend = _trend;
            //    //_previousValue = _value;

            //    _preFinalHigh = _high;
            //    _preFinalLow = _low;

            //    //if (input.IsFinal)
            //    //    return new DecimalIndicatorValue(this, Buffer.Sum() / Length);

            //    //return new DecimalIndicatorValue(this, (Buffer.Skip(1).Sum() + newValue) / Length);

            //    return new DecimalIndicatorValue(this, _value);
            //}
            //else
            //{
            #endregion
            //If candle is not file, then pull value from queue leaving first value behind, but do not change the queue

            
            _high.Enqueue(candle.HighPrice);
            _low.Enqueue(candle.LowPrice);

                _highPrice = _high.Max;
                _lowPrice = _low.Min;


                _maHigh.Process(candle.HighPrice, candle.Final.HasValue ? candle.Final.Value : true);
                _maLow.Process(candle.LowPrice, candle.Final.HasValue ? candle.Final.Value : true);

                //maxLowPrice = _low.SecondLastValue != 0 ? _low.SecondLastValue : candle.LowPrice;
                //minHighPrice = _high.SecondLastValue != 0 ? _high.SecondLastValue : candle.HighPrice;

                if (_nextTrend == 1)
                {
                    maxLowPrice = Math.Max(_lowPrice, maxLowPrice);

                    if (_maHigh.GetCurrentValue<decimal>() < maxLowPrice && candle.ClosePrice < (_low.SecondLastValue == 0 ? _low.LastValue : _low.SecondLastValue))
                    {
                        _trend = 1;
                        _nextTrend = 0;
                        minHighPrice = _highPrice;
                    }
                }
                else
                {
                    minHighPrice = Math.Min(_highPrice, minHighPrice);
                    if (_maLow.GetCurrentValue<decimal>() > minHighPrice && candle.ClosePrice > (_high.SecondLastValue == 0 ? _high.LastValue : _high.SecondLastValue))
                    {
                        _trend = 0;
                        _nextTrend = 1;
                        maxLowPrice = _lowPrice;
                    }
                }

                if (_trend == 0)
                {
                    if (_previousTrend != null && _previousTrend != 0)
                    {
                        _up = _previousdown == null ? _down : _previousdown.Value;
                    }
                    else
                    {
                        _up = _previousup == null ? maxLowPrice : Math.Max(maxLowPrice, _previousup.Value);
                    }
                }
                else
                {
                    if (_previousTrend != null && _previousTrend != 1)
                    {
                        _down = _previousup == null ? _up : _previousup.Value;
                    }
                    else
                    {

                        _down = _previousdown == null ? minHighPrice : Math.Min(minHighPrice, _previousdown.Value);
                    }
                }

                _value = _trend == 0 ? _up : _down;

                //decimal _basicUpperBand = ((candle.HighPrice + candle.LowPrice) / 2) + _multiplier * _atr.GetCurrentValue<decimal>();
                //decimal _basicLowerBand = ((candle.HighPrice + candle.LowPrice) / 2) - _multiplier * _atr.GetCurrentValue<decimal>();

                //_upperBand = _previousCandleClose == 0 ? 0 : _basicUpperBand < _previousUpperBand || _previousCandleClose > _previousUpperBand ? _basicUpperBand : _previousUpperBand;
                //_lowerBand = _previousCandleClose == 0 ? 0 : _basicLowerBand > _previousLowerBand || _previousCandleClose < _previousLowerBand ? _basicLowerBand : _previousLowerBand;


                ////_value = newValue <= _upperBand ? _upperBand : _lowerBand;

                //_value = (_value == _previousUpperBand && candle.ClosePrice <= _upperBand) ? _upperBand : 
                //    (_value == _previousUpperBand && candle.ClosePrice >= _upperBand) ? _lowerBand : 
                //    (_value == _previousLowerBand && candle.ClosePrice >= _lowerBand) ? _lowerBand : 
                //    (_value == _previousLowerBand && candle.ClosePrice <= _lowerBand) ? _upperBand : 0;

                //_previousLowerBand = _lowerBand;
                //_previousUpperBand = _upperBand;
                //_previousCandleClose = candle.ClosePrice;

                _previousup = _up;
                _previousdown = _down;
                _previousTrend = _trend;
                _previousValue = _value;



                //if (input.IsFinal)
                //    return new DecimalIndicatorValue(this, Buffer.Sum() / Length);

                //return new DecimalIndicatorValue(this, (Buffer.Skip(1).Sum() + newValue) / Length);

                return new DecimalIndicatorValue(this, _value);
           // }


            
        }


        public decimal GetValue()
        {
            return _value;
        }
        /// <summary>
        /// 0 = UP and 1 Down
        /// </summary>
        /// <returns></returns>
        public decimal GetTrend()
        {
            return _trend;
        }
        //public bool isFormed
        //{
        //    get
        //    {
        //        return _atr.IsFormed;
        //    }
        //}
    }
}
