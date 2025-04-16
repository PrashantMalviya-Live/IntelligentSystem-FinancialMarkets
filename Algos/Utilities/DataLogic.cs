using Algorithm.Algorithm;
using Algorithms.Indicators;
using Algos.Utilities.Views.ModelViews;
//using Algorithms.Utils;
using BrokerConnectWrapper;
using DataAccess;
using GlobalLayer;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Algorithms.Utilities
{
    public class DataLogic
    {
        public bool UpdateUser(User activeUser)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.UpdateUser(activeUser);
        }

        public void InsertHistoricals(Historical historical, int interval = 0)
        {
            MarketDAO marketDAO = new MarketDAO();
            marketDAO.InsertHistoricals(historical, interval);
        }

        public List<Historical> GetHistoricals(uint token, DateTime fromDate, DateTime toDate, int interval = 0)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.GetHistoricals(token, fromDate, toDate, interval);
        }

        public void UpdateAlgoParamaters(int algoInstance, decimal upperLimit = 0, decimal lowerLimit = 0, decimal arg1 = 0, decimal arg2 = 0,
            decimal arg3 = 0, decimal arg4 = 0, decimal arg5 = 0, decimal arg6 = 0, decimal arg7 = 0, decimal arg8 = 0, string arg9 = "")
        {
            MarketDAO marketDAO = new MarketDAO();
            marketDAO.UpdateAlgoParamaters(algoInstance, upperLimit, lowerLimit, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public List<InstrumentListView> GetInstrumentList()
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstrument = marketDAO.GetInstrumentList();

            List<InstrumentListView> instruments = new List<InstrumentListView>();

            foreach (DataRow dr in dsInstrument.Tables[0].Rows)
            {
                InstrumentListView listView = new() { InstrumentToken = Convert.ToUInt32(dr["InstrumentToken"]), TradingSymbol = (string)dr["TradingSymbol"] };
                instruments.Add(listView);
            }
            return instruments;
        }
        public List<IndicatorOperatorView> GetIndicatorList()
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsIndicator = marketDAO.GetIndicatorList();

            List<IndicatorOperatorView> indcatorList = new List<IndicatorOperatorView>();

            DataRelation algo_indicator_properties_relation = dsIndicator.Relations.Add("Indicator_Properties", new DataColumn[] { dsIndicator.Tables[0].Columns["Id"] },
                    new DataColumn[] { dsIndicator.Tables[1].Columns["IndicatorId"] });

            DataRelation algo_indicator_property_values_relation = dsIndicator.Relations.Add("Indicator_Property_Values", [dsIndicator.Tables[1].Columns["Id"]],
                    [dsIndicator.Tables[2].Columns["PropertyId"]], createConstraints: false);

            foreach (DataRow dr in dsIndicator.Tables[0].Rows)
            {
                List<DropDownComponentProperty> dropDownComponentProperties = new List<DropDownComponentProperty>();
                List<TextComponentProperty> textComponentProperties = new List<TextComponentProperty>();

                foreach (var drProperties in dr.GetChildRows(algo_indicator_properties_relation))
                {
                    DropDownComponentProperty dropDownComponentProperty;
                    Dictionary<string, string> indicatorProperties;
                    TextComponentProperty textComponentProperty;

                    var drArry = drProperties.GetChildRows(algo_indicator_property_values_relation);
                    if (drArry.Length > 0)
                    {
                        indicatorProperties = new Dictionary<string, string>();
                        foreach (var drpropertyValues in drArry)
                        {
                            indicatorProperties.Add(drpropertyValues["Id"].ToString(), (string)drpropertyValues["PropertyDisplayValue"]);
                        }

                        dropDownComponentProperty = new()
                        {
                            Id = Convert.ToString(drProperties["Id"]),
                            Name = (string)drProperties["DisplayName"], //Display Name
                            Values = indicatorProperties,
                            SelectedValue = indicatorProperties.ElementAt(0).Key,
                        };

                        dropDownComponentProperties.Add(dropDownComponentProperty);
                    }
                    else
                    {
                        textComponentProperty = new()
                        {
                            Id = Convert.ToString(drProperties["Id"]),
                            Name = (string)drProperties["DisplayName"], //Display Name
                            Value = String.Empty
                        };

                        textComponentProperties.Add(textComponentProperty);
                    }
                }
                IndicatorOperatorView indicatorOperatorView = new()
                {
                    Id = Convert.ToInt32(dr["Id"]),
                    Name = (string)dr["Name"],
                    DropDownComponentProperties = dropDownComponentProperties,
                    TextComponentProperties = textComponentProperties,
                    Type = (IndicatorOperatorType)(byte)dr["Type"],

                };

                indcatorList.Add(indicatorOperatorView);
            }

            IndicatorOperatorView ioView = indcatorList.FirstOrDefault(x => x.Id == Constants.CANDLE_INDICATOR_ID);

            indcatorList.ForEach(x =>
            {
                if (x.Id != Constants.CANDLE_INDICATOR_ID && x.Type == IndicatorOperatorType.Indicator)
                {
                    x.ChildIndicators.Add(Guid.NewGuid().ToString(), ioView);
                }
            });

            return indcatorList;
        }

        //public List<IndcatorListView> GetIndicatorList()
        //{
        //    MarketDAO marketDAO = new MarketDAO();
        //    DataSet dsIndicator = marketDAO.GetIndicatorList();

        //    List<IndcatorListView> indcatorList = new List<IndcatorListView>();

        //    DataRelation algo_indicator_properties_relation = dsIndicator.Relations.Add("Indicator_Properties", new DataColumn[] { dsIndicator.Tables[0].Columns["Id"] },
        //            new DataColumn[] { dsIndicator.Tables[1].Columns["IndicatorId"] });

        //    DataRelation algo_indicator_property_values_relation = dsIndicator.Relations.Add("Indicator_Property_Values", new DataColumn[] { dsIndicator.Tables[1].Columns["Id"] },
        //            new DataColumn[] { dsIndicator.Tables[2].Columns["PropertyId"] });



        //    foreach (DataRow dr in dsIndicator.Tables[0].Rows)
        //    {
        //        Dictionary<string, string> indicatorProperties;//= new Dictionary<string, string>();
        //        Dictionary<string, Dictionary<string, string>> indicatorPropertyvalues = new Dictionary<string, Dictionary<string, string>>();
        //        foreach (var drProperties in dr.GetChildRows(algo_indicator_properties_relation))
        //        {
        //            indicatorProperties = new Dictionary<string, string>();
        //            foreach (var drpropertyValues in drProperties.GetChildRows(algo_indicator_property_values_relation))
        //            {
        //                indicatorProperties.Add(drpropertyValues["Id"].ToString(), (string)drpropertyValues["PropertyDisplayValue"]);
        //            }

        //            indicatorPropertyvalues.Add((string) drProperties["DisplayName"], indicatorProperties);
        //        }



        //        IndcatorListView listView = new()
        //        {
        //            Id = (int)dr["Id"],
        //            Name = (string)dr["Name"],
        //            PropertyNameAndValues = indicatorPropertyvalues
        //        };

        //        indcatorList.Add(listView);
        //    }
        //    return indcatorList;
        //}


        public Dictionary<int, KeyValuePair<string, dynamic>> GetIndicatorProperties(int indicatorId)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsIndicator = marketDAO.GetIndicatorProperties(indicatorId);

            Dictionary<int, KeyValuePair<string, dynamic>> indcatorProperties = new Dictionary<int, KeyValuePair<string, dynamic>>();

            foreach (DataRow dr in dsIndicator.Tables[0].Rows)
            {
                indcatorProperties.Add((int)dr["Id"], new KeyValuePair<string, dynamic>((string)dr["PropertyName"], dr["PropertyValue"]));
            }

            return indcatorProperties;
        }
        public List<CandleTimeFramesView> GetCandleTimeFrameList()
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstrument = marketDAO.GetCandleTimeFrames();

            List<CandleTimeFramesView> ctfs = new List<CandleTimeFramesView>();

            foreach (DataRow dr in dsInstrument.Tables[0].Rows)
            {
                CandleTimeFramesView listView = new()
                {
                    Id = (int)dr["Id"],
                    Name = (string)dr["Name"],
                };
                ctfs.Add(listView);
            }
            return ctfs;
        }

        public List<AlertTriggerView> GetAlertTriggersForUser(string userId)
        {
            MarketDAO marketDAO = new MarketDAO();

            DataSet dsAlertTriggers = marketDAO.GetAlertTriggersforUser(userId);

            Dictionary<int, Dictionary<string, string>> ioPropertyOptions = new Dictionary<int, Dictionary<string, string>>();

            foreach (DataRow drPropertyOptions in dsAlertTriggers.Tables[1].Rows)
            {
                int propertyId = Convert.ToInt32(drPropertyOptions["PropertyId"]);

                if (ioPropertyOptions.ContainsKey(propertyId))
                {
                    ioPropertyOptions[propertyId].Add(drPropertyOptions["Id"].ToString(), drPropertyOptions["PropertyDisplayValue"].ToString());
                }
                else
                {
                    var propertyValues = new Dictionary<string, string>
                    {
                        { drPropertyOptions["Id"].ToString(), drPropertyOptions["PropertyDisplayValue"].ToString() }
                    };

                    ioPropertyOptions.Add(Convert.ToInt32(drPropertyOptions["PropertyId"]), propertyValues);
                }
            }

            List<AlertTriggerView> alertTriggers = new List<AlertTriggerView>();

            foreach (DataRow alertTriggerRow in dsAlertTriggers.Tables[0].Rows)
            {
                AlertTriggerView alertTriggerView = new AlertTriggerView()
                {

                    ID = Convert.ToInt32(alertTriggerRow["Id"]),
                    InstrumentToken = Convert.ToUInt32(alertTriggerRow["InstrumentToken"]),
                    TradingSymbol = Convert.ToString(alertTriggerRow["TradingSymbol"]),
                    SetupDate = Convert.ToDateTime(alertTriggerRow["SetupDate"]),
                    StartDate = Convert.ToDateTime(alertTriggerRow["StartDate"]),
                    EndDate = Convert.ToDateTime(alertTriggerRow["EndDate"]),
                    NumberOfTriggersPerInterval = Convert.ToByte(alertTriggerRow["NumberOfTriggersPerInterval"]),
                    TotalNumberOfTriggers = Convert.ToByte(alertTriggerRow["TotalNumberOfTriggers"]),
                    UserId = Convert.ToString(alertTriggerRow["UserId"]),
                    TriggerFrequency = Convert.ToByte(alertTriggerRow["TriggerFrequency"]),
                    Criteria = JsonConvert.DeserializeObject<Dictionary<string, IndicatorOperator>>((string)alertTriggerRow["AlertCriteria"])
                };
                alertTriggerView.Criteria = alertTriggerView.Unlean(ioPropertyOptions);

                alertTriggers.Add(alertTriggerView);
            }
            return alertTriggers;

        }

        public List<Alert> GetGeneratedAlerts(string userid)
        {
            MarketDAO marketDAO = new MarketDAO();

            DataSet dsAlertTriggers = marketDAO.GetGeneratedAlerts(userid);

            List<Alert> alerts = new List<Alert>();

            foreach (DataRow alertTriggerRow in dsAlertTriggers.Tables[0].Rows)
            {
                alerts.Add(new()
                {
                   ID = Guid.Parse(Convert.ToString(alertTriggerRow["AlertId"])),
                   AlertTriggerID = Convert.ToInt32(alertTriggerRow["AlertTriggerId"]),
                    AlertModes = Convert.ToString(alertTriggerRow["AlertModes"]),
                    InstrumentToken = Convert.ToUInt32(alertTriggerRow["InstrumentToken"]),
                    TradingSymbol = Convert.ToString(alertTriggerRow["TradingSymbol"]),
                    TriggeredDateTime = Convert.ToDateTime(alertTriggerRow["TriggerDateTime"]),
                    Message = Convert.ToString(alertTriggerRow["Message"] == DBNull.Value?"": alertTriggerRow["Message"]),
                    Criteria = Convert.ToString(alertTriggerRow["AlertCriteria"]),
                });
            }
            return alerts;
        }

        public List<AlertTriggerView> GetAlertsbyAlertId(int alertId)
        {
            MarketDAO marketDAO = new MarketDAO();

            DataSet dsAlertTriggers = marketDAO.GetAlertsbyAlertId(alertId);

            List<AlertTriggerView> alertTriggers = new List<AlertTriggerView>();

            foreach (DataRow alertTriggerRow in dsAlertTriggers.Tables[0].Rows)
            {
                alertTriggers.Add(new()
                {
                    ID = Convert.ToInt32(alertTriggerRow["Id"]),
                    InstrumentToken = Convert.ToUInt32(alertTriggerRow["InstrumentToken"]),
                    TradingSymbol = Convert.ToString(alertTriggerRow["TradingSymbol"]),
                    SetupDate = Convert.ToDateTime(alertTriggerRow["SetupDae"]),
                    StartDate = Convert.ToDateTime(alertTriggerRow["StartDate"]),
                    EndDate = Convert.ToDateTime(alertTriggerRow["EndDate"]),
                    NumberOfTriggersPerInterval = Convert.ToByte(alertTriggerRow["NumberOfTriggersPerInterval"]),
                    TotalNumberOfTriggers = Convert.ToByte(alertTriggerRow["TotalNumberOfTriggers"]),
                    UserId = Convert.ToString(alertTriggerRow["UserId"]),
                    TriggerFrequency = Convert.ToByte(alertTriggerRow["TriggerFrequency"]),
                    Criteria = System.Text.Json. JsonSerializer.Deserialize<Dictionary<string, IndicatorOperator>>((string)alertTriggerRow["AlertCriteria"])
                });
            }
            return alertTriggers;
        }
        
        public async Task<decimal> GetUserCreditsAsync (string userId)
        {
            MarketDAO marketDAO = new MarketDAO();
            return await marketDAO.GetUserCreditsAsync(userId);
        }
        public async Task<int> UpdateAlertTriggerAsync(AlertTriggerView alertTrigger)
        {
            MarketDAO marketDAO = new MarketDAO();
            return await marketDAO.UpdateAlertTriggerAsync(alertTrigger.ID,
                alertTrigger.InstrumentToken,
                alertTrigger.TradingSymbol,
                alertTrigger.UserId,
                alertTrigger.SetupDate,
                alertTrigger.StartDate,
                alertTrigger.EndDate,
                alertTrigger.NumberOfTriggersPerInterval,
                alertTrigger.TriggerFrequency,
                alertTrigger.TotalNumberOfTriggers,
                JsonConvert.SerializeObject(alertTrigger.LeanCriteria));
        }
        
        public void UpdateCandleScore(int algoInstance, DateTime candleTime, Decimal candlePrice, int score)
        {
            MarketDAO marketDAO = new MarketDAO();
            marketDAO.UpdateCandleScore(algoInstance, candleTime, candlePrice, score);
        }
        public void UpdateArg8(int algoInstance, decimal arg8)
        {
            MarketDAO marketDAO = new MarketDAO();
            marketDAO.UpdateArg8(algoInstance, arg8);
        }

        
        public OHLC GetPreviousDayRange(uint token, DateTime dateTime)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.GetPreviousDayRange(token, dateTime);
        }
        public DateTime GetPreviousTradingDate(DateTime tradeDate, int numberOfDaysPrior = 1, uint token = 256265) //NIFTY
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.GetPreviousTradingDate(tradeDate, numberOfDaysPrior, token);
        }
        public DateTime GetPreviousWeeklyExpiry(DateTime tradeDate, int numberofWeeksPrior = 1)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.GetPreviousWeeklyExpiry(tradeDate, numberofWeeksPrior);
        }
        
        public DateTime GetCurrentMonthlyExpiry(DateTime tradeDate, uint binstrument)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.GetCurrentMonthlyExpiry(tradeDate, binstrument);
        }
        public DateTime GetCurrentWeeklyExpiry(DateTime tradeDate, uint binstrument)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.GetCurrentWeeklyExpiry(tradeDate, binstrument);
        }

        public List<string> GetActiveTradingDays(DateTime fromDate, DateTime toDate)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.GetActiveTradingDays(fromDate, toDate);
        }
        public OHLC GetPriceRange(uint token, DateTime _startDateTime, DateTime _endDateTime)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.GetPriceRange(token, _startDateTime, _endDateTime);
        }
        public User GetActiveUser(int brokerId, string userid)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsUser = marketDAO.GetActiveUser(brokerId, userid);

            return new User(dsUser.Tables[0]);
        }
        public AspNetUser GetActiveApplicationUser(string userid)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsUser = marketDAO.GetActiveApplicationUser(userid);

            return new AspNetUser(dsUser.Tables[0]);
        }
        
        public void SaveCandle(Candle candle)
        {
            MarketDAO marketDAO = new MarketDAO();

            CandlePriceLevel maxPriceLevel = candle.MaxPriceLevel;
            CandlePriceLevel minPriceLevel = candle.MinPriceLevel;
            IEnumerable<CandlePriceLevel> priceLevels = candle.PriceLevels;

            int candleId = marketDAO.SaveCandle(candle.Arg, candle.ClosePrice, candle.CloseTime, candle.CloseVolume, candle.DownTicks,
                candle.HighPrice, candle.HighTime, candle.HighVolume, candle.InstrumentToken,
                candle.LowPrice, candle.LowTime, candle.LowVolume,
                maxPriceLevel.BuyCount, maxPriceLevel.BuyVolume, maxPriceLevel.Money, maxPriceLevel.Price,
                maxPriceLevel.SellCount, maxPriceLevel.SellVolume, maxPriceLevel.TotalVolume,
                minPriceLevel.BuyCount, minPriceLevel.BuyVolume, minPriceLevel.Money, minPriceLevel.Price,
                minPriceLevel.SellCount, minPriceLevel.SellVolume, minPriceLevel.TotalVolume,
                candle.OpenInterest, candle.OpenPrice, candle.OpenTime, candle.OpenVolume,
                candle.RelativeVolume, candle.State, candle.TotalPrice, candle.TotalTicks, candle.TotalVolume, candle.UpTicks, candle.CandleType);

            foreach (CandlePriceLevel candlePriceLevel in candle.PriceLevels)
            {
                marketDAO.SaveCandlePriceLevels(candleId, candlePriceLevel.BuyCount, candlePriceLevel.BuyVolume, candlePriceLevel.Money, candlePriceLevel.Price,
                candlePriceLevel.SellCount, candlePriceLevel.SellVolume, candlePriceLevel.TotalVolume);
            }
        }
        public DataSet LoadAlgoInputs(AlgoIndex algoindex, DateTime fromDate, DateTime toDate)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.LoadAlgoInputs(algoindex, fromDate, toDate);
        }

        public void LoadTokens()
        {
            ZConnect.Login();
            List<Instrument> instruments = ZObjects.kite.GetInstruments();

            MarketDAO marketDAO = new MarketDAO();
            marketDAO.StoreInstrumentList(instruments);
        }

        public List<Order> GetOrders(AlgoIndex algoIndex = 0, int orderid = 0)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet ds = marketDAO.GetOrders(algoIndex, orderid);

            List<Order> orders = new List<Order>();
            foreach(DataRow dr in ds.Tables[0].Rows)
            {
                Order order = new Order
                {
                    InstrumentToken = Convert.ToUInt32(dr["InstrumentToken"]),
                    Tradingsymbol = (string) dr["TradingSymbol"],
                    TransactionType = (string) dr["TransactionType"],
                    AveragePrice = Convert.ToDecimal(dr["AveragePrice"]),
                    Quantity = (int) dr["Quantity"],
                    TriggerPrice = Convert.ToDecimal(dr["TriggerPrice"]),
                    Status = (string) dr["Status"],
                    StatusMessage = Convert.ToString(dr["StatusMessage"]),
                    OrderType = Convert.ToString(dr["OrderType"]),
                    OrderTimestamp = Convert.ToDateTime(dr["OrderTimeStamp"]),
                    AlgoIndex = Convert.ToInt32(dr["AlgoIndex"]),
                    AlgoInstance = Convert.ToInt32(dr["AlgoInstance"])
                };
                orders.Add(order);
            }
            return orders;
        }
        public DataSet GetDailyOHLC(IEnumerable<uint> tokens, DateTime date)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.GetDailyOHLC(tokens, date);
        }
        public Instrument GetOTMInstrument(DateTime? expiry, uint bToken, decimal bInstrumentPrice, string instrumentType)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstrument = marketDAO.GetOTMInstrument(expiry, bToken, bInstrumentPrice, instrumentType);
            DataRow data = dsInstrument.Tables[0].Rows[0];

            Instrument instrument = new Instrument();
            instrument.InstrumentToken = Convert.ToUInt32(data["Instrument_Token"]);
            instrument.TradingSymbol = Convert.ToString(data["TradingSymbol"]);
            instrument.Strike = Convert.ToDecimal(data["Strike"]);
            if (data["Expiry"] != DBNull.Value)
            {
                instrument.Expiry = Convert.ToDateTime(data["Expiry"]);
            }
            instrument.InstrumentType = Convert.ToString(data["Instrument_Type"]);
            instrument.LotSize = Convert.ToUInt32(data["Lot_Size"]);
            instrument.Exchange = Convert.ToString(data["Exchange"]);
            instrument.Segment = Convert.ToString(data["Segment"]);
            if (data["BToken"] != DBNull.Value)
            {
                instrument.BaseInstrumentToken = Convert.ToUInt32(data["BToken"]);
            }
            if (data["KToken"] != DBNull.Value)
            {
                instrument.KToken = Convert.ToUInt32(data["KToken"]);
            }
            return instrument;
        }
        public Instrument GetInstrument(DateTime? expiry, uint bInstrumentToken, decimal strike, string instrumentType)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstrument = marketDAO.GetInstrument(expiry, bInstrumentToken, strike, instrumentType);
            DataRow data = dsInstrument.Tables[0].Rows[0];

            Instrument instrument = new Instrument();
            instrument.InstrumentToken = Convert.ToUInt32(data["Instrument_Token"]);
            instrument.TradingSymbol = Convert.ToString(data["TradingSymbol"]);
            instrument.Strike = Convert.ToDecimal(data["Strike"]);
            if (data["Expiry"] != DBNull.Value)
            {
                instrument.Expiry = Convert.ToDateTime(data["Expiry"]);
            }
            instrument.InstrumentType = Convert.ToString(data["Instrument_Type"]);
            instrument.LotSize = Convert.ToUInt32(data["Lot_Size"]);
            instrument.Exchange = Convert.ToString(data["Exchange"]);
            instrument.Segment = Convert.ToString(data["Segment"]);
            if (data["BToken"] != DBNull.Value)
            {
                instrument.BaseInstrumentToken = Convert.ToUInt32(data["BToken"]);
            }
            if (data["KToken"] != DBNull.Value)
            {
                instrument.KToken = Convert.ToUInt32(data["KToken"]);
            }
            return instrument;
        }

        public Instrument GetInstrument(uint instrumentToken, DateTime expiry)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstrument = marketDAO.GetInstrument(instrumentToken, expiry);
            DataRow data = dsInstrument.Tables[0].Rows[0];

            Instrument instrument = new Instrument();
            instrument.InstrumentToken = Convert.ToUInt32(data["Instrument_Token"]);
            instrument.TradingSymbol = Convert.ToString(data["TradingSymbol"]);
            instrument.Strike = Convert.ToDecimal(data["Strike"]);
            if (data["Expiry"] != DBNull.Value)
            {
                instrument.Expiry = Convert.ToDateTime(data["Expiry"]);
            }
            instrument.InstrumentType = Convert.ToString(data["Instrument_Type"]);
            instrument.LotSize = Convert.ToUInt32(data["Lot_Size"]);
            instrument.Exchange = Convert.ToString(data["Exchange"]);
            instrument.Segment = Convert.ToString(data["Segment"]);
            if (data["BToken"] != DBNull.Value)
            {
                instrument.BaseInstrumentToken = Convert.ToUInt32(data["BToken"]);
            }
            if (data["KToken"] != DBNull.Value)
            {
                instrument.KToken = Convert.ToUInt32(data["KToken"]);
            }
            return instrument;
        }

        public Instrument GetInstrument(string tradingSymbol)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstrument = marketDAO.GetInstrument(tradingSymbol);
            DataRow data = dsInstrument.Tables[0].Rows[0];

            Instrument instrument = new Instrument();
            instrument.InstrumentToken = Convert.ToUInt32(data["Instrument_Token"]);
            instrument.TradingSymbol = Convert.ToString(data["TradingSymbol"]);
            instrument.Strike = Convert.ToDecimal(data["Strike"]);
            if (data["Expiry"] != DBNull.Value)
            {
                instrument.Expiry = Convert.ToDateTime(data["Expiry"]);
            }
            instrument.InstrumentType = Convert.ToString(data["Instrument_Type"]);
            instrument.LotSize = Convert.ToUInt32(data["Lot_Size"]);
            instrument.Exchange = Convert.ToString(data["Exchange"]);
            instrument.Segment = Convert.ToString(data["Segment"]);
            if (data["BToken"] != DBNull.Value)
            {
                instrument.BaseInstrumentToken = Convert.ToUInt32(data["BToken"]);
            }
            return instrument;
        }

        public Dictionary<uint, Instrument> LoadInstruments(AlgoIndex algoIndex, DateTime? expiry, uint baseInstrumentToken, decimal baseInstrumentPrice)
            //,decimal FromStrikePrice = 0, decimal ToStrikePrice = 0, InstrumentType instrumentType = InstrumentType.ALL)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.LoadInstruments(algoIndex, expiry, baseInstrumentToken, baseInstrumentPrice);
                  //,FromStrikePrice = 0, ToStrikePrice = 0, instrumentType);


            Dictionary<uint, Instrument> activeInstruments = new Dictionary<uint, Instrument>();

            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Instrument instrument = new Instrument();
                instrument.InstrumentToken = Convert.ToUInt32(data["Instrument_Token"]);
                instrument.TradingSymbol = Convert.ToString(data["TradingSymbol"]);
                instrument.Strike = Convert.ToDecimal(data["Strike"]);
                if (data["Expiry"] != DBNull.Value)
                {
                    instrument.Expiry = Convert.ToDateTime(data["Expiry"]);
                }
                instrument.InstrumentType = Convert.ToString(data["Instrument_Type"]);
                instrument.LotSize = Convert.ToUInt32(data["Lot_Size"]);
                instrument.Exchange = Convert.ToString(data["Exchange"]);
                instrument.Segment = Convert.ToString(data["Segment"]);
                if (data["BToken"] != DBNull.Value)
                {
                    instrument.BaseInstrumentToken = Convert.ToUInt32(data["BToken"]);
                }


                //activeInstruments.Add(String.Format("{0}{1}", instrument.InstrumentType, instrument.Strike) , instrument);
                activeInstruments.Add(instrument.InstrumentToken, instrument);
            }
            return activeInstruments;
        }
        public List<Option> LoadSpreadOptions(DateTime? nearExpiry, DateTime? farExpiry,
            uint baseInstrumentToken, string instrumentType1, string instrumentType2, decimal strike1, decimal strike2)
        {
            string strikeList = string.Format("{0},{1}", strike1, strike2);

            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.LoadSpreadOptions(nearExpiry, farExpiry, baseInstrumentToken, strikeList);

            List<Option> options = new List<Option>();
            //SortedList<decimal, Option[]> optionList = new SortedList<decimal, Option[]>();

            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Option instrument = new Option();
                instrument.InstrumentToken = Convert.ToUInt32(data["Instrument_Token"]);
                instrument.TradingSymbol = Convert.ToString(data["TradingSymbol"]);
                instrument.Strike = Convert.ToDecimal(data["Strike"]);
                if (data["Expiry"] != DBNull.Value)
                {
                    instrument.Expiry = Convert.ToDateTime(data["Expiry"]) + new TimeSpan(15, 30, 0);
                }
                instrument.InstrumentType = Convert.ToString(data["Instrument_Type"]);
                instrument.LotSize = Convert.ToUInt32(data["Lot_Size"]);
                instrument.Exchange = Convert.ToString(data["Exchange"]);
                instrument.Segment = Convert.ToString(data["Segment"]);
                if (data["BToken"] != DBNull.Value)
                {
                    instrument.BaseInstrumentToken = Convert.ToUInt32(data["BToken"]);
                }

                options.Add(instrument);
                //if (instrument.InstrumentType.Trim(' ').ToLower() == instrumentType1.Trim(' ').ToLower() && instrument.Strike == strike1)
                //{
                //    options.Add(instrument);
                //}
                //else if (instrument.InstrumentType.Trim(' ').ToLower() == instrumentType2.Trim(' ').ToLower() && instrument.Strike == strike2)
                //{
                //    options.Add(instrument);
                //}

                //activeInstruments.Add(String.Format("{0}{1}", instrument.InstrumentType, instrument.Strike) , instrument);
                //options.Add(instrument);
            }
            return options;
        }

        public SortedList<decimal, Option[]> LoadSpreadOptions(DateTime? nearExpiry, DateTime? farExpiry,
            uint baseInstrumentToken, string strikeList, out Dictionary<uint, Option> optionDictionary)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.LoadSpreadOptions(nearExpiry, farExpiry, baseInstrumentToken, strikeList);

            List<Option> options = new List<Option>();
            SortedList<decimal, Option[]> optionList = new SortedList<decimal, Option[]>();
            optionDictionary = new Dictionary<uint, Option>();
            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Option instrument = new Option();
                instrument.InstrumentToken = Convert.ToUInt32(data["Instrument_Token"]);
                instrument.TradingSymbol = Convert.ToString(data["TradingSymbol"]);
                instrument.Strike = Convert.ToDecimal(data["Strike"]);
                if (data["Expiry"] != DBNull.Value)
                {
                    instrument.Expiry = Convert.ToDateTime(data["Expiry"]) + new TimeSpan(15,30,0);
                }
                instrument.InstrumentType = Convert.ToString(data["Instrument_Type"]);
                instrument.LotSize = Convert.ToUInt32(data["Lot_Size"]);
                instrument.Exchange = Convert.ToString(data["Exchange"]);
                instrument.Segment = Convert.ToString(data["Segment"]);
                if (data["BToken"] != DBNull.Value)
                {
                    instrument.BaseInstrumentToken = Convert.ToUInt32(data["BToken"]);
                }
                if (instrument.InstrumentType.Trim(' ').ToLower() == "pe")
                {
                    if (optionList.ContainsKey(instrument.Strike))
                    {
                        optionList[instrument.Strike] = new Option[2] { optionList[instrument.Strike][0], instrument };
                    }
                    else
                    {
                        optionList.Add(instrument.Strike, new Option[2] { instrument, null });
                    }
                    optionDictionary.Add(instrument.InstrumentToken, instrument);
                }
                //activeInstruments.Add(String.Format("{0}{1}", instrument.InstrumentType, instrument.Strike) , instrument);
                //options.Add(instrument);
            }
            return optionList;
        }
        public SortedList<decimal, Option[][]> LoadOptionsForSpread(DateTime? nearExpiry, DateTime? farExpiry,
           uint baseInstrumentToken, string strikeList, out Dictionary<uint, Option> optionDictionary)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.LoadSpreadOptions(nearExpiry, farExpiry, baseInstrumentToken, strikeList);

            List<Option> options = new List<Option>();
            SortedList<decimal, Option[][]> optionList = new SortedList<decimal, Option[][]>();
            optionDictionary = new Dictionary<uint, Option>();
            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Option instrument = new Option();
                instrument.InstrumentToken = Convert.ToUInt32(data["Instrument_Token"]);
                instrument.TradingSymbol = Convert.ToString(data["TradingSymbol"]);
                instrument.Strike = Convert.ToDecimal(data["Strike"]);
                if (data["Expiry"] != DBNull.Value)
                {
                    instrument.Expiry = Convert.ToDateTime(data["Expiry"]) + new TimeSpan(15, 30, 0);
                }
                instrument.InstrumentType = Convert.ToString(data["Instrument_Type"]);
                instrument.LotSize = Convert.ToUInt32(data["Lot_Size"]);
                instrument.Exchange = Convert.ToString(data["Exchange"]);
                instrument.Segment = Convert.ToString(data["Segment"]);
                if (data["BToken"] != DBNull.Value)
                {
                    instrument.BaseInstrumentToken = Convert.ToUInt32(data["BToken"]);
                }
                //if (instrument.InstrumentType.Trim(' ').ToLower() == "pe")
                //{
                //    if (optionList.ContainsKey(instrument.Strike))
                //    {
                //        optionList[instrument.Strike] = new Option[2][] { new Option[] { optionList[instrument.Strike][0][0], instrument }, new Option[] { optionList[instrument.Strike][1][0], instrument } };
                //    }
                //    else
                //    {
                //        optionList.Add(instrument.Strike, new Option[2][] { new Option[] { instrument, null }, new Option[] { instrument, null } });
                //    }
                //    optionDictionary.Add(instrument.InstrumentToken, instrument);
                //}

                //InstrumentType.PE = 1

                if (optionList.ContainsKey(instrument.Strike))
                {
                    if (instrument.InstrumentType.Trim(' ').ToLower() == "pe")
                    {
                        if(optionList[instrument.Strike][1][0] == null)
                        {
                            optionList[instrument.Strike] = new Option[2][] { new Option[] { optionList[instrument.Strike][0][0], optionList[instrument.Strike][0][1] }, new Option[] { instrument, null } };
                        }
                        else
                        {
                            optionList[instrument.Strike] = new Option[2][] { new Option[] { optionList[instrument.Strike][0][0], optionList[instrument.Strike][0][1] }, new Option[] { optionList[instrument.Strike][1][0], instrument } };
                        }
                    }
                    if (instrument.InstrumentType.Trim(' ').ToLower() == "ce")
                    {
                        if (optionList[instrument.Strike][0][0] == null)
                        {
                            optionList[instrument.Strike] = new Option[2][] { new Option[] { instrument, null }, new Option[] { optionList[instrument.Strike][1][0], optionList[instrument.Strike][1][1] } };
                        }
                        else
                        {
                            optionList[instrument.Strike] = new Option[2][] { new Option[] { optionList[instrument.Strike][0][0], instrument }, new Option[] { optionList[instrument.Strike][1][0], optionList[instrument.Strike][1][1] } };
                        }
                    }

                    optionList[instrument.Strike] = new Option[2][] { new Option[] { optionList[instrument.Strike][0][0], optionList[instrument.Strike][0][1] }, new Option[] { optionList[instrument.Strike][1][0], instrument } };
                }
                else
                {
                    if (instrument.InstrumentType.Trim(' ').ToLower() == "pe")
                    {
                        optionList.Add(instrument.Strike, new Option[2][] { new Option[] { null, null }, new Option[] { instrument, null } });
                    }
                    else
                    {
                        optionList.Add(instrument.Strike, new Option[2][] { new Option[] { instrument, null }, new Option[] { null, null } });
                    }
                     
                }
                if (!optionDictionary.ContainsKey(instrument.InstrumentToken))
                {
                    optionDictionary.Add(instrument.InstrumentToken, instrument);
                }

                //activeInstruments.Add(String.Format("{0}{1}", instrument.InstrumentType, instrument.Strike) , instrument);
                //options.Add(instrument);
            }
            return optionList;
        }
        public List<Instrument> LoadIndexStocks(uint indexToken)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.LoadIndexStocks(indexToken);
            List<Instrument> allStocks = new List<Instrument>();
            UInt32 kotakToken = 0;
            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Instrument instrument = new Instrument();
                instrument.InstrumentToken = Convert.ToUInt32(data["InstrumentToken"]);
                instrument.TradingSymbol = Convert.ToString(data["TradingSymbol"]);
                
                //Just for cehcking with stocks order
                instrument.LotSize = 10;// Convert.ToUInt32(data["LotSize"]);

                //instrument.Strike = Convert.ToDecimal(data["Strike"]);
                if (data["KTokens"] != DBNull.Value)
                {
                    instrument.KToken = Convert.ToUInt32(data["KTokens"]);
                }
                instrument.InstrumentType = Convert.ToString(data["InstrumentType"]);
                instrument.BaseInstrumentToken = indexToken;
                allStocks.Add(instrument);

#if market
                //dsInstruments.Tables[1].PrimaryKey = new DataColumn[] { dsInstruments.Tables[1].Columns["Instrument_Token"] };
                //kotakToken = Convert.ToUInt32(dsInstruments.Tables[1].Rows.Find(instrument.InstrumentToken)[1]);
#elif local
                //kotakToken = 0;
#endif
                //instrument.KToken = kotakToken;
            }
            return allStocks;
        }

        public List<Instrument> LoadOptions(DateTime? expiry, uint baseInstrumentToken,
            decimal baseInstrumentPrice, decimal maxDistanceFromBInstrument, out SortedList<decimal, Instrument> ceList,
            out SortedList<decimal, Instrument> peList, out Dictionary<uint, uint> mappedTokens)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.LoadOptions(expiry, baseInstrumentToken, baseInstrumentPrice,
                maxDistanceFromBInstrument);
            mappedTokens = new Dictionary<uint, uint>();
            ceList = new SortedList<decimal, Instrument>();
            peList = new SortedList<decimal, Instrument>();
            uint kotakToken = 0;
            List<Instrument> allOptions = new List<Instrument>();
            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Instrument instrument = new Instrument();
                instrument.InstrumentToken = Convert.ToUInt32(data["Instrument_Token"]);
                instrument.TradingSymbol = Convert.ToString(data["TradingSymbol"]);
                instrument.Strike = Convert.ToDecimal(data["Strike"]);
                if (data["Expiry"] != DBNull.Value)
                {
                    instrument.Expiry = Convert.ToDateTime(data["Expiry"]);
                }
                instrument.InstrumentType = Convert.ToString(data["Instrument_Type"]);
                instrument.LotSize = Convert.ToUInt32(data["Lot_Size"]);
                instrument.Exchange = Convert.ToString(data["Exchange"]);
                instrument.Segment = Convert.ToString(data["Segment"]);
                if (data["BToken"] != DBNull.Value)
                {
                    instrument.BaseInstrumentToken = Convert.ToUInt32(data["BToken"]);
                }

                if (instrument.InstrumentType.Trim(' ').ToLower() == "ce")
                {
                    ceList.TryAdd(instrument.Strike, instrument);
                }
                else if (instrument.InstrumentType.Trim(' ').ToLower() == "pe")
                {
                    peList.TryAdd(instrument.Strike, instrument);
                }
                allOptions.Add(instrument);

#if market
                dsInstruments.Tables[1].PrimaryKey = new DataColumn[] { dsInstruments.Tables[1].Columns["Instrument_Token"] };
                kotakToken = Convert.ToUInt32(dsInstruments.Tables[1].Rows.Find(instrument.InstrumentToken)[1]);
#elif local
                kotakToken = 0;
#endif

                //kotakToken = Convert.ToUInt32(dsInstruments.Tables[1].Rows.Find(instrument.InstrumentToken)[1]);
                //kotakToken = 0;
                instrument.KToken = kotakToken;
                mappedTokens.TryAdd(instrument.InstrumentToken, kotakToken);
            }
            return allOptions;
        }
        public List<Instrument> LoadCloseByOptions(DateTime? expiry, uint baseInstrumentToken, 
            decimal baseInstrumentPrice, decimal maxDistanceFromBInstrument, out SortedList<decimal, Instrument> ceList, 
            out SortedList<decimal, Instrument> peList, out Dictionary<uint, uint> mappedTokens)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.LoadCloseByOptions(expiry, baseInstrumentToken, baseInstrumentPrice,
                maxDistanceFromBInstrument);
            mappedTokens = new Dictionary<uint, uint>();
            ceList = new SortedList<decimal, Instrument>();
            peList = new SortedList<decimal, Instrument>();
            uint kotakToken = 0;
            List<Instrument> allOptions = new List<Instrument>();
            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Instrument instrument = new Instrument();
                instrument.InstrumentToken = Convert.ToUInt32(data["Instrument_Token"]);
                instrument.TradingSymbol = Convert.ToString(data["TradingSymbol"]);
                instrument.Strike = Convert.ToDecimal(data["Strike"]);
                if (data["Expiry"] != DBNull.Value)
                {
                    instrument.Expiry = Convert.ToDateTime(data["Expiry"]);
                }
                instrument.InstrumentType = Convert.ToString(data["Instrument_Type"]);
                instrument.LotSize = Convert.ToUInt32(data["Lot_Size"]);
                instrument.Exchange = Convert.ToString(data["Exchange"]);
                instrument.Segment = Convert.ToString(data["Segment"]);
                if (data["BToken"] != DBNull.Value)
                {
                    instrument.BaseInstrumentToken = Convert.ToUInt32(data["BToken"]);
                }

                if (instrument.InstrumentType.Trim(' ').ToLower() == "ce")
                {
                    ceList.TryAdd(instrument.Strike, instrument);
                }
                else if (instrument.InstrumentType.Trim(' ').ToLower() == "pe")
                {
                    peList.TryAdd(instrument.Strike, instrument);
                }
                allOptions.Add(instrument);

#if market
                dsInstruments.Tables[1].PrimaryKey = new DataColumn[] { dsInstruments.Tables[1].Columns["Instrument_Token"] };
                kotakToken = 0; // Convert.ToUInt32(dsInstruments.Tables[1].Rows.Find(instrument.InstrumentToken)[1]);
                instrument.KToken = kotakToken;
#elif local
                kotakToken = 0;
#endif

                //kotakToken = Convert.ToUInt32(dsInstruments.Tables[1].Rows.Find(instrument.InstrumentToken)[1]);
                //kotakToken = 0;
                mappedTokens.TryAdd(instrument.InstrumentToken, kotakToken);
            }
            return allOptions;
        }

        public SortedList<decimal, Instrument>[] LoadCloseByOptions(DateTime? expiry,
        uint baseInstrumentToken, decimal baseInstrumentPrice, decimal maxDistanceFromBInstrument, out Dictionary<uint, uint> mappedTokens)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.LoadCloseByOptions(expiry, baseInstrumentToken, baseInstrumentPrice,
                maxDistanceFromBInstrument);

            List<Instrument> options = new List<Instrument>();
            SortedList<decimal, Instrument> ceList = new SortedList<decimal, Instrument>();
            SortedList<decimal, Instrument> peList = new SortedList<decimal, Instrument>();
            mappedTokens = new Dictionary<uint, uint>();
            uint kotakToken = 0;
            dsInstruments.Tables[1].PrimaryKey = new DataColumn[] { dsInstruments.Tables[1].Columns[0] };
            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Instrument instrument = new Instrument();
                instrument.InstrumentToken = Convert.ToUInt32(data["Instrument_Token"]);
                instrument.TradingSymbol = Convert.ToString(data["TradingSymbol"]);
                instrument.Strike = Convert.ToDecimal(data["Strike"]);
                if (data["Expiry"] != DBNull.Value)
                {
                    instrument.Expiry = Convert.ToDateTime(data["Expiry"]);
                    instrument.Expiry += new TimeSpan(15, 30, 0);
                }
                instrument.InstrumentType = Convert.ToString(data["Instrument_Type"]);
                instrument.LotSize = Convert.ToUInt32(data["Lot_Size"]);
                instrument.Exchange = Convert.ToString(data["Exchange"]);
                instrument.Segment = Convert.ToString(data["Segment"]);
                if (data["BToken"] != DBNull.Value)
                {
                    instrument.BaseInstrumentToken = Convert.ToUInt32(data["BToken"]);
                }


                if (instrument.InstrumentType.Trim(' ').ToLower() == "ce")
                {
                    ceList.Add(instrument.Strike, instrument);
                }
                else if (instrument.InstrumentType.Trim(' ').ToLower() == "pe")
                {
                    peList.Add(instrument.Strike, instrument);
                }
#if market
                dsInstruments.Tables[1].PrimaryKey = new DataColumn[] { dsInstruments.Tables[1].Columns["Instrument_Token"] };
                kotakToken = 0;// Convert.ToUInt32(dsInstruments.Tables[1].Rows.Find(instrument.InstrumentToken)[1]);

#elif local
                kotakToken = 0;
#endif
                instrument.KToken = kotakToken;
                if (!mappedTokens.ContainsKey(instrument.InstrumentToken))
                {
                    mappedTokens.Add(instrument.InstrumentToken, kotakToken);
                }

                //activeInstruments.Add(String.Format("{0}{1}", instrument.InstrumentType, instrument.Strike) , instrument);
                //options.Add(instrument);
            }
            //return options;
            SortedList<decimal, Instrument>[] optionUniverse = new SortedList<decimal, Instrument>[2];
            optionUniverse[(int)InstrumentType.CE] = ceList;
            optionUniverse[(int)InstrumentType.PE] = peList;

            return optionUniverse;
        }
        public Dictionary<uint, Instrument> LoadOptionsChain(DateTime? expiry,
       uint baseInstrumentToken, decimal baseInstrumentPrice, decimal maxDistanceFromBInstrument, 
       out Dictionary<uint, uint> mappedTokens, out SortedDictionary<decimal, Instrument> ceList, 
       out SortedDictionary<decimal, Instrument> peList)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.LoadCloseByOptions(expiry, baseInstrumentToken, baseInstrumentPrice,
                maxDistanceFromBInstrument);
            ceList = new SortedDictionary<decimal, Instrument>();
            peList = new SortedDictionary<decimal, Instrument>();
            List<Instrument> options = new List<Instrument>();
            Dictionary<uint, Instrument> allOptions = new Dictionary<uint, Instrument>();
            mappedTokens = new Dictionary<uint, uint>();
            uint kotakToken = 0;
            dsInstruments.Tables[1].PrimaryKey = new DataColumn[] { dsInstruments.Tables[1].Columns[0] };
            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Instrument instrument = new Instrument();
                instrument.InstrumentToken = Convert.ToUInt32(data["Instrument_Token"]);
                instrument.TradingSymbol = Convert.ToString(data["TradingSymbol"]);
                instrument.Strike = Convert.ToDecimal(data["Strike"]);
                if (data["Expiry"] != DBNull.Value)
                {
                    instrument.Expiry = Convert.ToDateTime(data["Expiry"]);
                    instrument.Expiry += new TimeSpan(15, 30, 0);
                }
                instrument.InstrumentType = Convert.ToString(data["Instrument_Type"]);
                instrument.LotSize = Convert.ToUInt32(data["Lot_Size"]);
                instrument.Exchange = Convert.ToString(data["Exchange"]);
                instrument.Segment = Convert.ToString(data["Segment"]);
                if (data["BToken"] != DBNull.Value)
                {
                    instrument.BaseInstrumentToken = Convert.ToUInt32(data["BToken"]);
                }


                if (instrument.InstrumentType.Trim(' ').ToLower() == "ce")
                {
                    ceList.TryAdd(instrument.Strike, instrument);
                }
                else if (instrument.InstrumentType.Trim(' ').ToLower() == "pe")
                {
                    peList.TryAdd(instrument.Strike, instrument);
                }
#if market
                dsInstruments.Tables[1].PrimaryKey = new DataColumn[] { dsInstruments.Tables[1].Columns["Instrument_Token"] };
                kotakToken = 0;// Convert.ToUInt32(dsInstruments.Tables[1].Rows.Find(instrument.InstrumentToken)[1]);
#elif local
                kotakToken = 0;
#endif
                mappedTokens.TryAdd(instrument.InstrumentToken, kotakToken);
                //mappedTokens.TryAdd(instrument.InstrumentToken, instrument.InstrumentToken);
                allOptions.TryAdd(instrument.InstrumentToken, instrument);

                //activeInstruments.Add(String.Format("{0}{1}", instrument.InstrumentType, instrument.Strike) , instrument);
                //options.Add(instrument);
            }
            return allOptions;
            //SortedList<decimal, Instrument>[] optionUniverse = new SortedList<decimal, Instrument>[2];
            //optionUniverse[(int)InstrumentType.CE] = ceList;
            //optionUniverse[(int)InstrumentType.PE] = peList;
        }
        public SortedList<decimal, Instrument[]> LoadCloseByStraddleOptions(DateTime? expiry,
        uint baseInstrumentToken, decimal baseInstrumentPrice, decimal maxDistanceFromBInstrument, 
        out Dictionary<uint, Instrument>  optionDictionary, out Dictionary<uint, uint> mappedTokens)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.LoadCloseByStraddleOptions(expiry, baseInstrumentToken, baseInstrumentPrice,
                maxDistanceFromBInstrument);

            SortedList<decimal, Instrument[]> straddleList = new SortedList<decimal, Instrument[]>();
            optionDictionary = new Dictionary<uint, Instrument>();
            mappedTokens = new Dictionary<uint, uint>();
            uint kotakToken = 0;

            dsInstruments.Tables[1].PrimaryKey = new DataColumn[] { dsInstruments.Tables[1].Columns[0] };
            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Instrument instrument = new Instrument();
                instrument.InstrumentToken = Convert.ToUInt32(data["Instrument_Token"]);
                instrument.TradingSymbol = Convert.ToString(data["TradingSymbol"]);
                instrument.Strike = Convert.ToDecimal(data["Strike"]);
                if (data["Expiry"] != DBNull.Value)
                {
                    instrument.Expiry = Convert.ToDateTime(data["Expiry"]);
                }
                instrument.InstrumentType = Convert.ToString(data["Instrument_Type"]);
                instrument.LotSize = Convert.ToUInt32(data["Lot_Size"]);
                instrument.Exchange = Convert.ToString(data["Exchange"]);
                instrument.Segment = Convert.ToString(data["Segment"]);
                if (data["BToken"] != DBNull.Value)
                {
                    instrument.BaseInstrumentToken = Convert.ToUInt32(data["BToken"]);
                }

                if (!straddleList.ContainsKey(instrument.Strike))
                {
                    straddleList.Add(instrument.Strike, new Instrument[2]);
                }

                if (instrument.InstrumentType.Trim(' ').ToLower() == "ce")
                {
                    straddleList[instrument.Strike][(int)InstrumentType.CE] = instrument;
                }
                else if (instrument.InstrumentType.Trim(' ').ToLower() == "pe")
                {
                    straddleList[instrument.Strike][(int)InstrumentType.PE] = instrument;
                }
                optionDictionary.Add(instrument.InstrumentToken, instrument);
#if market
                dsInstruments.Tables[1].PrimaryKey = new DataColumn[] { dsInstruments.Tables[1].Columns["Instrument_Token"] };
                kotakToken = Convert.ToUInt32(dsInstruments.Tables[1].Rows.Find(instrument.InstrumentToken)[1]);
#elif local
                kotakToken = 0;
#endif
                mappedTokens.Add(instrument.InstrumentToken, kotakToken);
            }
            return straddleList;
        }
        public SortedList<decimal, Instrument>[] LoadCloseByExpiryOptions(uint baseInstrumentToken, 
            decimal baseInstrumentPrice, decimal maxDistanceFromBInstrument, DateTime currentTime)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.LoadCloseByExpiryOptions(baseInstrumentToken, baseInstrumentPrice,
                maxDistanceFromBInstrument, currentTime);

            List<Instrument> options = new List<Instrument>();
            SortedList<decimal, Instrument> ceList = new SortedList<decimal, Instrument>();
            SortedList<decimal, Instrument> peList = new SortedList<decimal, Instrument>();

            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Instrument instrument = new Instrument();
                instrument.InstrumentToken = Convert.ToUInt32(data["Instrument_Token"]);
                instrument.TradingSymbol = Convert.ToString(data["TradingSymbol"]);
                instrument.Strike = Convert.ToDecimal(data["Strike"]);
                if (data["Expiry"] != DBNull.Value)
                {
                    instrument.Expiry = Convert.ToDateTime(data["Expiry"]);
                }
                instrument.InstrumentType = Convert.ToString(data["Instrument_Type"]);
                instrument.LotSize = Convert.ToUInt32(data["Lot_Size"]);
                instrument.Exchange = Convert.ToString(data["Exchange"]);
                instrument.Segment = Convert.ToString(data["Segment"]);
                if (data["BToken"] != DBNull.Value)
                {
                    instrument.BaseInstrumentToken = Convert.ToUInt32(data["BToken"]);
                }


                if (instrument.InstrumentType.Trim(' ').ToLower() == "ce")
                {
                    ceList.Add(instrument.Strike, instrument);
                }
                else if (instrument.InstrumentType.Trim(' ').ToLower() == "pe")
                {
                    peList.Add(instrument.Strike, instrument);
                }

                //activeInstruments.Add(String.Format("{0}{1}", instrument.InstrumentType, instrument.Strike) , instrument);
                //options.Add(instrument);
            }
            //return options;
            SortedList<decimal, Instrument>[] optionUniverse = new SortedList<decimal, Instrument>[2];
            optionUniverse[(int)InstrumentType.CE] = ceList;
            optionUniverse[(int)InstrumentType.PE] = peList;

            return optionUniverse;
        }

        public DataSet LoadHistoricalTokenVolume(AlgoIndex algoIndex, DateTime startTime,
            DateTime endTime, TimeSpan candleTimeSpan, int numberofCandles)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet ds = marketDAO.LoadHistoricalTokenVolume(algoIndex, startTime,
                endTime, candleTimeSpan, numberofCandles);

            return ds;
        }
        
        public Dictionary<uint, List<decimal>> GetHistoricalCandlePrices(int candlesCount, DateTime lastCandleEndTime, string tokenList,
            TimeSpan _candleTimeSpan, bool isBaseInstrument = false, CandleType candleType = CandleType.Time, uint vThreshold = 0, uint bToken = 256265)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet ds = marketDAO.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, tokenList, 
                _candleTimeSpan, isBaseInstrument, candleType, vThreshold, bToken);

            Dictionary<uint, List<decimal>> tokenPrices = new Dictionary<uint, List<decimal>>();
            foreach(DataRow dr in ds.Tables[0].Rows)
            {
                uint token = Convert.ToUInt32(dr["InstrumentToken"]);
                if (tokenPrices.ContainsKey(token))
                {
                    tokenPrices[token].Add(Convert.ToDecimal(dr["ClosePrice"]));
                }
                else
                {
                    List<decimal> prices = new List<decimal>();
                    prices.Add(Convert.ToDecimal(dr["ClosePrice"]));
                    tokenPrices.Add(token, prices);
                }
                
            }
            return tokenPrices;
        }

        public Dictionary<uint, List<decimal>> GetHistoricalClosePricesFromCandles(int candlesCount, DateTime lastCandleEndTime, string tokenList, TimeSpan _candleTimeSpan)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet ds = marketDAO.GetHistoricalClosePricesFromCandles(candlesCount, lastCandleEndTime, tokenList, _candleTimeSpan);

            //List<decimal> prices = new List<decimal>();
            //foreach (DataRow dr in ds.Tables[0].Rows)
            //{
            //    prices.Add((decimal)dr[0]);
            //}
            //return prices;
            Dictionary<uint, List<decimal>> tokenPrices = new Dictionary<uint, List<decimal>>();
            foreach (DataRow dr in ds.Tables[0].Rows)
            {
                uint token = Convert.ToUInt32(dr["InstrumentToken"]);
                if (tokenPrices.ContainsKey(token))
                {
                    tokenPrices[token].Add(Convert.ToDecimal(dr["ClosePrice"]));
                }
                else
                {
                    List<decimal> prices = new List<decimal>();
                    prices.Add(Convert.ToDecimal(dr["ClosePrice"]));
                    tokenPrices.Add(token, prices);
                }

            }
            return tokenPrices;
        }

        public DataSet LoadCandles(int numberofCandles, CandleType candleType, DateTime endDateTime, string instrumentTokenList, TimeSpan timeFrame)
        {
            MarketDAO marketDAO = new MarketDAO();
            DataSet dsCandles = marketDAO.LoadCandles(numberofCandles, candleType, endDateTime, instrumentTokenList, timeFrame);

            return dsCandles;

            //List<Candle> candleList = new List<Candle>();

            //foreach (DataRow candle in dsCandles.Tables[0].Rows)
            //{
            //    Candle instrument = new Candle();
            //    instrument.InstrumentToken = Convert.ToUInt32(optionData["Token"]);
            //    instrument.TradingSymbol = Convert.ToString(optionData["Symbol"]);
            //    instrument.Strike = Convert.ToDecimal(optionData["StrikePrice"]);
            //    instrument.Expiry = Convert.ToDateTime(optionData["ExpiryDate"]);
            //    instrument.InstrumentType = Convert.ToString(optionData["Type"]);
            //    instrument.LotSize = Convert.ToUInt32(optionData["LotSize"]);




            //    updateCMD.Parameters.AddWithValue("@instrumentToken", (Int64)instrumentToken);
            //    updateCMD.Parameters.AddWithValue("@closePrice", closePrice);
            //    updateCMD.Parameters.AddWithValue("@CloseTime", CloseTime);
            //    updateCMD.Parameters.AddWithValue("@closeVolume", closeVolume);
            //    updateCMD.Parameters.AddWithValue("@downTicks", downTicks);
            //    updateCMD.Parameters.AddWithValue("@highPrice", highPrice);
            //    updateCMD.Parameters.AddWithValue("@highTime", highTime);
            //    updateCMD.Parameters.AddWithValue("@highVolume", highVolume);
            //    updateCMD.Parameters.AddWithValue("@lowPrice", lowPrice);
            //    updateCMD.Parameters.AddWithValue("@lowTime", lowTime);
            //    updateCMD.Parameters.AddWithValue("@lowVolume", lowVolume);
            //    updateCMD.Parameters.AddWithValue("@maxPriceLevelBuyCount", (int)maxPriceLevelBuyCount);
            //    updateCMD.Parameters.AddWithValue("@maxPriceLevelBuyVolume", (int)maxPriceLevelBuyVolume);
            //    updateCMD.Parameters.AddWithValue("@maxPriceLevelMoney", maxPriceLevelMoney);
            //    updateCMD.Parameters.AddWithValue("@maxPriceLevelPrice", maxPriceLevelPrice);
            //    updateCMD.Parameters.AddWithValue("@maxPriceLevelSellCount", (int)maxPriceLevelSellCount);
            //    updateCMD.Parameters.AddWithValue("@maxPriceLevelSellVolume", (int)maxPriceLevelSellVolume);
            //    updateCMD.Parameters.AddWithValue("@maxPriceLevelTotalVolume", (int)maxPriceLevelTotalVolume);
            //    updateCMD.Parameters.AddWithValue("@minPriceLevelBuyCount", (int)minPriceLevelBuyCount);
            //    updateCMD.Parameters.AddWithValue("@minPriceLevelBuyVolume", (int)minPriceLevelBuyVolume);
            //    updateCMD.Parameters.AddWithValue("@minPriceLevelMoney", minPriceLevelMoney);
            //    updateCMD.Parameters.AddWithValue("@minPriceLevelPrice", minPriceLevelPrice);
            //    updateCMD.Parameters.AddWithValue("@minPriceLevelSellCount", (int)minPriceLevelSellCount);
            //    updateCMD.Parameters.AddWithValue("@minPriceLevelSellVolume", (int)minPriceLevelSellVolume);
            //    updateCMD.Parameters.AddWithValue("@minPriceLevelTotalVolume", (int)minPriceLevelTotalVolume);
            //    updateCMD.Parameters.AddWithValue("@openInterest", openInterest);
            //    updateCMD.Parameters.AddWithValue("@openPrice", openPrice);
            //    updateCMD.Parameters.AddWithValue("@openTime", openTime);
            //    updateCMD.Parameters.AddWithValue("@openVolume", openVolume);
            //    updateCMD.Parameters.AddWithValue("@relativeVolume", (int)relativeVolume);
            //    updateCMD.Parameters.AddWithValue("@totalPrice", totalPrice);
            //    updateCMD.Parameters.AddWithValue("@totalTicks", totalTicks);
            //    updateCMD.Parameters.AddWithValue("@totalVolume", totalVolume);
            //    updateCMD.Parameters.AddWithValue("@upTicks", upTicks);
            //    updateCMD.Parameters.AddWithValue("@Arg", Arg.ToString());
            //    updateCMD.Parameters.AddWithValue("@CandleType", (int)candleType);




            //    options.Add(instrument);



        }



            //}

            public List<Instrument> RetrieveOptions(UInt32 baseInstrumentToken, DateTime expiryDate)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveOptions(baseInstrumentToken, expiryDate);
        }
        public List<Instrument> RetrieveBaseInstruments()
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveBaseInstruments();
        }

        public DataSet RetrieveActiveStrangles(AlgoIndex algoIndex)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveActiveStrangles(algoIndex);
        }
        public DataSet RetrieveActiveData(AlgoIndex algoIndex)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveActiveData(algoIndex);
        }
        public DataSet GetActiveAlgos(AlgoIndex algoIndex, string userId="")
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.GetActiveAlgos(algoIndex, userId);
        }
        //public DataSet GetInstrument(decimal strikePrice, string InstrumentType)
        //{
        //    MarketDAO marketDAO = new MarketDAO();
        //    return marketDAO.GetInstrument(strikePrice, InstrumentType);
        //}

        public DataSet RetrieveActiveStrangleData(AlgoIndex algoIndex)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveActiveStrangleData(algoIndex);
        }

        public DataSet RetrieveActivePivotInstruments(AlgoIndex algoIndex)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveActivePivotInstruments(algoIndex);
        }

        public DataSet TestRetrieveActivePivotInstruments(AlgoIndex algoIndex)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.TestRetrieveActivePivotInstruments(algoIndex);
        }

        


        public decimal RetrieveLastPrice(UInt32 token, DateTime? time = null, bool buyOrder = false)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveLastPrice(token, time, buyOrder);
        }
       
        public decimal UpdateTrade(int strategyID, UInt32 instrumentToken, ShortTrade trade, AlgoIndex algoIndex, int tradedLot = 0, int triggerID = 0)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.UpdateTrade(strategyID, instrumentToken, trade.AveragePrice, trade.ExchangeTimestamp, 
                trade.OrderId, trade.Quantity, trade.TransactionType, algoIndex, tradedLot, triggerID);
        }
        public decimal UpdateTrade(int strategyID, UInt32 instrumentToken, Order order, AlgoIndex algoIndex, int tradedLot = 0, int triggerID = 0)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.UpdateTrade(strategyID, instrumentToken, order.AveragePrice, order.ExchangeTimestamp,
                order.OrderId, order.Quantity, order.TransactionType, algoIndex, tradedLot, triggerID);
        }

        public decimal InsertOptionIV(uint token1, uint token2, decimal _baseInstrumentPrice, decimal? iv1, decimal lastPrice1, DateTime lastTradeTime1,
            decimal? iv2, decimal lastPrice2, DateTime lastTradeTime2)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.InsertOptionIV(token1, token2, _baseInstrumentPrice, iv1, lastPrice1, lastTradeTime1, iv2, lastPrice2, lastTradeTime2);
        }


        public decimal UpdateOrder(int algoInstance, Order order, int strategyId = 0)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.UpdateOrder(order, algoInstance);
        }
        public void UpdateAlgoPnl(int algoInstance, decimal pnl)
        {
            MarketDAO marketDAO = new MarketDAO();
            marketDAO.UpdateAlgoPnl(algoInstance, pnl);
        }
        public void DeActivateAlgo(int algoInstance)
        {
            MarketDAO marketDAO = new MarketDAO();
            marketDAO.DeActivateAlgo(algoInstance);
        }

        public void DeActivateOrderTrio(OrderTrio orderTrio)
        {
            MarketDAO marketDAO = new MarketDAO();
            marketDAO.DeActivateOrderTrio(orderTrio);
        }

        public int StoreBoxData(int boxID, UInt32 firstCallToken, UInt32 secondCallToken, UInt32 firstPutToken, UInt32 secondPutToken,
            decimal firstCallPrice, decimal secondCallPrice, decimal firstPutPrice, decimal secondPutPrice, decimal boxValue)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.StoreBoxData(boxID, firstCallToken, secondCallToken, firstPutToken, secondPutToken,
            firstCallPrice, secondCallPrice, firstPutPrice, secondPutPrice, boxValue);
        }

        public DataSet RetrieveBoxData(int boxID)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveBoxData(boxID);
        }

        public List<string> RetrieveOptionExpiries(UInt32 baseInstrumentToken)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveOptionExpiries(baseInstrumentToken);
        }
        public SortedList<Decimal, Instrument> RetrieveNextNodes(UInt32 baseInstrumentToken,
            string instrumentType, decimal currentStrikePrice, DateTime expiry, int updownboth)
        {

            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.RetrieveNextNodes(baseInstrumentToken, instrumentType, currentStrikePrice, expiry, updownboth);

            SortedList<Decimal, Instrument> dinstruments = new SortedList<Decimal, Instrument>();

            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Instrument instrument = new Instrument();
                instrument.InstrumentToken = Convert.ToUInt32(data["Token"]);
                instrument.TradingSymbol = Convert.ToString(data["Symbol"]);
                instrument.Strike = Convert.ToDecimal(data["StrikePrice"]);
                instrument.Expiry = Convert.ToDateTime(data["ExpiryDate"]);
                instrument.InstrumentType = Convert.ToString(data["Type"]);
                instrument.LotSize = Convert.ToUInt32(data["LotSize"]);
                instrument.BaseInstrumentToken = Convert.ToUInt32(data["BInstrumentToken"]);
                ///TODO:This field is causing delay. See what you can do.
                ///Commenting again. Query it from Zerodha as GetLTP for instrument if this is not updated when required
                // instrument.LastPrice = Convert.ToUInt32(data["LastPrice"]);

                dinstruments.Add(instrument.Strike, instrument);
            }
            return dinstruments;
        }
        public SortedList<Decimal, Option> RetrieveNextNodes(UInt32 baseInstrumentToken, decimal bInstrumentPrice,
           string instrumentType, decimal currentStrikePrice, DateTime expiry, int updownboth, int noOfRecords = 10)
        {

            MarketDAO marketDAO = new MarketDAO();
            DataSet dsInstruments = marketDAO.RetrieveNextNodes(baseInstrumentToken, instrumentType, currentStrikePrice, expiry, updownboth, noOfRecords: noOfRecords);

            SortedList<Decimal, Option> options = new SortedList<Decimal, Option>();

            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Option option = new Option();
                option.InstrumentToken = Convert.ToUInt32(data["Token"]);
                option.TradingSymbol = Convert.ToString(data["Symbol"]);
                option.Strike = Convert.ToDecimal(data["StrikePrice"]);
                option.Expiry = Convert.ToDateTime(data["ExpiryDate"]);
                option.InstrumentType = Convert.ToString(data["Type"]);
                option.LotSize = Convert.ToUInt32(data["LotSize"]);
                option.BaseInstrumentToken = Convert.ToUInt32(data["BInstrumentToken"]);
                option.BaseInstrumentPrice = bInstrumentPrice;
                ///TODO:This field is causing delay. See what you can do.
                ///Commenting again. Query it from Zerodha as GetLTP for instrument if this is not updated when required
                // instrument.LastPrice = Convert.ToUInt32(data["LastPrice"]);

                options.Add(option.Strike, option);
            }
            return options;
        }

        public SortedList<Decimal, Instrument>[] RetrieveNextStrangleNodes(UInt32 baseInstrumentToken,
           DateTime expiry, decimal callStrikePrice, decimal putStrikePrice, int updownboth)
        {

            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveNextStrangleNodes(baseInstrumentToken, expiry, callStrikePrice, putStrikePrice, updownboth);
        }

        public SortedList<Decimal, Instrument>[] RetrieveNextStrangleNodes(UInt32 baseInstrumentToken,
           DateTime expiry, decimal callStrikePrice, decimal putStrikePrice, int updownboth, out Dictionary<uint, uint> mappedTokens)
        {

            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.RetrieveNextStrangleNodes(baseInstrumentToken, expiry, callStrikePrice, putStrikePrice, updownboth, out mappedTokens);
        }

        public Dictionary<uint, List<PriceTime>> RetrieveTicksFromDB(uint btoken, DateTime expiry, decimal fromStrike, decimal toStrike,
            bool futuresData, bool optionsData, DateTime tradeDate)
        {
            MarketDAO marketDAO = new MarketDAO();

            return marketDAO.GetTickData(btoken, expiry, fromStrike, toStrike, optionsData, futuresData, tradeDate);
        }
        public void UpdateListData(int strangleId, UInt32 instrumentToken, int instrumentIndex,int previousInstrumentIndex, string instrumentType, 
            UInt32 previousInstrumentToken, decimal currentNodePrice, decimal previousNodePrice, AlgoIndex algoIndex)
        {
            MarketDAO marketDAO = new MarketDAO();
            marketDAO.UpdateListData(strangleId, instrumentToken, instrumentIndex,previousInstrumentIndex, instrumentType, 
                previousInstrumentToken, currentNodePrice, previousNodePrice, algoIndex);
        }
        public void UpdateListData(int strangleId, UInt32 currentCallToken, UInt32 currentPutToken, UInt32 prevCallToken, UInt32 prevPutToken,
            int  currentIndex, int prevIndex, decimal[] currentSellPrice, decimal[] prevBuyPrice, AlgoIndex algoIndex, decimal bInstPrice)
        {
            MarketDAO marketDAO = new MarketDAO();
            marketDAO.UpdateListData(strangleId, currentCallToken, currentPutToken, prevCallToken, prevPutToken,
            currentIndex, prevIndex, currentSellPrice, prevBuyPrice, algoIndex, bInstPrice);
        }
       
        public void UpdateOptionData(int strategyId, UInt32 instrumentToken, string instrumentType, decimal lastPrice, decimal currentPrice, AlgoIndex algoIndex)
        {
            MarketDAO marketDAO = new MarketDAO();
            marketDAO.UpdateOptionData(strategyId, instrumentToken, instrumentType, lastPrice, currentPrice, algoIndex);
        }

        public int UpdateOrderTrio(OrderTrio orderTrio, int algoInstance)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.UpdateOrderTrio(orderTrio.Order.InstrumentToken, orderTrio.Order.OrderId, orderTrio.SLOrder != null? orderTrio.SLOrder.OrderId:"",
                orderTrio.TPOrder != null?orderTrio.TPOrder.OrderId: "", orderTrio.TargetProfit, orderTrio.StopLoss, orderTrio.EntryTradeTime, orderTrio.BaseInstrumentStopLoss, 
                orderTrio.TPFlag, algoInstance, orderTrio.isActive, orderTrio.Id, orderTrio.SLFlag, orderTrio.IndicatorsValue == null?"": JsonConvert.SerializeObject(orderTrio.IndicatorsValue) );
        }

        ///// <summary>
        ///// This method is used to update information of newly created strangle on the database.
        ///// </summary>
        ///// <param name="ceToken"></param>
        ///// <param name="peToken"></param>
        ///// <param name="ceLowerValue"></param>
        ///// <param name="ceLowerDelta"></param>
        ///// <param name="ceUpperValue"></param>
        ///// <param name="ceUpperDelta"></param>
        ///// <param name="peLowerValue"></param>
        ///// <param name="peLowerDelta"></param>
        ///// <param name="peUpperValue"></param>
        ///// <param name="peUpperDelta"></param>
        ///// <param name="stopLossPoints"></param>
        ///// <returns></returns>
        //public int UpdateStrangleData(UInt32 ceToken, UInt32 peToken, decimal ceLowerValue = 0, double ceLowerDelta = 0, decimal ceUpperValue = 0, double ceUpperDelta = 0,
        //    decimal peLowerValue = 0, double peLowerDelta = 0, decimal peUpperValue = 0, double peUpperDelta = 0, decimal stopLossPoints = 0)
        //{
        //    MarketDAO marketDAO = new MarketDAO();
        //    return marketDAO.UpdateStrangleData(ceToken, peToken, ceLowerValue, ceLowerDelta, ceUpperValue, ceUpperDelta,
        //    peLowerValue, peLowerDelta, peUpperValue, peUpperDelta, stopLossPoints);

        //}

        public int StoreStrangleData(UInt32 ceToken, UInt32 peToken, decimal cePrice, decimal pePrice, AlgoIndex algoIndex, 
            double ceLowerDelta = 0, double ceUpperDelta = 0, double peLowerDelta = 0, double peUpperDelta = 0,  
            double stopLossPoints = 0)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.UpdateStrangleData(ceToken, peToken, cePrice, pePrice, algoIndex, ceLowerDelta, ceUpperDelta,
            peLowerDelta, peUpperDelta, stopLossPoints);
        }
        public int StorePivotInstruments(UInt32 primaryInstrumentToken, DateTime expiry, AlgoIndex algoIndex, int maxlot, int lotunit, PivotFrequency pivotFrequency, int pivotWindow)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.StorePivotInstruments(primaryInstrumentToken, expiry, algoIndex, maxlot, lotunit, pivotFrequency, pivotWindow);

        }
        public int StoreStrangleData(UInt32 ceToken, UInt32 peToken, decimal cePrice, decimal pePrice, decimal bInstPrice, AlgoIndex algoIndex,
            decimal safetyMargin = 10, double stopLossPoints = 0, int initialQty=0, int maxQty=0, int stepQty=0, string ceOrderId = "", string peOrderId = "", string transactionType = "")
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.UpdateStrangleData(ceToken, peToken, cePrice, pePrice, bInstPrice, algoIndex, safetyMargin, stopLossPoints, initialQty, maxQty, stepQty, ceOrderId, peOrderId, transactionType);
        }

        public int CreateOptionStrategy(OptionStrategy optionStrategy)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.CreateOptionStrategy(parentToken: optionStrategy.ParentInstToken, algoIndex: optionStrategy.AlgoIndex, 
                stoplosspoints: optionStrategy.StopLossPoints, initialQty: optionStrategy.InitialQty, maxQty: optionStrategy.MaxQty, 
                stepQty: optionStrategy.StepQty);
        }

        public int StoreOptionStrategy(OptionStrategy optionStrategy)
        {

            StringBuilder tokens = new StringBuilder();
            StringBuilder prices = new StringBuilder();
            StringBuilder qtys = new StringBuilder();
            StringBuilder tags = new StringBuilder();
            StringBuilder transactionTypes = new StringBuilder();
            StringBuilder orderIds = new StringBuilder();
            foreach (ShortOrder shortOrder in optionStrategy.Orders)
            {
                tokens.AppendFormat("{0},", shortOrder.InstrumentToken);
                prices.AppendFormat("{0},", shortOrder.Price);
                qtys.AppendFormat("{0},", shortOrder.Quantity);
                tags.AppendFormat("{0},", shortOrder.Tag);
                transactionTypes.AppendFormat("{0},", shortOrder.TransactionType);
                orderIds.AppendFormat("{0},", shortOrder.OrderId);

            }
            tokens.Remove(tokens.Length - 2, 1);
            prices.Remove(prices.Length - 2, 1);
            qtys.Remove(qtys.Length - 2, 1);
            tags.Remove(tags.Length - 2, 1);
            transactionTypes.Remove(transactionTypes.Length - 2, 1);
            orderIds.Remove(orderIds.Length - 2, 1);


            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.StoreStrategyData(tokens.ToString(), prices.ToString(), qtys.ToString(), tags.ToString(), 
                transactionTypes.ToString(), orderIds.ToString(), parentToken: optionStrategy.ParentInstToken, parentPrice: optionStrategy.ParentInstPrice,
                algoIndex: optionStrategy.AlgoIndex, stoplosspoints: optionStrategy.StopLossPoints, 
                initialQty: optionStrategy.InitialQty, maxQty: optionStrategy.MaxQty, stepQty: optionStrategy.StepQty);
        }

        public int StoreStrangleData(UInt32 ceToken, UInt32 peToken, decimal cePrice, decimal pePrice, AlgoIndex algoIndex, 
            decimal ceMaxProfitPoints = 0, decimal ceMaxLossPoints = 0, decimal peMaxProfitPoints = 0, 
            decimal peMaxLossPoints = 0,  double stopLossPoints = 0)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.UpdateStrangleData(ceToken, peToken, cePrice, pePrice, algoIndex, ceMaxProfitPoints, 
                ceMaxLossPoints, peMaxProfitPoints, peMaxLossPoints, stopLossPoints: stopLossPoints);
        }
        public int StoreIndexForMainPainStrangle(uint bToken, DateTime expiry, int strikePriceIncrement, AlgoIndex algoIndex,
            int tradingQty, decimal maxLossThreshold, decimal profitTarget, DateTime timeOfOrder)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.StoreIndexForMainPain(bToken, expiry, strikePriceIncrement, algoIndex, tradingQty, maxLossThreshold, profitTarget, timeOfOrder);
        }

        public int StoreStrangleData(UInt32 ceToken, UInt32 peToken, decimal cePrice, decimal pePrice, AlgoIndex algoIndex,
           decimal ceMaxProfitPoints = 0, decimal ceMaxLossPoints = 0, decimal peMaxProfitPoints = 0,
           decimal peMaxLossPoints = 0, decimal ceMaxProfitPercent=0, decimal ceMaxLossPercent=0, decimal peMaxProfitPercent=0, 
           decimal peMaxLossPercent=0, double stopLossPoints = 0)
        {
            MarketDAO marketDAO = new MarketDAO();
            return marketDAO.UpdateStrangleData(ceToken, peToken, cePrice, pePrice, algoIndex, ceMaxProfitPoints,
                ceMaxLossPoints, peMaxProfitPoints, peMaxLossPoints, ceMaxProfitPercent, ceMaxLossPercent, 
                peMaxProfitPercent, peMaxLossPercent, stopLossPoints);

        }
    }
}
