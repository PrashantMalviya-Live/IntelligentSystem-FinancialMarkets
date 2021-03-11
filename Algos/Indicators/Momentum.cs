using System;
using System.ComponentModel;
namespace Algorithms.Indicators
{ 

	/// <summary>
	/// Momentum.
	/// </summary>
	/// <remarks>
	/// Momentum Simple = C - C-n Where C- closing price of previous period. Where C-n - closing price N periods ago.
	/// </remarks>
[DisplayName("Momentum")]
	public class Momentum : LengthIndicator<decimal>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Momentum"/>.
		/// </summary>
		public Momentum()
		{
			Length = 5;
		}

		/// <inheritdoc />
		public override bool IsFormed => Buffer.Count > Length;

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			var newValue = input.GetValue<decimal>();

			if (input.IsFinal)
			{
				Buffer.Add(newValue);
				
				if ((Buffer.Count - 1) > Length)
					Buffer.RemoveAt(0);
			}

			if (Buffer.Count == 0)
				return new DecimalIndicatorValue(this);

			return new DecimalIndicatorValue(this, newValue - Buffer[0]);
		}
	}
}