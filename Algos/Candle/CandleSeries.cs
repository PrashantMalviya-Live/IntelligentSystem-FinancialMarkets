using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using GlobalLayer;
using Algorithms.Utilities;
using System.Data;

namespace Algorithms.Candles
{
	/// <summary>
	/// Candles series.
	/// </summary>
	public class CandleSeries //: NotifiableObject, IPersistable
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="CandleSeries"/>.
		/// </summary>
		public CandleSeries()
		{
		}

		public List<Candle> LoadCandles(int numberofCandles, CandleType candleType, DateTime endDateTime, uint instrumentToken, TimeSpan timeFrame)
		{
			DataLogic dl = new DataLogic();
			DataSet dsCandles = dl.LoadCandles(numberofCandles, candleType, endDateTime, instrumentToken, timeFrame);

			List<Candle> candleList = new List<Candle>();

			DataRelation strangle_Token_Relation = dsCandles.Relations.Add("Candle_PriceLevel", new DataColumn[] { dsCandles.Tables[0].Columns["ID"], dsCandles.Tables[0].Columns["CandleType"] },
				new DataColumn[] { dsCandles.Tables[1].Columns["CandleId"], dsCandles.Tables[1].Columns["CandleType"] });

			foreach (DataRow candleRow in dsCandles.Tables[0].Rows)
			{
				Candle candle = NewCandle(candleType);
				candle.LoadCandle(candleRow);
				
				//candle.InstrumentToken = Convert.ToUInt32(candleRow["instrumentToken"]);
				//candle.ClosePrice = Convert.ToDecimal(candleRow["closePrice"]);
				//candle.CloseTime = Convert.ToDateTime(candleRow["CloseTime"]);
				//candle.CloseVolume = Convert.ToDecimal(candleRow["closeVolume"]);
				//candle.DownTicks = Convert.ToInt32(candleRow["downTicks"]);
				//candle.HighPrice = Convert.ToDecimal(candleRow["highPrice"]);

				//candle.HighTime = Convert.ToDateTime(candleRow["highTime"]);
				//candle.HighVolume = Convert.ToDecimal(candleRow["highVolume"]);
				//candle.LowPrice = Convert.ToDecimal(candleRow["lowPrice"]);
				//candle.LowTime = Convert.ToDateTime(candleRow["lowTime"]);
				//candle.LowVolume = Convert.ToDecimal(candleRow["lowVolume"]);

				//List<CandlePriceLevel> candlePriceLevels = new List<CandlePriceLevel>();
				//foreach (DataRow drPriceLevel in candleRow.GetChildRows("Candle_PriceLevel"))
				//{
				//	CandlePriceLevel candlePriceLevel = new CandlePriceLevel(Convert.ToDecimal(drPriceLevel["Price"]));

				//	candlePriceLevel.BuyCount = Convert.ToInt32(drPriceLevel["BuyCount"]);
				//	candlePriceLevel.BuyVolume = Convert.ToInt32(drPriceLevel["BuyVolume"]);
				//	candlePriceLevel.SellCount = Convert.ToInt32(drPriceLevel["SellCount"]);
				//	candlePriceLevel.SellVolume = Convert.ToInt32(drPriceLevel["SellVolume"]);
				//	candlePriceLevel.TotalVolume = Convert.ToInt32(drPriceLevel["TotalVolume"]);
				//	candlePriceLevel.CandleType = (CandleType)Convert.ToInt32(drPriceLevel["CandleType"]);
				//	candlePriceLevels.Add(candlePriceLevel);
				//}

				//candle.PriceLevels = candlePriceLevels;

				//candle.OpenInterest = Convert.ToUInt32(candleRow["openInterest"]);
				//candle.OpenPrice = Convert.ToDecimal(candleRow["openPrice"]);
				//candle.OpenTime = Convert.ToDateTime(candleRow["openTime"]);
				//candle.OpenVolume = Convert.ToDecimal(candleRow["openVolume"]);
				//candle.RelativeVolume = Convert.ToDecimal(candleRow["relativeVolume"]);
				//candle.TotalPrice = Convert.ToDecimal(candleRow["totalPrice"]);
				//candle.TotalTicks = Convert.ToInt32(candleRow["totalTicks"]);
				//candle.TotalVolume = Convert.ToDecimal(candleRow["totalVolume"]);
				//candle.UpTicks = Convert.ToInt32(candleRow["upTicks"]);

				//candle.Arg = Convert.ToUInt32(candleRow["Arg"]);

				candleList.Add(candle);
			}
			return candleList;
		}
		private Candle NewCandle(CandleType candleType)
        {
			Candle candle = null;
			switch (candleType)
			{
				case GlobalLayer.CandleType.Time:
					candle = new TimeFrameCandle();
					break;
				case GlobalLayer.CandleType.Volume:
					candle = new VolumeCandle();
					break;
				case GlobalLayer.CandleType.Money:
					candle = new MoneyCandle();
					break;
			}
			return candle;
		}

		///// <summary>
		///// Initializes a new instance of the <see cref="CandleSeries"/>.
		///// </summary>
		///// <param name="candleType">The candle type.</param>
		///// <param name="instrument">The instrument to be used for candles formation.</param>
		///// <param name="arg">The candle formation parameter. For example, for <see cref="TimeFrameCandle"/> this value is <see cref="TimeFrameCandle.TimeFrame"/>.</param>
		//public CandleSeries(Type candleType, Instrument instrument, object arg)
		//{
		//	if (!candleType.IsCandle())
		//		throw new ArgumentOutOfRangeException(nameof(candleType), candleType, "Wrong Candle");

		//	WorkingTime = instrument.Board?.WorkingTime;
		//}

		private Instrument _instrument;


		public virtual Instrument Security
		{
			get => _instrument;
			set
			{
				_instrument = value;
				//NotifyChanged(nameof(Security));
			}
		}

		private Type _candleType;

		/// <summary>
		/// The candle type.
		/// </summary>
		[Browsable(false)]
		public virtual Type CandleType
		{
			get => _candleType;
			set
			{
				//NotifyChanging(nameof(CandleType));
				_candleType = value;
				//NotifyChanged(nameof(CandleType));
			}
		}

		private object _arg;

		/// <summary>
		/// The candle formation parameter. For example, for <see cref="TimeFrameCandle"/> this value is <see cref="TimeFrameCandle.TimeFrame"/>.
		/// </summary>
		[Browsable(false)]
		public virtual object Arg
		{
			get => _arg;
			set
			{
				//NotifyChanging(nameof(Arg));
				_arg = value;
				//NotifyChanged(nameof(Arg));
			}
		}

		///// <summary>
		///// The time boundary, within which candles for give series shall be translated.
		///// </summary>
		//[Browsable(false)]
		//public WorkingTime WorkingTime { get; set; }

		/// <summary>
		/// To perform the calculation <see cref="Candle.PriceLevels"/>. By default, it is disabled.
		/// </summary>
		public bool IsCalcVolumeProfile { get; set; }

		/// <summary>
		/// The initial date from which you need to get data.
		/// </summary>
		public DateTimeOffset? From { get; set; }

		/// <summary>
		/// The final date by which you need to get data.
		/// </summary>
		public DateTimeOffset? To { get; set; }

		/// <summary>
		/// Allow build candles from smaller timeframe.
		/// </summary>
		/// <remarks>
		/// Available only for <see cref="TimeFrameCandle"/>.
		/// </remarks>
		public bool AllowBuildFromSmallerTimeFrame { get; set; } = true;

		/// <summary>
		/// Use only the regular trading hours for which data will be requested.
		/// </summary>
		public bool IsRegularTradingHours { get; set; }

		/// <summary>
		/// Market-data count.
		/// </summary>
		public long? Count { get; set; }

		/// <summary>
		/// Build mode.
		/// </summary>
		//public MarketDataBuildModes BuildCandlesMode { get; set; }

		/// <summary>
		/// Which market-data type is used as a source value.
		/// </summary>
		//[Display(
		//	ResourceType = typeof(LocalizedStrings),
		//	Name = LocalizedStrings.Str213Key,
		//	Description = LocalizedStrings.CandlesBuildSourceKey,
		//	GroupName = LocalizedStrings.BuildKey,
		//	Order = 21)]
		//[Browsable(false)]
		//[Obsolete("Use BuildCandlesFrom2 property.")]
		//public MarketDataTypes? BuildCandlesFrom
		//{
		//	get => BuildCandlesFrom2?.ToMarketDataType();
		//	set => BuildCandlesFrom2 = value?.ToDataType(null);
		//}

		/// <summary>
		/// Which market-data type is used as a source value.
		/// </summary>
		//public Messages.DataType BuildCandlesFrom2 { get; set; }

		///// <summary>
		///// Extra info for the <see cref="BuildCandlesFrom"/>.
		///// </summary>
		//public Level1Fields? BuildCandlesField { get; set; }

		/// <summary>
		/// Request <see cref="CandleStates.Finished"/> only candles.
		/// </summary>
		public bool IsFinished { get; set; }

		/// <summary>
		/// Try fill gaps.
		/// </summary>
		public bool FillGaps { get; set; }

		/// <inheritdoc />
		//public override string ToString()
		//{
		//	return CandleType?.Name + "_" + Security + "_" + CandleType?.ToCandleMessageType().DataTypeArgToString(Arg);
		//}

		//			/// <summary>
		//			/// Load settings.
		//			/// </summary>
		//			/// <param name="storage">Settings storage.</param>
		//			public void Load(SettingsStorage storage)
		//			{
		//				var secProvider = ServicesRegistry.TrySecurityProvider;
		//				if (secProvider != null)
		//				{
		//					var instrumentId = storage.GetValue<string>(nameof(SecurityId));

		//					if (!instrumentId.IsEmpty())
		//						Security = secProvider.LookupById(instrumentId);
		//				}

		//				CandleType = storage.GetValue(nameof(CandleType), CandleType);
		//				Arg = storage.GetValue(nameof(Arg), Arg);
		//				From = storage.GetValue(nameof(From), From);
		//				To = storage.GetValue(nameof(To), To);
		//				WorkingTime = storage.GetValue(nameof(WorkingTime), WorkingTime);

		//				IsCalcVolumeProfile = storage.GetValue(nameof(IsCalcVolumeProfile), IsCalcVolumeProfile);

		//				BuildCandlesMode = storage.GetValue(nameof(BuildCandlesMode), BuildCandlesMode);

		//				if (storage.ContainsKey(nameof(BuildCandlesFrom2)))
		//					BuildCandlesFrom2 = storage.GetValue<SettingsStorage>(nameof(BuildCandlesFrom2)).Load<Messages.DataType>();
		//#pragma warning disable CS0618 // Type or member is obsolete
		//				else if (storage.ContainsKey(nameof(BuildCandlesFrom)))
		//					BuildCandlesFrom = storage.GetValue(nameof(BuildCandlesFrom), BuildCandlesFrom);
		//#pragma warning restore CS0618 // Type or member is obsolete

		//				BuildCandlesField = storage.GetValue(nameof(BuildCandlesField), BuildCandlesField);
		//				AllowBuildFromSmallerTimeFrame = storage.GetValue(nameof(AllowBuildFromSmallerTimeFrame), AllowBuildFromSmallerTimeFrame);
		//				IsRegularTradingHours = storage.GetValue(nameof(IsRegularTradingHours), IsRegularTradingHours);
		//				Count = storage.GetValue(nameof(Count), Count);
		//				IsFinished = storage.GetValue(nameof(IsFinished), IsFinished);
		//				FillGaps = storage.GetValue(nameof(FillGaps), FillGaps);
		//			}

		///// <summary>
		///// Save settings.
		///// </summary>
		///// <param name="storage">Settings storage.</param>
		//public void Save(SettingsStorage storage)
		//{
		//	if (Security != null)
		//		storage.SetValue(nameof(SecurityId), Security.Id);

		//	if (CandleType != null)
		//		storage.SetValue(nameof(CandleType), CandleType.GetTypeName(false));

		//	if (Arg != null)
		//		storage.SetValue(nameof(Arg), Arg);

		//	storage.SetValue(nameof(From), From);
		//	storage.SetValue(nameof(To), To);

		//	if (WorkingTime != null)
		//		storage.SetValue(nameof(WorkingTime), WorkingTime);

		//	storage.SetValue(nameof(IsCalcVolumeProfile), IsCalcVolumeProfile);

		//	storage.SetValue(nameof(BuildCandlesMode), BuildCandlesMode);

		//	if (BuildCandlesFrom2 != null)
		//		storage.SetValue(nameof(BuildCandlesFrom2), BuildCandlesFrom2.Save());

		//	storage.SetValue(nameof(BuildCandlesField), BuildCandlesField);
		//	storage.SetValue(nameof(AllowBuildFromSmallerTimeFrame), AllowBuildFromSmallerTimeFrame);
		//	storage.SetValue(nameof(IsRegularTradingHours), IsRegularTradingHours);
		//	storage.SetValue(nameof(Count), Count);
		//	storage.SetValue(nameof(IsFinished), IsFinished);
		//	storage.SetValue(nameof(FillGaps), FillGaps);
		//}
	}
}
