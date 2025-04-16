using System;
using System.ComponentModel;
using GlobalLayer;

namespace Algorithms.Indicators
{
	/// <summary>
	/// The part of the indicator <see cref="DirectionalIndex"/>.
	/// </summary>
	public abstract class DiPart : LengthIndicator<decimal>
	{
		private readonly AverageTrueRange _averageTrueRange;
		private readonly LengthIndicator<decimal> _movingAverage;
		private Candle _lastCandle;
		private bool _isFormed;

		/// <summary>
		/// Initialize <see cref="DiPart"/>.
		/// </summary>
		protected DiPart()
		{
			_averageTrueRange = new AverageTrueRange(new WilderMovingAverage2(), new TrueRange());
			_movingAverage = new WilderMovingAverage2();

			//Length = 5;
		}
		protected DiPart(int length)
		{
			_averageTrueRange = new AverageTrueRange(new WilderMovingAverage2(length), new TrueRange());
			_movingAverage = new WilderMovingAverage2(length);

			Length = length;
		}

		/// <inheritdoc />
		public override void Reset()
		{
			base.Reset();

			_averageTrueRange.Length = Length;
			_movingAverage.Length = Length;

			_lastCandle = null;
			_isFormed = false;
		}

		/// <inheritdoc />
		public override bool IsFormed => _isFormed;

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			decimal? result = null;

			var candle = ((SingleIndicatorValue<GlobalLayer.Candle>)input).Value; //input as Candle;//.GetValue<Candle>();

			// 1 period delay
			//_isFormed = _averageTrueRange.IsFormed && _movingAverage.IsFormed;

			_averageTrueRange.Process(input);

			if (_lastCandle != null)
			{
				var trValue = _averageTrueRange.GetCurrentValue<decimal>();

				var maValue = _movingAverage.Process(new DecimalIndicatorValue(this, GetValue(candle, _lastCandle)) { IsFinal = input.IsFinal });

				if (!maValue.IsEmpty)
					result = trValue != 0m ? 100m * maValue.GetValue<decimal>() / trValue : 0m;
			}

			// 1 period delay Removed.
			_isFormed = _averageTrueRange.IsFormed && _movingAverage.IsFormed;

			if (input.IsFinal)
				_lastCandle = candle;

			return result == null ? new DecimalIndicatorValue(this) : new DecimalIndicatorValue(this, result.Value);
		}

		/// <summary>
		/// To get the part value.
		/// </summary>
		/// <param name="current">The current candle.</param>
		/// <param name="prev">The previous candle.</param>
		/// <returns>Value.</returns>
		protected abstract decimal GetValue(Candle current, Candle prev);
	}
}