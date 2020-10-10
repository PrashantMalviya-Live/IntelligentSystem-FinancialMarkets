using System.ComponentModel;
using System.Linq;

namespace Algorithms.Indicators
{
	/// <summary>
	/// Smoothed Moving Average.
	/// </summary>
	[DisplayName("SMMA")]
	public class SmoothedMovingAverage : LengthIndicator<decimal>
	{
		private decimal _prevFinalValue;

		/// <summary>
		/// Initializes a new instance of the <see cref="SmoothedMovingAverage"/>.
		/// </summary>
		public SmoothedMovingAverage()
		{
			Length = 32;
		}

		/// <inheritdoc />
		public override void Reset()
		{
			_prevFinalValue = 0;
			base.Reset();
		}

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			var newValue = input.GetValue<decimal>();

			if (!IsFormed)
			{
				if (input.IsFinal)
				{
					Buffer.Add(newValue);

					_prevFinalValue = Buffer.Sum() / Length;

					return new DecimalIndicatorValue(this, _prevFinalValue);
				}

				return new DecimalIndicatorValue(this, (Buffer.Skip(1).Sum() + newValue) / Length);
			}

			var curValue = (_prevFinalValue * (Length - 1) + newValue) / Length;

			if (input.IsFinal)
				_prevFinalValue = curValue;

			return new DecimalIndicatorValue(this, curValue);
		}
	}
}
