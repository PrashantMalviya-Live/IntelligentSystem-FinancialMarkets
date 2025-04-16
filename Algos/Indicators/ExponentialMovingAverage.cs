namespace Algorithms.Indicators
{
    using GlobalLayer;
    using System;
    using System.ComponentModel;
    using System.Linq;

    /// <summary>
    /// Exponential Moving Average.
    /// </summary>
	[DisplayName("EMA")]
	public class ExponentialMovingAverage : LengthIndicator<decimal>
	{
		private decimal _prevFinalValue;
		private decimal _multiplier = 1;

		/// <summary>
		/// Initializes a new instance of the <see cref="ExponentialMovingAverage"/>.
		/// </summary>
		public ExponentialMovingAverage(int length = 32)
		{
			Length = length;
		}

		/// <inheritdoc />
		public override void Reset()
		{
			base.Reset();
			_multiplier = 2m / (Length + 1);
			_prevFinalValue = 0;
		}

		//public T GetCurrentValue<T>()
		//{
		//	return GetValue<T>(0);
		//}
		//public decimal GetCurrentValue()
		//{
		//	return GetValue<decimal>(0);
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


		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			//var candle = input.GetValue<Candle>();
			//var newValue = candle.ClosePrice;// input.GetValue<decimal>();
			
			var newValue = input.GetValue<decimal>();
			if (!IsFormed)
			{
				if (input.IsFinal)
				{
					Buffer.Add(newValue);

					_prevFinalValue = Buffer.Sum() / Length;

					return new DecimalIndicatorValue(this, _prevFinalValue);
				}
				else
				{
					return new DecimalIndicatorValue(this, (Buffer.Skip(1).Sum() + newValue) / Length);
				}
			}
			else
			{
				// если sma сформирована 
				// если IsFinal = true рассчитываем ema и сохраняем для последующих расчетов с промежуточными значениями
				var curValue = (newValue - _prevFinalValue) * _multiplier + _prevFinalValue;

				if (input.IsFinal)
					_prevFinalValue = curValue;

				return new DecimalIndicatorValue(this, curValue);
			}
		}

    }
}