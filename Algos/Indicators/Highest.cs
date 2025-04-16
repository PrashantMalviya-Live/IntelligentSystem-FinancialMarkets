using GlobalLayer;
using System;
using System.ComponentModel;
using System.Linq;

namespace Algorithms.Indicators
{
	/// <summary>
	/// Maximum value for a period.
	/// </summary>
	[DisplayName("Highest")]
	public class Highest : LengthIndicator<decimal>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Highest"/>.
		/// </summary>
		public Highest()
		{
			Length = 5;
		}

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			try
			{
				var newValue = input.IsSupport(typeof(Candle)) ? input.GetValue<Candle>().HighPrice : input.GetValue<decimal>();

				var lastValue = Buffer.Count == 0 ? newValue : this.GetValue<decimal>(0);

				if (input.IsFinal)
					Buffer.Add(newValue);

				if (newValue > lastValue)
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
						lastValue = Buffer.Aggregate(newValue, (current, t) => Math.Max(t, current));
					}
				}

				return new DecimalIndicatorValue(this, lastValue);
			}
			catch (Exception ex)
            {
				return null;
            }
		}
	}
}