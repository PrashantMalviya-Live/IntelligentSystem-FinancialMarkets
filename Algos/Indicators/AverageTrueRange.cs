using System;
using System.ComponentModel;
namespace Algorithms.Indicators

{
	using System;
	using System.ComponentModel;

	/// <summary>
	/// The average true range <see cref="AverageTrueRange.TrueRange"/>.
	/// </summary>
	[DisplayName("ATR")]
	public class AverageTrueRange : LengthIndicator<IIndicatorValue>
	{
		private bool _isFormed;

		/// <summary>
		/// Initializes a new instance of the <see cref="AverageTrueRange"/>.
		/// </summary>
		public AverageTrueRange()
			: this(new WilderMovingAverage(), new TrueRange())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AverageTrueRange"/>.
		/// </summary>
		/// <param name="movingAverage">Moving Average.</param>
		/// <param name="trueRange">True range.</param>
		public AverageTrueRange(LengthIndicator<decimal> movingAverage, TrueRange trueRange)
		{
			MovingAverage = movingAverage ?? throw new ArgumentNullException(nameof(movingAverage));
			TrueRange = trueRange ?? throw new ArgumentNullException(nameof(trueRange));
		}

		/// <summary>
		/// Moving Average.
		/// </summary>
		[Browsable(false)]
		public LengthIndicator<decimal> MovingAverage { get; }

		/// <summary>
		/// True range.
		/// </summary>
		[Browsable(false)]
		public TrueRange TrueRange { get; }

		/// <inheritdoc />
		public override bool IsFormed => _isFormed;

		/// <inheritdoc />
		public override void Reset()
		{
			base.Reset();

			_isFormed = false;

			MovingAverage.Length = Length;
			TrueRange.Reset();
		}

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			// используем дополнительную переменную IsFormed, 
			// т.к. нужна задержка в один период для корректной инициализации скользящей средней
			_isFormed = MovingAverage.IsFormed;

			return MovingAverage.Process(TrueRange.Process(input));
		}
	}
}