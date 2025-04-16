using GlobalLayer;
using System;
using System.ComponentModel;
using System.Linq;

namespace Algorithms.Indicators
{

	/// <summary>
	/// Minimum value for a period.
	/// </summary>
	[DisplayName("Lowest")]
	public class Lowest : LengthIndicator<decimal>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Lowest"/>.
		/// </summary>
		public Lowest()
		{
			Length = 5;
		}

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			var newValue = input.IsSupport(typeof(Candle)) ? input.GetValue<Candle>().LowPrice : input.GetValue<decimal>();

			var lastValue = Buffer.Count == 0 ? newValue : this.GetValue<decimal>(0);

			// добавляем новое начало
			if (input.IsFinal)
				Buffer.Add(newValue);

			if (newValue < lastValue)
			{
				lastValue = newValue;
			}

			if (Buffer.Count > Length) 
			{
				var first = Buffer[0];

				if (input.IsFinal)
					Buffer.RemoveAt(0);

				if (first == lastValue && lastValue != newValue) 
				{
					lastValue = Buffer.Aggregate(newValue, (current, t) => Math.Min(t, current));
				}
			}

			return new DecimalIndicatorValue(this, lastValue);
		}
	}
}