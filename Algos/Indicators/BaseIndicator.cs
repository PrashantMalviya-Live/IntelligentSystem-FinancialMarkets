using Algorithm.Algorithm;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace Algorithms.Indicators
{

	/// <summary>
	/// The base Indicator.
	/// </summary>
	public abstract class BaseIndicator : IIndicator //Cloneable<IIndicator>,
    {
		/// <summary>
		/// Initialize <see cref="BaseIndicator"/>.
		/// </summary>
		protected BaseIndicator()
		{
			var type = GetType();

			_name = type.Name;
			//InputType = type.GetValueType(true);
			//ResultType = type.GetValueType(false);
		}

		/// <inheritdoc />
		[Browsable(false)]
		public Guid Id { get; private set; } = Guid.NewGuid();

		private string _name;

		/// <inheritdoc />
		public virtual string Name
		{
			get => _name;
			set
			{
				if (value == String.Empty)
					throw new ArgumentNullException(nameof(value));

				_name = value;
			}
		}

		/// <inheritdoc />
		public virtual void Reset()
		{
			IsFormed = false;
			Container.ClearValues();
			Reseted?.Invoke();
		}

        ///// <summary>
        ///// Save settings.
        ///// </summary>
        ///// <param name="storage">Settings storage.</param>
        //public virtual void Save(SettingsStorage storage)
        //{
        //	storage.SetValue(nameof(Id), Id);
        //	storage.SetValue(nameof(Name), Name);
        //}

        /// <summary>
        /// Load settings.
        /// </summary>
        /// <param name="storage">Settings storage.</param>
        //public virtual void Load(SettingsStorage storage)
        //{
        //    Id = storage.GetValue<Guid>(nameof(Id));
        //    Name = storage.GetValue<string>(nameof(Name));
        //}


        /// <inheritdoc />
        [Browsable(false)]
		public virtual bool IsFormed { get; protected set; }

		/// <inheritdoc />
		[Browsable(false)]
		public IIndicatorContainer Container { get; } = new IndicatorContainer();

		/// <inheritdoc />
		[Browsable(false)]
		public virtual IIndicator ChildIndicator { get; set; }

        /// <inheritdoc />
        [Browsable(false)]
        public virtual int TimeSpanInMins { get; set; }


        /// <inheritdoc />
        [Browsable(false)]
		public virtual Type InputType { get; }

		/// <inheritdoc />
		[Browsable(false)]
		public virtual Type ResultType { get; }

		/// <inheritdoc />
		public event Action<IIndicatorValue, IIndicatorValue> Changed;

		/// <inheritdoc />
		public event Action Reseted;

		/// <inheritdoc />
		public virtual IIndicatorValue Process(IIndicatorValue input)
		{
			var result = OnProcess(input);

			result.InputValue = input;
			//var result = value as IIndicatorValue ?? input.SetValue(value);

			if (input.IsFinal)
			{
				result.IsFinal = input.IsFinal;
				Container.AddValue(input, result);
			}

			if (IsFormed && !result.IsEmpty)
				RaiseChangedEvent(input, result);

			return result;
		}

		/// <summary>
		/// To handle the input value.
		/// </summary>
		/// <param name="input">The input value.</param>
		/// <returns>The resulting value.</returns>
		protected abstract IIndicatorValue OnProcess(IIndicatorValue input);

		/// <summary>
		/// To call the event <see cref="BaseIndicator.Changed"/>.
		/// </summary>
		/// <param name="input">The input value of the indicator.</param>
		/// <param name="result">The resulting value of the indicator.</param>
		protected void RaiseChangedEvent(IIndicatorValue input, IIndicatorValue result)
		{
			if (input == null)
				throw new ArgumentNullException(nameof(input));

			if (result == null)
				throw new ArgumentNullException(nameof(result));

			Changed?.Invoke(input, result);
		}

		/// <summary>
		/// Create a copy of <see cref="IIndicator"/>.
		/// </summary>
		/// <returns>Copy.</returns>
		//public override IIndicator Clone()
		//{
		//	return PersistableHelper.Clone(this);
		//}

		/// <inheritdoc />
		public override string ToString() => Name;
	}

    class BaseIndicatorCollection : IEnumerable<BaseIndicator>, IEnumerable
    {
        List<BaseIndicator> baseIndicators;

        // Explicit for IEnumerable because weakly typed collections are Bad
        System.Collections.IEnumerator IEnumerable.GetEnumerator()
        {
            // uses the strongly typed IEnumerable<T> implementation
            return this.GetEnumerator();
        }

        // Normal implementation for IEnumerable<T>
        public IEnumerator<BaseIndicator> GetEnumerator()
        {
            foreach (BaseIndicator baseIndicator in this.baseIndicators)
            {
                yield return baseIndicator;
                //nb: if SomeCollection is not strongly-typed use a cast:
                // yield return (Foo)foo;
                // Or better yet, switch to an internal collection which is
                // strongly-typed. Such as List<T> or T[], your choice.
            }

            // or, as pointed out: return this.foos.GetEnumerator();
        }
    }
}