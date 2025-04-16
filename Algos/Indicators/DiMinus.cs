using System;
using System.ComponentModel;
using GlobalLayer;
namespace Algorithms.Indicators
{
	/// <summary>
	/// DIMinus is a component of the Directional Movement System developed by Welles Wilder.
	/// </summary>
	[Browsable(false)]
	public class DiMinus : DiPart
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DiMinus"/>.
		/// </summary>
		public DiMinus()
		{
		}
		public DiMinus(int length):base(length)
		{
		}

		/// <inheritdoc />
		protected override decimal GetValue(Candle current, Candle prev)
		{
			if (current.LowPrice < prev.LowPrice && current.HighPrice - prev.HighPrice < prev.LowPrice - current.LowPrice)
				return prev.LowPrice - current.LowPrice;
			else
				return 0;
		}
	}
}