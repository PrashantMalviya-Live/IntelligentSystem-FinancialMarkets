using System;
using System.ComponentModel;

namespace Algorithms.Indicators
{
	/// <summary>
	/// Welles Wilder Average Directional Index.
	/// </summary>
	public class AverageDirectionalIndex : BaseComplexIndicator
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="AverageDirectionalIndex"/>.
		/// </summary>
		public AverageDirectionalIndex()
			: this(new DirectionalIndex { Length = 14 }, new WilderMovingAverage { Length = 14 })
		{
		}

		public AverageDirectionalIndex(int length)
			: this(new DirectionalIndex { Length = length }, new WilderMovingAverage { Length = length })
		{
			Length = length;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AverageDirectionalIndex"/>.
		/// </summary>
		/// <param name="dx">Welles Wilder Directional Movement Index.</param>
		/// <param name="movingAverage">Moving Average.</param>
		public AverageDirectionalIndex(DirectionalIndex dx, LengthIndicator<decimal> movingAverage)
		{
			if (dx == null)
				throw new ArgumentNullException(nameof(dx));

			if (movingAverage == null)
				throw new ArgumentNullException(nameof(movingAverage));

			InnerIndicators.Add(Dx = dx);
			InnerIndicators.Add(MovingAverage = movingAverage);
			Mode = ComplexIndicatorModes.Sequence;
		}

		/// <summary>
		/// Welles Wilder Directional Movement Index.
		/// </summary>
		[Browsable(false)]
		public DirectionalIndex Dx { get; }

		/// <summary>
		/// Moving Average.
		/// </summary>
		[Browsable(false)]
		public LengthIndicator<decimal> MovingAverage { get; }

		/// <summary>
		/// Period length.
		/// </summary>
		public virtual int Length
		{
			get => MovingAverage.Length;
			set
			{
				MovingAverage.Length = Dx.Length = value;
				Reset();
			}
		}

		///// <inheritdoc />
		//public override void Load(SettingsStorage storage)
		//{
		//	base.Load(storage);
		//	Length = storage.GetValue<int>(nameof(Length));
		//}

		///// <inheritdoc />
		//public override void Save(SettingsStorage storage)
		//{
		//	base.Save(storage);
		//	storage.SetValue(nameof(Length), Length);
		//}
	}
}