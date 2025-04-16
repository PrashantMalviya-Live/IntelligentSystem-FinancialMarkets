using System.ComponentModel;
using System.Linq;

namespace Algorithms.Indicators
{
	public class StochasticOscillator : BaseComplexIndicator
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="StochasticOscillator"/>.
		/// </summary>
		public StochasticOscillator()
		{
			InnerIndicators.Add(K = new StochasticK());
			InnerIndicators.Add(D = new SimpleMovingAverage { Length = 3 });

			Mode = ComplexIndicatorModes.Sequence;
		}

		/// <summary>
		/// %K.
		/// </summary>
		[TypeConverter(typeof(ExpandableObjectConverter))]
		public StochasticK K { get; }

		/// <summary>
		/// %D.
		/// </summary>
		[TypeConverter(typeof(ExpandableObjectConverter))]
		public SimpleMovingAverage D { get; }
	}
}