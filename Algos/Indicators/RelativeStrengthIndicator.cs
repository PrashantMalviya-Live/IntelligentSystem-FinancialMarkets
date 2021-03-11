using System.ComponentModel;
using System.Linq;


namespace Algorithms.Indicators
{
	/// <summary>
	/// Relative Strength Index.
	/// </summary>
	[DisplayName("RSI")]
	public class RelativeStrengthIndex : LengthIndicator<decimal>
	{
		private readonly SmoothedMovingAverage _gain;
		private readonly SmoothedMovingAverage _loss;
		private bool _isInitialized;
		private decimal _last;

		/// <summary>
		/// Initializes a new instance of the <see cref="RelativeStrengthIndex"/>.
		/// </summary>
		public RelativeStrengthIndex()
		{
			_gain = new SmoothedMovingAverage();
			_loss = new SmoothedMovingAverage();

			Length = 14;
		}

		/// <inheritdoc />
		public override bool IsFormed => _gain.IsFormed;

		/// <inheritdoc />
		public override void Reset()
		{
			_loss.Length = _gain.Length = Length;
			base.Reset();
		}

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			var newValue = input.GetValue<decimal>();

			if (!_isInitialized)
			{
				if (input.IsFinal)
				{
					_last = newValue;
					_isInitialized = true;
				}

				return new DecimalIndicatorValue(this);
			}

			var delta = newValue - _last;

			var gainValue = _gain.Process(input.SetValue(this, delta > 0 ? delta : 0m)).GetValue<decimal>();
			var lossValue = _loss.Process(input.SetValue(this, delta > 0 ? 0m : -delta)).GetValue<decimal>();

			if (input.IsFinal)
				_last = newValue;

			if (lossValue == 0)
				return new DecimalIndicatorValue(this, 100m);

			if (gainValue / lossValue == 1)
				return new DecimalIndicatorValue(this, 0m);

			return new DecimalIndicatorValue(this, 100m - 100m / (1m + gainValue / lossValue));
		}
	}
}
