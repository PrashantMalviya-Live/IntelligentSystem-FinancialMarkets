using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
namespace Algorithms.Indicators
{
	/// <summary>
	/// Weighted moving average.
	/// </summary>
	[DisplayName("WMA")]
	public class WeightedMovingAverage : LengthIndicator<decimal>
	{
		private decimal _denominator = 1;

		/// <summary>
		/// Initializes a new instance of the <see cref="WeightedMovingAverage"/>.
		/// </summary>
		public WeightedMovingAverage()
		{
			Length = 32;
		}

		/// <inheritdoc />
		public override void Reset()
		{
			base.Reset();

			_denominator = 0;

			for (var i = 1; i <= Length; i++)
				_denominator += i;
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
				foreach(var b in Buffer.Skip(1))
                {
					buff.Add(b);
                }
				buff.Add(newValue);
			}

			var w = 1;
			return new DecimalIndicatorValue(this, buff.Sum(v => w++ * v) / _denominator);
		}
	}
}