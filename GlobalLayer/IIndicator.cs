
namespace GlobalLayer
{
	using System;


	/// <summary>
	/// The interface describing indicator.
	/// </summary>
	public interface IIndicator // : ICloneable<T>
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

		/// <summary>
		/// The container storing indicator data.
		/// </summary>
		IIndicatorContainer Container { get; }

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
		event Action<IIndicatorValue, IIndicatorValue> Changed;

		/// <summary>
		/// The event of resetting the indicator status to initial. The event is called each time when initial settings are changed (for example, the length of period).
		/// </summary>
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
