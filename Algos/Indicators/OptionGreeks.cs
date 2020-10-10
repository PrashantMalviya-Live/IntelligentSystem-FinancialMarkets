using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.Distributions;
using GlobalLayer;
namespace Algorithms.Indicators
{
    public class OptionGreeks
    {
		private static readonly Normal _normalDistribution = new Normal();
		public static decimal GetIntrinsicValue(Instrument option, decimal baseAssetPrice)
		{
			return ((decimal)(option.InstrumentType == "Call" ? baseAssetPrice - option.Strike : option.Strike - baseAssetPrice));
		}

		/// <summary>
		/// To get the timed option value.
		/// </summary>
		/// <param name="option">Options contract.</param>
		/// <param name="securityProvider">The provider of information about instruments.</param>
		/// <param name="dataProvider">The market data provider.</param>
		/// <returns>The timed value. If it is impossible to get the current market price of the asset then the <see langword="null" /> will be returned.</returns>
		public static decimal GetTimeValue(Instrument option, decimal baseAssetPrice)
		{
			var price = option.LastPrice;
			var intrinsic = GetIntrinsicValue(option, baseAssetPrice);

			return (decimal)(price - intrinsic);
		}

		internal static DateTime GetExpirationTime(DateTime optionExpiry)
		{
			var expDate = optionExpiry;

			if (expDate.TimeOfDay == TimeSpan.Zero)
			{
				expDate += TimeSpan.Parse("15:30:00"); //All options expir by 3:30
			}

			return expDate;
		}

		/// <summary>
		/// To get the option period before expiration.
		/// </summary>
		/// <param name="expirationTime">The option expiration time.</param>
		/// <param name="currentTime">The current time.</param>
		/// <returns>The option period before expiration. If the value is equal to <see langword="null" />, then the value calculation currently is impossible.</returns>
		public static double? GetExpirationTimeLine(DateTime expiry, DateTimeOffset currentTime)
		{
			return GetExpirationTimeLine(GetExpirationTime(expiry), DateTime.Now, TimeSpan.FromDays(365));
		}

		/// <summary>
		/// To get the option period before expiration.
		/// </summary>
		/// <param name="expirationTime">The option expiration time.</param>
		/// <param name="currentTime">The current time.</param>
		/// <param name="timeLine">The length of the total period.</param>
		/// <returns>The option period before expiration. If the value is equal to <see langword="null" />, then the value calculation currently is impossible.</returns>
		public static double? GetExpirationTimeLine(DateTime expirationTime, DateTime currentTime, TimeSpan timeLine)
		{
			var retVal = expirationTime - currentTime;

			if (retVal <= TimeSpan.Zero)
				return null;
			//throw new InvalidOperationException(LocalizedStrings.Str710Params.Put(expirationTime, currentTime));

			return (double)retVal.Ticks / timeLine.Ticks;
		}


		/// <summary>
		/// To check whether the instrument has finished the action.
		/// </summary>
		/// <param name="security">Security.</param>
		/// <param name="exchangeInfoProvider">Exchanges and trading boards provider.</param>
		/// <param name="currentTime">The current time.</param>
		/// <returns><see langword="true" /> if the instrument has finished its action.</returns>
		
		

		/// <summary>
		/// To create the volatility order book from usual order book.
		/// </summary>
		/// <param name="depth">The order book quotes of which will be changed to volatility quotes.</param>
		/// <param name="securityProvider">The provider of information about instruments.</param>
		/// <param name="dataProvider">The market data provider.</param>
		/// <param name="exchangeInfoProvider">Exchanges and trading boards provider.</param>
		/// <param name="currentTime">The current time.</param>
		/// <param name="riskFree">The risk free interest rate.</param>
		/// <param name="dividend">The dividend amount on shares.</param>
		/// <returns>The order book volatility.</returns>
		//public static double ImpliedVolatility(Instrument option, DateTimeOffset currentTime, decimal riskFree = 0, decimal dividend = 0)
		//{
		//	return ImpliedVolatility(new BlackScholes(option) { RiskFree = riskFree, Dividend = dividend }, currentTime);
		//}

		/// <summary>
		/// To create the volatility order book from usual order book.
		/// </summary>
		/// <param name="depth">The order book quotes of which will be changed to volatility quotes.</param>
		/// <param name="model">The model for calculating Greeks values by the Black-Scholes formula.</param>
		/// <param name="currentTime">The current time.</param>
		/// <returns>The order book volatility.</returns>
		//public static double ImpliedVolatility(BlackScholes model, DateTimeOffset currentTime)
		//{
		//	return model.ImpliedVolatility(currentTime);
		//}
		public virtual decimal? BSPremium(Instrument option, decimal baseAssetPrice,
			decimal? deviation = null, decimal riskFree = 0, decimal dividend = 0)
		{
			var timeToExp = GetExpirationTimeLine(option.Expiry.Value, DateTime.Now);

			if (timeToExp == null)
				return null;

			return TryRound(Premium(option.InstrumentType == "Call" ? InstrumentType.CE:InstrumentType.PE, option.Strike, baseAssetPrice, riskFree, dividend,
				deviation.Value, timeToExp.Value, D1(baseAssetPrice, option.Strike, riskFree, dividend, deviation.Value, timeToExp.Value)));
		}

		protected decimal? TryRound(decimal? value)
		{
			if (value != null && RoundDecimals >= 0)
				value = Math.Round(value.Value, RoundDecimals);

			return value;
		}
		private int _roundDecimals = -1;
		public virtual int RoundDecimals
		{
			get => _roundDecimals;
			set
			{
				_roundDecimals = value;
			}
		}

		/// <summary>
		/// To calculate the implied volatility.
		/// </summary>
		/// <param name="premium">The option premium.</param>
		/// <param name="getPremium">To calculate the premium by volatility.</param>
		/// <returns>The implied volatility. If the value is equal to <see langword="null" />, then the value calculation currently is impossible.</returns>
		public static decimal? ImpliedVolatility(decimal premium, Func<decimal, decimal?> getPremium)
		{
			if (getPremium == null)
				throw new ArgumentNullException(nameof(getPremium));

			const decimal min = 0.00001m;

			var deviation = min;

			if (premium <= getPremium(deviation))
				return null;

			var high = 2m;
			var low = 0m;

			while ((high - low) > min)
			{
				deviation = (high + low) / 2;

				if (getPremium(deviation) > premium)
					high = deviation;
				else
					low = deviation;
			}

			return ((high + low) / 2) * 100;
		}

		
		//private const int _dayInYear = 365; // Количество дней в году (расчет временного распада)

		private static double InvertD1(double d1)
		{
			// http://ru.wikipedia.org/wiki/Нормальное_распределение (сигма=1 и мю=0)
			return Math.Exp(-d1 * d1 / 2.0) / Math.Sqrt(2 * Math.PI);
		}

		/// <summary>
		/// To calculate the time exhibitor.
		/// </summary>
		/// <param name="riskFree">The risk free interest rate.</param>
		/// <param name="timeToExp">The option period before the expiration.</param>
		/// <returns>The time exhibitor.</returns>
		public static double ExpRate(decimal riskFree, double timeToExp)
		{
			return riskFree == 0 ? 1 : Math.Exp(-(double)riskFree * timeToExp);
		}

		/// <summary>
		/// To calculate the d1 parameter of the option fulfilment probability estimating.
		/// </summary>
		/// <param name="assetPrice">Underlying asset price.</param>
		/// <param name="strike">The strike price.</param>
		/// <param name="riskFree">The risk free interest rate.</param>
		/// <param name="dividend">The dividend amount on shares.</param>
		/// <param name="deviation">Standard deviation.</param>
		/// <param name="timeToExp">The option period before the expiration.</param>
		/// <returns>The d1 parameter of the option fulfilment probability estimating.</returns>
		public static double D1(decimal assetPrice, decimal strike, decimal riskFree, decimal dividend, decimal deviation, double timeToExp)
		{
			if (deviation < 0)
				throw new Exception("Option Greek!");

			return (Math.Log((double)(assetPrice / strike)) +
				(double)(riskFree - dividend + deviation * deviation / 2.0m) * timeToExp) / ((double)deviation * Math.Sqrt(timeToExp));
		}

		/// <summary>
		/// To calculate the d2 parameter of the option fulfilment probability estimating.
		/// </summary>
		/// <param name="d1">The d1 parameter of the option fulfilment probability estimating.</param>
		/// <param name="deviation">Standard deviation.</param>
		/// <param name="timeToExp">The option period before the expiration.</param>
		/// <returns>The d2 parameter of the option fulfilment probability estimating.</returns>
		public static double D2(double d1, decimal deviation, double timeToExp)
		{
			return d1 - (double)deviation * Math.Sqrt(timeToExp);
		}

		/// <summary>
		/// To calculate the option premium.
		/// </summary>
		/// <param name="optionType">Option type.</param>
		/// <param name="strike">The strike price.</param>
		/// <param name="assetPrice">Underlying asset price.</param>
		/// <param name="riskFree">The risk free interest rate.</param>
		/// <param name="dividend">The dividend amount on shares.</param>
		/// <param name="deviation">Standard deviation.</param>
		/// <param name="timeToExp">The option period before the expiration.</param>
		/// <param name="d1">The d1 parameter of the option fulfilment probability estimating.</param>
		/// <returns>The option premium.</returns>
		public static decimal Premium(InstrumentType optionType, decimal strike, decimal assetPrice, 
			decimal riskFree, decimal dividend, decimal deviation, double timeToExp, double d1)
		{
			var sign = (optionType == InstrumentType.CE) ? 1 : -1;

			var expDiv = ExpRate(dividend, timeToExp);
			var expRate = ExpRate(riskFree, timeToExp);

			return (assetPrice * (decimal)(expDiv * NormalDistr(d1 * sign)) -
					strike * (decimal)(expRate * NormalDistr(D2(d1, deviation, timeToExp) * sign))) * sign;
		}

		/// <summary>
		/// To calculate the option delta.
		/// </summary>
		/// <param name="optionType">Option type.</param>
		/// <param name="assetPrice">Underlying asset price.</param>
		/// <param name="d1">The d1 parameter of the option fulfilment probability estimating.</param>
		/// <returns>Option delta.</returns>
		public static decimal Delta(InstrumentType optionType, decimal assetPrice, double d1)
		{
			var delta = (decimal)NormalDistr(d1);

			if (optionType == InstrumentType.PE)
				delta -= 1;

			return delta;
		}

		/// <summary>
		/// To calculate the option gamma.
		/// </summary>
		/// <param name="assetPrice">Underlying asset price.</param>
		/// <param name="deviation">Standard deviation.</param>
		/// <param name="timeToExp">The option period before the expiration.</param>
		/// <param name="d1">The d1 parameter of the option fulfilment probability estimating.</param>
		/// <returns>Option gamma.</returns>
		public static decimal Gamma(decimal assetPrice, decimal deviation, double timeToExp, double d1)
		{
			if (deviation == 0)
				return 0;
			//throw new ArgumentOutOfRangeException(nameof(deviation), deviation, "Стандартное отклонение имеет недопустимое значение.");

			if (assetPrice == 0)
				return 0;

			return (decimal)InvertD1(d1) / (assetPrice * deviation * Convert.ToDecimal(Math.Sqrt(timeToExp)));
		}

		/// <summary>
		/// To calculate the option vega.
		/// </summary>
		/// <param name="assetPrice">Underlying asset price.</param>
		/// <param name="timeToExp">The option period before the expiration.</param>
		/// <param name="d1">The d1 parameter of the option fulfilment probability estimating.</param>
		/// <returns>Option vega.</returns>
		public static decimal Vega(decimal assetPrice, double timeToExp, double d1)
		{
			return assetPrice * (decimal)(0.01 * InvertD1(d1) * Math.Sqrt(timeToExp));
		}

		/// <summary>
		/// To calculate the option theta.
		/// </summary>
		/// <param name="optionType">Option type.</param>
		/// <param name="strike">The strike price.</param>
		/// <param name="assetPrice">Underlying asset price.</param>
		/// <param name="riskFree">The risk free interest rate.</param>
		/// <param name="deviation">Standard deviation.</param>
		/// <param name="timeToExp">The option period before the expiration.</param>
		/// <param name="d1">The d1 parameter of the option fulfilment probability estimating.</param>
		/// <param name="daysInYear">Days per year.</param>
		/// <returns>Option theta.</returns>
		public static decimal Theta(InstrumentType optionType, decimal strike, decimal assetPrice, decimal riskFree, 
			decimal deviation, double timeToExp, double d1, decimal daysInYear = 365)
		{
			var nd1 = InvertD1(d1);

			var expRate = ExpRate(riskFree, timeToExp);

			var sign = optionType == InstrumentType.CE ? 1 : -1;

			return
				(-(assetPrice * deviation * (decimal)nd1) / (2 * Convert.ToDecimal(Math.Sqrt(timeToExp))) -
				sign * (strike * riskFree * (decimal)(expRate * NormalDistr(sign * D2(d1, deviation, timeToExp))))) / daysInYear;
		}

		/// <summary>
		/// To calculate the option rho.
		/// </summary>
		/// <param name="optionType">Option type.</param>
		/// <param name="strike">The strike price.</param>
		/// <param name="assetPrice">Underlying asset price.</param>
		/// <param name="riskFree">The risk free interest rate.</param>
		/// <param name="deviation">Standard deviation.</param>
		/// <param name="timeToExp">The option period before the expiration.</param>
		/// <param name="d1">The d1 parameter of the option fulfilment probability estimating.</param>
		/// <returns>Option rho.</returns>
		public static decimal Rho(InstrumentType optionType, decimal strike, decimal assetPrice, 
			decimal riskFree, decimal deviation, double timeToExp, double d1)
		{
			var expRate = ExpRate(riskFree, timeToExp);

			var sign = optionType == InstrumentType.CE ? 1 : -1;

			return sign * (0.01m * strike * (decimal)(timeToExp * expRate * NormalDistr(sign * D2(d1, deviation, timeToExp))));
		}

		

		private static double NormalDistr(double x)
		{
			return _normalDistribution.CumulativeDistribution(x);
		}
	}
}
