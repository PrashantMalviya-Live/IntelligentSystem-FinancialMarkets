using System;
using System.Collections.Generic;
using System.Linq;

namespace Algorithms.Indicators
{
	

	/// <summary>
	/// The container, storing indicators data.
	/// </summary>
	public class IndicatorContainer : IIndicatorContainer
	{
		private readonly List<Tuple<IIndicatorValue, IIndicatorValue>> _values = new List<Tuple<IIndicatorValue, IIndicatorValue>>();

		/// <summary>
		/// The maximal number of indicators values.
		///</summary>
		public int MaxValueCount
		{
			get => _values.Capacity;
			set => _values.Capacity = value;
		}

		/// <summary>
		/// The current number of saved values.
		/// </summary>
		public int Count => _values.Count;

		/// <summary>
		/// Add new values.
		/// </summary>
		/// <param name="input">The input value of the indicator.</param>
		/// <param name="result">The resulting value of the indicator.</param>
		public virtual void AddValue(IIndicatorValue input, IIndicatorValue result)
		{
			_values.Add(Tuple.Create(input, result));
		}

		/// <summary>
		/// To get all values of the identifier.
		/// </summary>
		/// <returns>All values of the identifier. The empty set, if there are no values.</returns>
		public virtual IEnumerable<Tuple<IIndicatorValue, IIndicatorValue>> GetValues()
		{
			return _values;//.SyncGet(c => c.Reverse().ToArray());
		}

		/// <summary>
		/// To get the indicator value by the index.
		/// </summary>
		/// <param name="index">The sequential number of value from the end.</param>
		/// <returns>Input and resulting values of the indicator.</returns>
		public virtual Tuple<IIndicatorValue, IIndicatorValue> GetValue(int index)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index), index, "Argument out of range");

			lock (_values)
			{
				if (index >= _values.Count)
					throw new ArgumentOutOfRangeException(nameof(index), index, "Argument out of range");

				return _values[_values.Count - 1 - index];
			}
		}

		/// <summary>
		/// To delete all values of the indicator.
		/// </summary>
		public virtual void ClearValues()
		{
			_values.Clear();
		}
	}
}