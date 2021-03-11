using GlobalLayer;
using System;

namespace Algorithms.Utilities
{
	/// <summary>
	/// The model for calculating Greeks values by the Black-Scholes formula.
	/// </summary>
	public class BS : IBlackScholes
	{
		public virtual Option option { get; }


		/// Initializes a new instance of the <see cref="BlackScholes"/>.
		/// </summary>
		/// <param name="option">Options contract.</param>
		/// <param name="securityProvider">The provider of information about instruments.</param>
		/// <param name="dataProvider">The market data provider.</param>
		/// <param name="exchangeInfoProvider">Exchanges and trading boards provider.</param>
		public BS(Option optionInstrument)
		{
			option = optionInstrument;
		}

		/// <inheritdoc />
		public decimal RiskFree { get; set; } = 0.1m;

		/// <inheritdoc />
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

		private Instrument _underlyingAsset;

		/// <summary>
		/// Underlying asset.
		/// </summary>
		public virtual Instrument UnderlyingAsset
		{
			get => _underlyingAsset ?? (_underlyingAsset = option.GetUnderlyingAsset());
			set => _underlyingAsset = value;
		}

		/// <summary>
		/// The standard deviation by default.
		/// </summary>
		public decimal? DefaultDeviation => Convert.ToDecimal(DerivativesHelper.BlackScholesImpliedVol(Convert.ToDouble(option.LastPrice), Convert.ToDouble(option.Strike),
			Convert.ToDouble(option.BaseInstrumentPrice), (option.Expiry - option.LastTradeTime).Value.TotalDays / 365, Convert.ToDouble(RiskFree),
			Convert.ToDouble(Dividend), option.InstrumentType.Trim(' ').ToLower() == "ce" ? PutCallFlag.Call : PutCallFlag.Put));
			
			//ImpliedVolatility(option.LastTradeTime.Value, option.LastPrice);

		/// <summary>
		/// The time before expiration calculation.
		/// </summary>
		/// <param name="startDateTime">The time from where to calculation expiration.</param>
		/// <returns>The time remaining until expiration. If the value is equal to <see langword="null" />, then the value calculation currently is impossible.</returns>
		public virtual double? GetExpirationTimeLine(DateTime startDateTime)
		{
			return DerivativesHelper.GetExpirationTimeLine(option.GetExpirationTime(), startDateTime);
		}

		/// <summary>
		/// To get the price of the underlying asset.
		/// </summary>
		/// <param name="assetPrice">The price of the underlying asset if it is specified.</param>
		/// <returns>The price of the underlying asset. If the value is equal to <see langword="null" />, then the value calculation currently is impossible.</returns>
		public decimal GetAssetPrice(decimal assetPrice)
		{
			return option.BaseInstrumentPrice; // assetPrice;// option.LastPrice;
		}

		/// <summary>
		/// Option type.
		/// </summary>
		public InstrumentType OptionType
		{
			get
			{
				return option.InstrumentType.Trim(' ').ToLower() == "ce" ? InstrumentType.CE: InstrumentType.PE;
			}
		}
		public PutCallFlag OptionTypeFlag
		{
			get
			{
				return option.InstrumentType.Trim(' ').ToLower() == "ce" ? PutCallFlag.Call : PutCallFlag.Put;
			}
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


		public virtual decimal FuturePremium(decimal bInstrumentPrice, DateTime? endDateTime = null)
		{
			var currentTimeToExp1 = (option.Expiry - option.LastTradeTime).Value.TotalDays / 365;
			var currentTimeToExp = GetExpirationTimeLine(option.LastTradeTime.Value);


			var futureTimeToExp1 = (option.Expiry - endDateTime).Value.TotalDays / 365;
			var futureTimeToExp = GetExpirationTimeLine(endDateTime.Value);

			double iv = DerivativesHelper.BlackScholesImpliedVol(Convert.ToDouble(option.LastPrice), Convert.ToDouble(option.Strike), Convert.ToDouble(option.BaseInstrumentPrice),
				//(option.Expiry - option.LastTradeTime).Value.TotalDays / 365
				currentTimeToExp.Value
				, Convert.ToDouble(RiskFree), 0d, OptionTypeFlag);

			double vega = 0;

					
			double expectedPrice = DerivativesHelper.BlackScholesPriceAndVega(Convert.ToDouble(option.Strike), Convert.ToDouble(bInstrumentPrice),
				futureTimeToExp.Value, iv, Convert.ToDouble(RiskFree), 0d, OptionTypeFlag, out vega);

			//deviation = deviation ?? DefaultDeviation ?? throw new Exception("No Deviation");
			//assetPrice = GetAssetPrice(assetPrice);

			return Convert.ToDecimal(expectedPrice);
		}


		/// <inheritdoc />
		public virtual decimal Premium(DateTime currentTime, decimal assetPrice, DateTime? endDateTime = null, decimal? deviation = null)
		{
			deviation = deviation ?? DefaultDeviation?? throw new Exception("No Deviation");
			assetPrice = GetAssetPrice(assetPrice);

			var currentTimeToExp = GetExpirationTimeLine(option.LastTradeTime.Value);
			var futureTimeToExp = GetExpirationTimeLine(endDateTime == null? option.LastTradeTime.Value : endDateTime.Value);

			return DerivativesHelper.Premium(OptionType, GetStrike(), assetPrice, RiskFree, Dividend, deviation.Value,
				futureTimeToExp.Value, D1(deviation.Value, option.BaseInstrumentPrice, currentTimeToExp.Value));
		}

		public decimal GetEstimatedPrice(DateTime futureDateTime, decimal bInstrumentValue, DateTime currentDateTime)
		{
			decimal futureDays = Convert.ToDecimal((currentDateTime - futureDateTime).TotalDays);

			//Determine current IV based on price
			//Use the IV and determine the price based on period for estimation



			decimal intrinsicValue = option.Strike - bInstrumentValue;

			return intrinsicValue;
		}

	/// <inheritdoc />
	public virtual decimal Delta(DateTime currentTime, decimal assetPrice, decimal ? deviation = null)
		{
			assetPrice = GetAssetPrice(assetPrice);

			var timeToExp = GetExpirationTimeLine(currentTime);

			return DerivativesHelper.Delta(OptionType, assetPrice, D1(deviation ?? DefaultDeviation.Value, assetPrice, timeToExp.Value));
		}

		/// <inheritdoc />
		public virtual decimal? Gamma(DateTime currentTime, decimal assetPrice, decimal? deviation = null)
		{
			deviation = deviation ?? DefaultDeviation;
			assetPrice = GetAssetPrice(assetPrice);

			var timeToExp = GetExpirationTimeLine(currentTime);

			return TryRound(DerivativesHelper.Gamma(assetPrice, deviation.Value, timeToExp.Value, D1(deviation.Value, assetPrice, timeToExp.Value)));
		}

		/// <inheritdoc />
		public virtual decimal? Vega(DateTime currentTime, decimal assetPrice, decimal? deviation = null)
		{
			assetPrice = GetAssetPrice(assetPrice);

			var timeToExp = GetExpirationTimeLine(currentTime);
			
			return TryRound(DerivativesHelper.Vega(assetPrice, timeToExp.Value, D1(deviation ?? DefaultDeviation.Value, assetPrice, timeToExp.Value)));
		}

		/// <inheritdoc />
		public virtual decimal? Theta(DateTime currentTime, decimal assetPrice, decimal ? deviation = null)
		{
			deviation = deviation ?? DefaultDeviation;
			assetPrice = GetAssetPrice(assetPrice);

			var timeToExp = GetExpirationTimeLine(currentTime);

			return TryRound(DerivativesHelper.Theta(OptionType, GetStrike(), assetPrice, RiskFree, deviation.Value, timeToExp.Value, D1(deviation.Value, assetPrice, timeToExp.Value)));
		}

		/// <inheritdoc />
		public virtual decimal? Rho(DateTime currentTime, decimal assetPrice, decimal ? deviation = null)
		{
			deviation = deviation ?? DefaultDeviation;
			assetPrice = GetAssetPrice(assetPrice);

			var timeToExp = GetExpirationTimeLine(currentTime);

			return TryRound(DerivativesHelper.Rho(OptionType, GetStrike(), assetPrice, RiskFree, deviation.Value, timeToExp.Value, D1(deviation.Value, assetPrice, timeToExp.Value)));
		}

		/// <inheritdoc />
		public virtual decimal? ImpliedVolatility(DateTime currentTime, decimal premium)
		{
			//var timeToExp = GetExpirationTimeLine();
			decimal? iv = null;
			try
			{
				iv = TryRound(DerivativesHelper.ImpliedVolatility(premium, deviation => Premium(currentTime, deviation)));
				option.IV = iv;
				option.LastPrice = premium;
			}
			catch (Exception e)
			{
				throw e;
			}
			return iv;
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
			return DerivativesHelper.D1(assetPrice, GetStrike(), RiskFree, Dividend, deviation, timeToExp);
		}


		internal decimal GetStrike()
		{
			return option.Strike;
		}
	}
}