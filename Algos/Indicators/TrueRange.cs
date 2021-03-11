using System;
using System.Linq;
using System.ComponentModel;
using GlobalLayer;
namespace Algorithms.Indicators
{
	/// <summary>
	/// True range.
	/// </summary>
	[DisplayName("TR")]
	public class TrueRange : BaseIndicator
	{
		private Candle _prevCandle;

		/// <summary>
		/// Initializes a new instance of the <see cref="TrueRange"/>.
		/// </summary>
		public TrueRange()
		{
		}

		/// <inheritdoc />
		public override void Reset()
		{
			base.Reset();
			_prevCandle = null;
		}

		/// <summary>
		/// To get price components to select the maximal value.
		/// </summary>
		/// <param name="currentCandle">The current candle.</param>
		/// <param name="prevCandle">The previous candle.</param>
		/// <returns>Price components.</returns>
		protected virtual decimal[] GetPriceMovements(Candle currentCandle, Candle prevCandle)
		{
			return new[]
			{
				Math.Abs(currentCandle.HighPrice - currentCandle.LowPrice),
				Math.Abs(prevCandle.ClosePrice - currentCandle.HighPrice),
				Math.Abs(prevCandle.ClosePrice - currentCandle.LowPrice)
			};
		}

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			//var candle = input as Candle;//.GetValue<Candle>();
			var candle = ((SingleIndicatorValue<GlobalLayer.Candle>)input).Value;
			if (_prevCandle != null)
			{
				if (input.IsFinal)
					IsFormed = true;

				var priceMovements = GetPriceMovements(candle, _prevCandle);

				if (input.IsFinal)
					_prevCandle = candle;

				return new DecimalIndicatorValue(this, priceMovements.Max());
			}

			if (input.IsFinal)
				_prevCandle = candle;

			return new DecimalIndicatorValue(this, candle.HighPrice - candle.LowPrice);
		}
	}
}