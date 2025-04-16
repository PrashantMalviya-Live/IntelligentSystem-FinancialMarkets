namespace Algorithms.Indicators
{
    using GlobalLayer;
    using System;
    using System.ComponentModel;
    using System.Linq;

    /// <summary>
    /// Exponential Moving Average.
    /// </summary>
	[DisplayName("EMA")]
	public class ExponentialMovingAverage : LengthIndicator<decimal>
	{
		private decimal _prevFinalValue;
		private decimal _multiplier = 1;

		/// <summary>
		/// Initializes a new instance of the <see cref="ExponentialMovingAverage"/>.
		/// </summary>
		public ExponentialMovingAverage(int length = 32)
		{
			Length = length;
		}

		/// <inheritdoc />
		public override void Reset()
		{
			base.Reset();
			_multiplier = 2m / (Length + 1);
			_prevFinalValue = 0;
		}


		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
            var candle = input.GetValue<Candle>();

			var newValue = candle.ClosePrice;// input.GetValue<decimal>();

			if (!IsFormed)
			{
				if (input.IsFinal)
				{
					Buffer.Add(newValue);

					_prevFinalValue = Buffer.Sum() / Length;

					return new DecimalIndicatorValue(this, _prevFinalValue);
				}
				else
				{
					return new DecimalIndicatorValue(this, (Buffer.Skip(1).Sum() + newValue) / Length);
				}
			}
			else
			{
				// если sma сформирована 
				// если IsFinal = true рассчитываем ema и сохраняем для последующих расчетов с промежуточными значениями
				var curValue = (newValue - _prevFinalValue) * _multiplier + _prevFinalValue;

				if (input.IsFinal)
					_prevFinalValue = curValue;

				return new DecimalIndicatorValue(this, curValue);
			}
		}

    }
}