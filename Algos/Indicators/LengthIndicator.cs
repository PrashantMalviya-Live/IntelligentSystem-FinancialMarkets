using System;
using System.Collections.Generic;
using System.ComponentModel;
namespace Algorithms.Indicators
{



	/// <summary>
	/// The base class for indicators with one resulting value and based on the period.
	/// </summary>
	/// <typeparam name="TResult">Result values type.</typeparam>
	public abstract class LengthIndicator<TResult> : BaseIndicator
	{
		/// <summary>
		/// Initialize <see cref="LengthIndicator{T}"/>.
		/// </summary>
		protected LengthIndicator()
		{
			Buffer = new List<TResult>();
		}

		/// <inheritdoc />
		public override void Reset()
		{
			Buffer.Clear();
			base.Reset();
		}

		private int _length = 1;

		/// <summary>
		/// Period length. By default equal to 1.
		/// </summary>
		public int Length
		{
			get => _length;
			set
			{
				if (value < 1)
					throw new ArgumentOutOfRangeException(nameof(value), value, "Out of range");

				_length = value;

				Reset();
			}
		}

		/// <inheritdoc />
		public override bool IsFormed => Buffer.Count >= Length;

		/// <summary>
		/// The buffer for data storage.
		/// </summary>
		[Browsable(false)]
		protected IList<TResult> Buffer { get; }

		//public override void Load(SettingsStorage storage)
  //      {
  //          base.Load(storage);
  //          Length = storage.GetValue<int>(nameof(Length));
  //      }

        ///// <inheritdoc />
        //public override void Save(SettingsStorage storage)
        //{
        //	base.Save(storage);
        //	storage.SetValue(nameof(Length), Length);
        //}

        /// <inheritdoc />
        public override string ToString() => base.ToString() + " " + Length;
	}
}
