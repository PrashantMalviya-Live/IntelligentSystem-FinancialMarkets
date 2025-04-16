using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Text;

namespace Algorithms.Indicators
{
    public class SuperTrend : BaseComplexIndicator// LengthIndicator<IIndicatorValue>
    {
        private AverageTrueRange _atr;
        private decimal _multiplier;
        private int _length;
        private FixedSizedQueue<decimal> _high;
        private FixedSizedQueue<decimal> _low;
        private decimal _upperBand;
        private decimal _lowerBand;
        private decimal _previousUpperBand;
        private decimal _previousLowerBand;
        private decimal _value;
        private decimal _previousCandleClose;
        private bool _isFormed;
        public SuperTrend(decimal multiplier, int length)
        {
            _atr = new AverageTrueRange(length);
            _multiplier = multiplier;
            _length = length;
            _previousCandleClose = 0;
            
            //_high = new FixedSizedQueue<decimal>();
            //_high.Limit = _length;
            //_low = new FixedSizedQueue<decimal>();
            //_low.Limit = _length;
        }
        public override bool IsFormed => _isFormed;
        public override void Reset()
        {
            base.Reset();
            _isFormed = false;
            _atr.Reset();
            //this.Length = _length;
        }

        //public void Process (Candle e)
        protected override IIndicatorValue OnProcess(IIndicatorValue input)
        {
            _atr.Process(input);

            var candle = ((SingleIndicatorValue<GlobalLayer.Candle>)input).Value;
            
            //_high.Enqueue(candle.HighPrice);
            //_low.Enqueue(candle.LowPrice);
            decimal _basicUpperBand = ((candle.HighPrice + candle.LowPrice) / 2) + _multiplier * _atr.GetCurrentValue<decimal>();
            decimal _basicLowerBand = ((candle.HighPrice + candle.LowPrice) / 2) - _multiplier * _atr.GetCurrentValue<decimal>();

            //decimal _basicUpperBand = ((_high.Max + _high.Min) / 2) + _multiplier * _atr.GetCurrentValue<decimal>();
            //decimal _basicLowerBand = ((_high.Max + _high.Min) / 2) - _multiplier * _atr.GetCurrentValue<decimal>();

            _upperBand = _previousCandleClose == 0 ? 0 : _basicUpperBand < _previousUpperBand || _previousCandleClose > _previousUpperBand ? _basicUpperBand : _previousUpperBand;
            _lowerBand = _previousCandleClose == 0 ? 0 : _basicLowerBand > _previousLowerBand || _previousCandleClose < _previousLowerBand ? _basicLowerBand : _previousLowerBand;

            
            //_value = newValue <= _upperBand ? _upperBand : _lowerBand;

            _value = (_value == _previousUpperBand && candle.ClosePrice <= _upperBand) ? _upperBand : 
                (_value == _previousUpperBand && candle.ClosePrice >= _upperBand) ? _lowerBand : 
                (_value == _previousLowerBand && candle.ClosePrice >= _lowerBand) ? _lowerBand : 
                (_value == _previousLowerBand && candle.ClosePrice <= _lowerBand) ? _upperBand : 0;

            _previousLowerBand = _lowerBand;
            _previousUpperBand = _upperBand;
            _previousCandleClose = candle.ClosePrice;

            return new DecimalIndicatorValue(this, _value);
        }

        //protected override IIndicatorValue OnProcess(Candle e)
        //{
        //    _atr.Process(e);

        //    if (e.HighPrice > _high)
        //    {
        //        _high = e.HighPrice;
        //    }
        //    if (e.LowPrice < _low || _low == 0)
        //    {
        //        _low = e.LowPrice;
        //    }
        //    decimal _basicUpperBand = ((_high + _low) / 2) + _multiplier * _atr.GetCurrentValue<decimal>();
        //    decimal _basicLowerBand = ((_high + _low) / 2) - _multiplier * _atr.GetCurrentValue<decimal>();

        //    _upperBand = _previousCandleClose == 0 ? _basicUpperBand : _basicUpperBand < _upperBand && _previousCandleClose < _upperBand ? _basicUpperBand : _upperBand;
        //    _lowerBand = _previousCandleClose == 0 ? _basicLowerBand : _basicLowerBand > _lowerBand && _previousCandleClose > _lowerBand ? _basicLowerBand : _lowerBand;

        //    _previousCandleClose = e.ClosePrice;
        //    _value = e.ClosePrice <= _upperBand ? _upperBand : _lowerBand;

        //    return new DecimalIndicatorValue(this, _value);
        //}
        public decimal GetValue()
        {
            return _value;
        }
        public bool isFormed
        {
            get
            {
                return _atr.IsFormed;
            }
        }
    }
}
