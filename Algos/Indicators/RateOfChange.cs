using System;
using System.ComponentModel;
namespace Algorithms.Indicators
{ 

	/// <summary>
	/// Rate of change.
	/// </summary>
[DisplayName("ROC")]
	public class RateOfChange : Momentum
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="RateOfChange"/>.
		/// </summary>
		public RateOfChange()
		{
		}

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			var result = base.OnProcess(input);

			if (Buffer.Count > 0 && Buffer[0] != 0)
				return new DecimalIndicatorValue(this, result.GetValue<decimal>() / Buffer[0] * 100);
			
			return new DecimalIndicatorValue(this);
		}
	}
}