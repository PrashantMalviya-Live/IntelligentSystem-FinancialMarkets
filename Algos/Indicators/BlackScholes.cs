#region S# License
/******************************************************************************************
NOTICE!!!  This program and source code is owned and licensed by
StockSharp, LLC, www.stocksharp.com
Viewing or use of this code requires your acceptance of the license
agreement found at https://github.com/StockSharp/StockSharp/blob/master/LICENSE
Removal of this comment is a violation of the license agreement.

Project: StockSharp.Algo.Derivatives.Algo
File: BlackScholes.cs
Created: 2015, 11, 11, 2:32 PM

Copyright 2010 by StockSharp, LLC
*******************************************************************************************/
#endregion S# License
namespace Algorithms.Indicators
{
	using System;
	using GlobalLayer;

	/// <summary>
	/// The model for calculating Greeks values by the Black-Scholes formula.
	/// </summary>
	public class BlackScholes : IBlackScholes
	{
		public virtual Instrument Option { get; set; }

		public virtual InstrumentType OptionType {
			get => Option.InstrumentType == "Call" ? InstrumentType.CE : InstrumentType.PE;
		}
		public decimal RiskFree { get; set; }
		public virtual decimal Dividend { get; set; }
		private int _roundDecimals = -1;

		/// <summary>
		/// The number of decimal places at calculated values. The default is -1, which means no values rounding.
		/// </summary>
		public virtual int RoundDecimals
		{
			get => _roundDecimals;
			set
			{
				_roundDecimals = value;
			}
		}
		private Instrument _underlyingAsset { get; set; }


		/// <summary>
		/// The standard deviation by default.
		/// </summary>
		public decimal DefaultDeviation => (ImpliedVolatility(DateTime.Now, Option.LastPrice) ?? 0) / 100;

		/// <summary>
		/// The time before expiration calculation.
		/// </summary>
		/// <param name="currentTime">The current time.</param>
		/// <returns>The time remaining until expiration. If the value is equal to <see langword="null" />, then the value calculation currently is impossible.</returns>
		public virtual double? GetExpirationTimeLine(DateTimeOffset currentTime)
		{
			return OptionGreeks.GetExpirationTimeLine(Option.Expiry.Value, currentTime);
		}
		/// <summary>
		/// To round to <see cref="BlackScholes.RoundDecimals"/>.
		/// </summary>
		/// <param name="value">The initial value.</param>
		/// <returns>The rounded value.</returns>
		protected decimal? TryRound(decimal? value)
		{
			if (value != null && RoundDecimals >= 0)
				value = Math.Round(value.Value, RoundDecimals);

			return value;
		}

		/// <inheritdoc />
		public virtual decimal? Premium(DateTimeOffset currentTime, decimal? deviation = null, decimal? assetPrice = null)
		{
			deviation = deviation ?? DefaultDeviation;

			var timeToExp = GetExpirationTimeLine(currentTime);

			if (timeToExp == null)
				return null;

			return TryRound(OptionGreeks.Premium(OptionType,Option.Strike, assetPrice.Value, RiskFree, Dividend, 
				deviation.Value, timeToExp.Value, D1(deviation.Value, assetPrice.Value, timeToExp.Value)));
		}

		/// <inheritdoc />
		public virtual decimal? Delta(DateTimeOffset currentTime, decimal? deviation = null, decimal? assetPrice = null)
		{
			var timeToExp = GetExpirationTimeLine(currentTime);

			if (timeToExp == null)
				return null;

			return TryRound(OptionGreeks.Delta(OptionType, assetPrice.Value, D1(deviation ?? DefaultDeviation, assetPrice.Value, timeToExp.Value)));
		}

		/// <inheritdoc />
		public virtual decimal? Gamma(DateTimeOffset currentTime, decimal? deviation = null, decimal? assetPrice = null)
		{
			deviation = deviation ?? DefaultDeviation;

			var timeToExp = GetExpirationTimeLine(currentTime);

			if (timeToExp == null)
				return null;

			return TryRound(OptionGreeks.Gamma(assetPrice.Value, deviation.Value, timeToExp.Value, D1(deviation.Value, assetPrice.Value, timeToExp.Value)));
		}

		/// <inheritdoc />
		public virtual decimal? Vega(DateTimeOffset currentTime, decimal? deviation = null, decimal? assetPrice = null)
		{
			var timeToExp = GetExpirationTimeLine(currentTime);

			if (timeToExp == null)
				return null;

			return TryRound(OptionGreeks.Vega(assetPrice.Value, timeToExp.Value, D1(deviation ?? DefaultDeviation, assetPrice.Value, timeToExp.Value)));
		}

		/// <inheritdoc />
		public virtual decimal? Theta(DateTimeOffset currentTime, decimal? deviation = null, decimal? assetPrice = null)
		{
			deviation = deviation ?? DefaultDeviation;
			
			var timeToExp = GetExpirationTimeLine(currentTime);

			if (timeToExp == null)
				return null;

			return TryRound(OptionGreeks.Theta(OptionType, Option.Strike, assetPrice.Value, RiskFree, deviation.Value, timeToExp.Value, D1(deviation.Value, assetPrice.Value, timeToExp.Value)));
		}

		/// <inheritdoc />
		public virtual decimal? Rho(DateTimeOffset currentTime, decimal? deviation = null, decimal? assetPrice = null)
		{
			deviation = deviation ?? DefaultDeviation;

			var timeToExp = GetExpirationTimeLine(currentTime);

			if (timeToExp == null)
				return null;

			return TryRound(OptionGreeks.Rho(OptionType, Option.Strike, assetPrice.Value, RiskFree, deviation.Value, timeToExp.Value, D1(deviation.Value, assetPrice.Value, timeToExp.Value)));
		}

		/// <inheritdoc />
		public virtual decimal? ImpliedVolatility(DateTimeOffset currentTime, decimal premium)
		{
			//var timeToExp = GetExpirationTimeLine();
			return TryRound(OptionGreeks.ImpliedVolatility(premium, diviation => Premium(currentTime, diviation)));
		}

		/// <summary>
		/// To calculate the d1 parameter of the option fulfilment probability estimating.
		/// </summary>
		/// <param name="deviation">Standard deviation.</param>
		/// <param name="assetPrice">Underlying asset price.</param>
		/// <param name="timeToExp">The option period before the expiration.</param>
		/// <returns>The d1 parameter.</returns>
		protected virtual double D1(decimal deviation, decimal assetPrice, double timeToExp)
		{
			return OptionGreeks.D1(assetPrice, Option.Strike, RiskFree, Dividend, deviation, timeToExp);
		}
		
	}
}