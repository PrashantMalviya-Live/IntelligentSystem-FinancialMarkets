using System;
using System.ComponentModel;

namespace Algorithms.Indicators
{
	/// <summary>
	/// Welles Wilder Directional Movement Index.
	/// </summary>
	public class DirectionalIndex : BaseComplexIndicator
	{
		public sealed class DxValue : ComplexIndicatorValue
		{
			private decimal _value;

			public DxValue(IIndicator indicator)
				: base(indicator)
			{
			}

			public override IIndicatorValue SetValue<T>(IIndicator indicator, T value)
			{
				IsEmpty = false;
				_value = Convert.ToDecimal(value);
				return new DecimalIndicatorValue(indicator, _value);
			}

			public T GetValue<T>()
			{
				//return Convert.ToDecimal(_value); //_value.To<T>();
				return (T)Convert.ChangeType(_value, typeof(T));
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DirectionalIndex"/>.
		/// </summary>
		public DirectionalIndex()
		{
			InnerIndicators.Add(Plus = new DiPlus());
			InnerIndicators.Add(Minus = new DiMinus());
		}

		/// <summary>
		/// Period length.
		/// </summary>
		public virtual int Length
		{
			get => Plus.Length;
			set
			{
				Plus.Length = Minus.Length = value;
				Reset();
			}
		}

		/// <summary>
		/// DI+.
		/// </summary>
		[TypeConverter(typeof(ExpandableObjectConverter))]
		[DisplayName("DI+")]
		[Description("DI+.")]
		public DiPlus Plus { get; }

		/// <summary>
		/// DI-.
		/// </summary>
		[TypeConverter(typeof(ExpandableObjectConverter))]
		[DisplayName("DI-")]
		[Description("DI-.")]
		public DiMinus Minus { get; }

		/// <inheritdoc />
		protected override IIndicatorValue OnProcess(IIndicatorValue input)
		{
			var value = new DxValue(this) { IsFinal = input.IsFinal };

			var plusValue = Plus.Process(input);
			var minusValue = Minus.Process(input);

			value.InnerValues.Add(Plus, plusValue);
			value.InnerValues.Add(Minus, minusValue);

			if (plusValue.IsEmpty || minusValue.IsEmpty)
				return value;

			var plus = plusValue.GetValue<decimal>();
			var minus = minusValue.GetValue<decimal>();

			var diSum = plus + minus;
			var diDiff = Math.Abs(plus - minus);

			value.InnerValues.Add(this, value.SetValue(this, diSum != 0m ? (100 * diDiff / diSum) : 0m));

			return value;
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