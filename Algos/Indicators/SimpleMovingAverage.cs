using System;
using System.ComponentModel;
using System.Linq;

namespace Algorithms.Indicators
{

	/// <summary>
	/// Simple moving average.
	/// </summary>
	[DisplayName("SMA")]
	public class SimpleMovingAverage : LengthIndicator<decimal>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SimpleMovingAverage"/>.
		/// </summary>
		public SimpleMovingAverage()
		{
			Length = 32;
		}
		public SimpleMovingAverage(int length)
		{
			Length = length;
		}

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			var newValue = input.GetValue<decimal>();

			if (input.IsFinal)
			{
				Buffer.Add(newValue);

				if (Buffer.Count > Length)
					Buffer.RemoveAt(0);
			}

			if (input.IsFinal)
				return new DecimalIndicatorValue(this, Buffer.Sum() / Length);

			return new DecimalIndicatorValue(this, (Buffer.Skip(1).Sum() + newValue) / Length);
		}

        //public T GetCurrentValue<T>()
        //{
        //    return GetValue<T>(0);
        //}
        //public decimal GetCurrentValue()
        //{
        //    return GetValue<decimal>(0);
        //}

        //      /// <summary>
        ///// To get the indicator value by the index (0 - last value).
        ///// </summary>
        ///// <param name="indicator">Indicator.</param>
        ///// <param name="index">The value index.</param>
        ///// <returns>Indicator value.</returns>
        //public decimal? GetNullableValue(int index)
        //      {
        //          return GetValue<decimal?>(index);
        //      }

        //      /// <summary>
        //      /// To get the indicator value by the index (0 - last value).
        //      /// </summary>
        //      /// <param name="indicator">Indicator.</param>
        //      /// <param name="index">The value index.</param>
        //      /// <returns>Indicator value.</returns>
        //      public decimal GetValue(int index)
        //      {
        //          return GetNullableValue(index) ?? 0;
        //      }
        //      public decimal GetValue()
        //      {
        //          var container = Container;
        //          var value = container.GetValue(0).Item2;
        //          return typeof(IIndicatorValue).IsAssignableFrom(typeof(decimal)) ? (decimal)Convert.ChangeType(value, typeof(decimal)) : value.GetValue<decimal>();
        //      }

        /// <summary>
        /// To get the indicator value by the index (0 - last value).
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="indicator">Indicator.</param>
        /// <param name="index">The value index.</param>
        /// <returns>Indicator value.</returns>
        public T GetValue<T>(int index)
        {
            var container = Container;

            if (index >= container.Count)
            {
                //if (index == 0 && typeof(decimal) == typeof(T))
                //	return indicator.GetValue<T>(0);
                //else
                return default;
                //if (index == 0 && typeof(decimal) == typeof(T))
                //	return 0m.To<T>();
                //else
                //throw new ArgumentOutOfRangeException(nameof(index), index, LocalizedStrings.Str914Params.Put(indicator.Name));
            }
            var value = container.GetValue(index).Item2;

            if (value.IsEmpty)
            {
                if (value is T t)
                    return t;

                return default;
            }

            return typeof(IIndicatorValue).IsAssignableFrom(typeof(T)) ? (T)Convert.ChangeType(value, typeof(T)) : value.GetValue<T>();
        }

    }
}