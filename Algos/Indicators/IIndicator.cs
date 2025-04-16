
namespace Algorithms.Indicators
{
	using System;
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;


    public interface IEquationComponent
	{

	}
    /// <summary>
    /// The interface describing indicator.
    /// </summary>

    //[JsonPolymorphic( UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    //[JsonDerivedType(typeof(ExponentialMovingAverage), typeDiscriminator: "base")]
    public interface IIndicator : IEquationComponent  // : ICloneable<T>
	{
		/// <summary>
		/// Unique ID.
		/// </summary>
		Guid Id { get; }

		/// <summary>
		/// Indicator name.
		/// </summary>
		string Name { get; set; }

		/// <summary>
		/// Whether the indicator is set.
		/// </summary>
		bool IsFormed { get; }

		int TimeSpanInMins { get; set; }
		/// <summary>
		/// The container storing indicator data.
		/// </summary>
		IIndicatorContainer Container { get; }

		/// <summary>
		/// Indicator based on another indicator, such as EMA based on another EMA. 
		/// The based indicator (generally candle) will have this value null
		/// </summary>
		IIndicator? ChildIndicator { get; set; }

		/// <summary>
		/// Input values type.
		/// </summary>
		Type InputType { get; }

		/// <summary>
		/// Result values type.
		/// </summary>
		Type ResultType { get; }

        /// <summary>
        /// The indicator change event (for example, a new value is added).
        /// </summary>
        [field: NonSerialized]
        event Action<IIndicatorValue, IIndicatorValue> Changed;

        /// <summary>
        /// The event of resetting the indicator status to initial. The event is called each time when initial settings are changed (for example, the length of period).
        /// </summary>
        [field: NonSerialized]
        event Action Reseted;

		/// <summary>
		/// To handle the input value.
		/// </summary>
		/// <param name="input">The input value.</param>
		/// <returns>The new value of the indicator.</returns>
		IIndicatorValue Process(IIndicatorValue input);


        /// <summary>
        /// To reset the indicator status to initial. The method is called each time when initial settings are changed (for example, the length of period).
        /// </summary>
        void Reset();
	}
}
