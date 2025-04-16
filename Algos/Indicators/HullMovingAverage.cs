using System;
using System.ComponentModel;
namespace Algorithms.Indicators
{
	using System.ComponentModel;
	using System;


	/// <summary>
	/// Hull Moving Average.
	/// </summary>
	[DisplayName("HMA")]
	public class HullMovingAverage : LengthIndicator<decimal>
	{
		private readonly WeightedMovingAverage _wmaSlow = new WeightedMovingAverage();
		private readonly WeightedMovingAverage _wmaFast = new WeightedMovingAverage();
		private readonly WeightedMovingAverage _wmaResult = new WeightedMovingAverage();

		/// <summary>
		/// Initializes a new instance of the <see cref="HullMovingAverage"/>.
		/// </summary>
		public HullMovingAverage()
		{
			Length = 10;
			SqrtPeriod = 0;
		}

		private int _sqrtPeriod;

		/// <summary>
		/// Period of resulting average. If equal to 0, period of resulting average is equal to the square root of HMA period. By default equal to 0.
		/// </summary>
		public int SqrtPeriod
		{
			get => _sqrtPeriod;
			set
			{
				_sqrtPeriod = value;
				_wmaResult.Length = value == 0 ? (int)Math.Sqrt(Length) : value;
			}
		}

		/// <inheritdoc />
		public override bool IsFormed => _wmaResult.IsFormed;

		/// <inheritdoc />
		public override void Reset()
		{
			base.Reset();

			_wmaSlow.Length = Length;
			_wmaFast.Length = Length / 2;
			_wmaResult.Length = SqrtPeriod == 0 ? (int)Math.Sqrt(Length) : SqrtPeriod;
		}

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			_wmaSlow.Process(input);
			_wmaFast.Process(input);

			if (_wmaFast.IsFormed && _wmaSlow.IsFormed)
			{
				var diff = 2 *  _wmaFast.GetCurrentValue<decimal>() - _wmaSlow.GetCurrentValue<decimal>();
				_wmaResult.Process(diff);
			}

			return new DecimalIndicatorValue(this, _wmaResult.GetCurrentValue<decimal>());
		}

		///// <inheritdoc />
		//public override void Load(SettingsStorage storage)
		//{
		//	base.Load(storage);
		//	SqrtPeriod = storage.GetValue<int>(nameof(SqrtPeriod));
		//}

		///// <inheritdoc />
		//public override void Save(SettingsStorage storage)
		//{
		//	base.Save(storage);
		//	storage.SetValue(nameof(SqrtPeriod), SqrtPeriod);
		//}
	}
}