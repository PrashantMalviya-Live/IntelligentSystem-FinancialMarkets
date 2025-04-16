using GlobalLayer;
using System.ComponentModel;
using System.Linq;

namespace Algorithms.Indicators
{

	/// <summary>
	/// Stochastic %K.
	/// </summary>
	[IndicatorIn(typeof(CandleIndicatorValue))]
	public class StochasticK : LengthIndicator<decimal>
	{
		private readonly Lowest _low = new Lowest();

		private readonly Highest _high = new Highest();

		/// <summary>
		/// Initializes a new instance of the <see cref="StochasticK"/>.
		/// </summary>
		public StochasticK()
		{
			Length = 9;
		}

		/// <inheritdoc />
		public override bool IsFormed => _high.IsFormed;

		/// <inheritdoc />
		public override void Reset()
		{
			_high.Length = _low.Length = Length;
			base.Reset();
		}

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			var candle = input.GetValue<Candle>();
            //var highValue = _high.Process(input.SetValue(this, candle.High)).GetValue<decimal>();
            //var lowValue = _low.Process(input.SetValue(this, candle.Low)).GetValue<decimal>();

            //var diff = highValue - lowValue;

            //if (diff == 0)
            //	return new DecimalIndicatorValue(this, 0);

            //return new DecimalIndicatorValue(this, 100 * (candle.Close - lowValue) / diff);

			
            var highValue = _high.Process(input.SetValue(this, candle.HighPrice)).GetValue<decimal>();
            var lowValue = _low.Process(input.SetValue(this, candle.LowPrice)).GetValue<decimal>();

            var diff = highValue - lowValue;

            if (diff == 0)
                return new DecimalIndicatorValue(this, 0);

            return new DecimalIndicatorValue(this, 100 * (candle.ClosePrice - lowValue) / diff);
        }
	}
}