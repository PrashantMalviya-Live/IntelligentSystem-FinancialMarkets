using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace Algorithms.Indicators
{
	/// <summary>
	/// Convergence/divergence of moving averages.
	/// </summary>
	[DisplayName("MACD")]
	public class MovingAverageConvergenceDivergence : BaseIndicator
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MovingAverageConvergenceDivergence"/>.
		/// </summary>
		public MovingAverageConvergenceDivergence()
			: this(new ExponentialMovingAverage { Length = 26 }, new ExponentialMovingAverage { Length = 12 })
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MovingAverageConvergenceDivergence"/>.
		/// </summary>
		/// <param name="longMa">Long moving average.</param>
		/// <param name="shortMa">Short moving average.</param>
		public MovingAverageConvergenceDivergence(ExponentialMovingAverage longMa, ExponentialMovingAverage shortMa)
		{
			ShortMa = shortMa ?? throw new ArgumentNullException(nameof(shortMa));
			LongMa = longMa ?? throw new ArgumentNullException(nameof(longMa));
		}

		/// <summary>
		/// Long moving average.
		/// </summary>
		[TypeConverter(typeof(ExpandableObjectConverter))]
		public ExponentialMovingAverage LongMa { get; }

		/// <summary>
		/// Short moving average.
		/// </summary>
		[TypeConverter(typeof(ExpandableObjectConverter))]
		public ExponentialMovingAverage ShortMa { get; }

		/// <inheritdoc />
		public override bool IsFormed => LongMa.IsFormed;

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			var shortValue = ShortMa.Process(input);
			var longValue = LongMa.Process(input);
			return new DecimalIndicatorValue(this, shortValue.GetValue<decimal>() - longValue.GetValue<decimal>());
		}

		/// <inheritdoc />
		//public override void Load(SettingsStorage storage)
		//{
		//	base.Load(storage);

		//	LongMa.LoadNotNull(storage, nameof(LongMa));
		//	ShortMa.LoadNotNull(storage, nameof(ShortMa));
		//}

		///// <inheritdoc />
		//public override void Save(SettingsStorage storage)
		//{
		//	base.Save(storage);

		//	storage.SetValue(nameof(LongMa), LongMa.Save());
		//	storage.SetValue(nameof(ShortMa), ShortMa.Save());
		//}
	}
}