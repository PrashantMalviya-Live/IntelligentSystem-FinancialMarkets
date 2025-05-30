
namespace StockSharp.Algo.Indicators
{
	using System;
	using System.ComponentModel;

	using StockSharp.Localization;

	/// <summary>
	/// Correlation.
	/// </summary>
	/// <remarks>
	/// https://en.wikipedia.org/wiki/Correlation_and_dependence.
	/// </remarks>
	[DisplayName("COR")]
	[DescriptionLoc(LocalizedStrings.CorrelationKey, true)]
	[IndicatorIn(typeof(PairIndicatorValue<decimal>))]
	public class Correlation : Covariance
	{
		private readonly StandardDeviation _source;
		private readonly StandardDeviation _other;

		/// <summary>
		/// Initializes a new instance of the <see cref="Correlation"/>.
		/// </summary>
		public Correlation()
		{
			_source = new StandardDeviation();
			_other = new StandardDeviation();

			Length = 20;
		}

		/// <inheritdoc />
		public override void Reset()
		{
			base.Reset();

			if (_source != null)
				_source.Length = Length;

			if (_other != null)
				_other.Length = Length;
		}

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			var cov = base.OnProcess(input);

			var value = input.GetValue<Tuple<decimal, decimal>>();

			var sourceDev = _source.Process(value.Item1);
			var otherDev = _other.Process(value.Item2);

			var v = sourceDev.GetValue<decimal>() * otherDev.GetValue<decimal>();

			if (v != 0)
				v = cov.GetValue<decimal>() / v;

			return new DecimalIndicatorValue(this, v);
		}
	}
}