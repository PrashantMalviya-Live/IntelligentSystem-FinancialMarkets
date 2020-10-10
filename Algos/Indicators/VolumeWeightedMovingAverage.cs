using System.ComponentModel;
using GlobalLayer;
namespace Algorithms.Indicators
{
	/// <summary>
	/// Volume weighted moving average.
	/// </summary>
	/// <remarks>
	/// http://www2.wealth-lab.com/WL5Wiki/VMA.ashx http://stockcharts.com/school/doku.php?id=chart_school:technical_indicators:vwap_intraday.
	/// </remarks>
	[DisplayName("VMA")]
	//[IndicatorIn(typeof(CandleIndicatorValue))]
	public class VolumeWeightedMovingAverage : LengthIndicator<decimal>
	{
		// Текущее значение числителя
		private readonly Sum _nominator = new Sum();

		// Текущее значение знаменателя
		private readonly Sum _denominator = new Sum();

		/// <summary>
		/// To create the indicator <see cref="VolumeWeightedMovingAverage"/>.
		/// </summary>
		public VolumeWeightedMovingAverage()
		{
			Length = 32;
		}

		/// <inheritdoc />
		public override void Reset()
		{
			base.Reset();
			_denominator.Length = _nominator.Length = Length;
		}

		/// <inheritdoc />
		public override bool IsFormed => _nominator.IsFormed && _denominator.IsFormed;

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			var candle = input.GetValue<Candle>();

			var shValue = _nominator.Process(input.SetValue(this, candle.ClosePrice * candle.TotalVolume)).GetValue<decimal>();
			var znValue = _denominator.Process(input.SetValue(this, candle.TotalVolume)).GetValue<decimal>();

			return znValue != 0 
				? new DecimalIndicatorValue(this, shValue / znValue) 
				: new DecimalIndicatorValue(this);
		}
	}
}