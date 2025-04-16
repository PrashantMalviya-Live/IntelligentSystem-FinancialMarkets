using System;
using System.ComponentModel;

namespace Algorithms.Indicators
{
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Linq;

	/// <summary>
	/// Welles Wilder Moving Average.
	/// </summary>
	[DisplayName("WilderMA")]
	public class WilderMovingAverage : LengthIndicator<decimal>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="WilderMovingAverage"/>.
		/// </summary>
		public WilderMovingAverage()
		{
			Length = 20;//32;
		}
		public WilderMovingAverage(int length)
		{
			Length = length;
		}

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			var newValue = input.GetValue<decimal>();

			if (input.IsFinal)
			{
				Buffer.Add(newValue);

				if (Buffer.Count > Length)
					Buffer.RemoveAt(0);
			}

			var buff = Buffer;
			if (!input.IsFinal)
			{
				buff = new List<decimal>();
				foreach(decimal b in Buffer.Skip(1))
                {
					buff.Add(b);
                }
				//buff.AddRange(Buffer.Skip(1));
				buff.Add(newValue);
			}

			if(Buffer.Count < Length)
            {
				return new DecimalIndicatorValue(this, buff.Sum() / buff.Count);

			}

			return new DecimalIndicatorValue(this, (this.GetCurrentValue<decimal>() * (buff.Count - 1) + newValue) / buff.Count);
		}
	}
}