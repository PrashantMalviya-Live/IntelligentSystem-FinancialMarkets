using System;
using System.ComponentModel;
using GlobalLayer;
namespace Algorithms.Indicators
{
	using System.ComponentModel;

	/// <summary>
	/// DIPlus is a component of the Directional Movement System developed by Welles Wilder.
	/// </summary>
	[Browsable(false)]
	public class DiPlus : DiPart
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DiPlus"/>.
		/// </summary>
		public DiPlus()
		{
		}

		/// <inheritdoc />
		protected override decimal GetValue(Candle current, Candle prev)
		{
			if (current.HighPrice > prev.HighPrice && current.HighPrice - prev.HighPrice > prev.LowPrice - current.LowPrice)
				return current.HighPrice - prev.HighPrice;
			else
				return 0;
		}
	}
}