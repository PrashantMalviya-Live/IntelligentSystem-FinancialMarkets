using DataAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GlobalLayer;
using BrokerConnectWrapper;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Algorithms.Indicators;
using Algorithm.Algorithm;
using static Algorithms.Utilities.Utility;
using System.Text.Json;
using System.IO;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Serialization;
using System.Runtime.Serialization;
using System.Reflection;
using System.Data;
using InfluxDB.Client.Api.Domain;
using Newtonsoft.Json.Linq;
using Algos.Indicators;
using System.Threading;
using Flee.PublicTypes;

namespace Algorithms.Utilities
{
    public class Utility
    {
        public static int GenerateAlgoInstance(AlgoIndex algoIndex, uint bToken, DateTime timeStamp, DateTime expiry,
            int initialQtyInLotsSize, int maxQtyInLotSize = 0, int stepQtyInLotSize = 0, decimal upperLimit = 0,
            decimal upperLimitPercent = 0, decimal lowerLimit = 0, decimal lowerLimitPercent = 0,
            float stopLossPoints = 0, int optionType = 0, float candleTimeFrameInMins = 5,
            CandleType candleType = CandleType.Time, int optionIndex = 0, decimal Arg1 = 0, decimal Arg2 = 0,
            decimal Arg3 = 0, decimal Arg4 = 0, decimal Arg5 = 0, decimal Arg6 = 0, decimal Arg7 = 0, decimal Arg8 = 0, 
            string Arg9 = "", bool positionSizing = false, decimal maxLossPerTrade = 0)
        {
            MarketDAO dao = new MarketDAO();
            return dao.GenerateAlgoInstance(algoIndex, bToken, timeStamp, expiry,
            initialQtyInLotsSize, maxQtyInLotSize, stepQtyInLotSize, upperLimit,
            upperLimitPercent, lowerLimit, lowerLimitPercent,
            stopLossPoints, optionType, 0, candleTimeFrameInMins, candleType, 
            Arg1, Arg2, Arg3, Arg4, Arg5, Arg6, Arg7, Arg8, Arg9, positionSizing, maxLossPerTrade);
        }

        public static void LoadTokens()
        {
            try
            {
                //SendData();
                ZConnect.Login();
                List<Instrument> instruments = ZObjects.kite.GetInstruments(Exchange: "NFO");
                MarketDAO dao = new MarketDAO();
                dao.StoreInstrumentList(instruments);

                instruments = ZObjects.kite.GetInstruments(Exchange: "NSE");
                dao = new MarketDAO();
                dao.StoreInstrumentList(instruments);
            }
            catch (Exception ex)
            {

            }
        }

        //Update generated alert to database
        public static async Task<int> UpdateGeneratedAlertsAsync(Alert alert)
        {
            MarketDAO marketDAO = new MarketDAO();
            return await marketDAO.UpdateGeneratedAlertsAsync(alert);
        }

        /// <summary>
        /// Dummy database pull for active alerts that have credits available for alerts
        /// </summary>
        /// <returns></returns>
        public static List<AlertTriggerData> RetrieveAlertTriggerData()
        {
            //Instruments->Timeframes->Indicators

            List<AlertTriggerData> alertTriggerCriteria = new List<AlertTriggerData>();


            MarketDAO dao = new MarketDAO();
            DataSet dsAlertTriggers = dao.GetActiveAlertTriggers();

            foreach (DataRow alertTriggerRow in dsAlertTriggers.Tables[0].Rows)
            {
                alertTriggerCriteria.Add(new()
                {
                    //ID = Convert.ToInt32(alertTriggerRow["Id"]),
                    //InstrumentToken = Convert.ToUInt32(alertTriggerRow["InstrumentToken"]),
                    //TradingSymbol = Convert.ToString(alertTriggerRow["TradingSymbol"]),
                    //UserId = Convert.ToString(alertTriggerRow["UserID"]),
                    //AlertCriteria = AlertCriteriaConverter(JsonSerializer.Deserialize<List<AlertCriterion2>> ((string)alertTriggerRow["AlertCriteria"]))

                    ID = Convert.ToInt32(alertTriggerRow["Id"]),
                    InstrumentToken = Convert.ToUInt32(alertTriggerRow["InstrumentToken"]),
                    TradingSymbol = Convert.ToString(alertTriggerRow["TradingSymbol"]),
                    SetupDate = Convert.ToDateTime(alertTriggerRow["SetUpDate"]),
                    StartDate = Convert.ToDateTime(alertTriggerRow["StartDate"]),
                    EndDate = Convert.ToDateTime(alertTriggerRow["EndDate"]),
                    NumberOfTriggersPerInterval = Convert.ToByte(alertTriggerRow["NumberOfTriggersPerInterval"]),
                    TotalNumberOfTriggers = Convert.ToByte(alertTriggerRow["TotalNumberOfTriggers"]),
                    UserId = Convert.ToString(alertTriggerRow["UserId"]),
                    TriggerFrequency = Convert.ToByte(alertTriggerRow["TriggerFrequency"]),
                    AlertCriteria = AlertCriteriaConverter(JsonSerializer.Deserialize<Dictionary<string, IndicatorOperator>>((string)alertTriggerRow["AlertCriteria"]))
                });
            }
            return alertTriggerCriteria;
            //List<AlertCriterion> alertCriteria = new List<AlertCriterion>();

            
            
            //AlertCriterion alertTriggerCriterion = new AlertCriterion
            //{
            //    LHSTimeInMinutes = 3,
            //    LHSIndicator = new ExponentialMovingAverage(32),
            //    RHSTimeInMinutes = 5,
            //    RHSIndicator = new RelativeStrengthIndex(),
            //    MathOperator = MATH_OPERATOR.GREATER_THAN,
            //    LogicalCriteria = LOGICAL_CRITERIA.NULL
            //};

            //alertCriteria.Add(alertTriggerCriterion);

            ////for each new object check if token exits, timeframe exits,so that there are no extra objects

            //alertTriggerCriterion = new AlertCriterion
            //{
            //    LHSTimeInMinutes = 5,
            //    LHSIndicator = alertCriteria[0].RHSIndicator,// new ExponentialMovingAverage(32),
            //    RHSTimeInMinutes = 15,
            //    RHSIndicator = new ExponentialMovingAverage(32),
            //    MathOperator = MATH_OPERATOR.GREATER_THAN,
            //    LogicalCriteria = LOGICAL_CRITERIA.NULL
            //};

            //alertCriteria.Add(alertTriggerCriterion);

            //alertTriggerCriterion = new AlertCriterion
            //{
            //    LHSTimeInMinutes = 3,
            //    LHSIndicator = alertCriteria[0].LHSIndicator,
            //    RHSTimeInMinutes = 15,
            //    RHSIndicator = alertCriteria[1].RHSIndicator,
            //    MathOperator = MATH_OPERATOR.GREATER_THAN,
            //    LogicalCriteria = LOGICAL_CRITERIA.NULL
            //};

            //alertCriteria.Add(alertTriggerCriterion);

            ////AlertTrigger alertTrigger = new AlertTrigger() { ID = 1, 
            ////    InstrumentToken = 260105, 
            ////    TradingSymbol = "NIFTY50", AlertCriteria = System.Text.Json. JsonSerializer.Serialize(alertCriteria) };
            ////alertTriggerCriteria.Add(alertTrigger);

            //var serializeOptions = new JsonSerializerOptions();
            //serializeOptions.Converters.Add(new IndicatorConverterWithTypeDiscriminator());

            //string alertCriteriaJson = JsonSerializer.Serialize(alertCriteria, serializeOptions);


            //AlertTrigger alertTrigger = new AlertTrigger()
            //{
            //    ID = 1,
            //    InstrumentToken = 260105,
            //    TradingSymbol = "NIFTY50",
            //    AlertCriteria = alertCriteriaJson// "[{\"Id\":0,\"LHSIndicator\":\"4\",\"LHSTimeInMinutes\":\"2\",\"RHSIndicator\":\"3\",\"RHSTimeInMinutes\":\"2\",\"MathOperator\":\"1\",\"LogicalCriteria\":\"1\"}]"
            //};
            //alertTriggerCriteria.Add(alertTrigger);

            //return alertTriggerCriteria;
        }

        public static Dictionary<uint, AlertInstrumentTokenWithIndicators> LoadAlertTriggerData(List<AlertTriggerData> alertTriggers)
        {

            //Parameters:
            //List of active parameters based on active users with funded accounts
            //All these should be pull from database
            //List of all time frames
            //List of all indicators
            //subscribers should generate events to event bus based on which alerts would be sent

            //Instruments->Timeframes->Indicators
            Dictionary<uint, AlertInstrumentTokenWithIndicators> alertTriggerData = new Dictionary<uint, AlertInstrumentTokenWithIndicators>();

            foreach (var alertTrigger in alertTriggers)
            {
                var alertCriteriaJson = alertTrigger.AlertCriteria;

                List<IIndicator> indicators = new List<IIndicator>();
                List<TimeFrameWithIndicators> timeFrameWithIndicators = new List<TimeFrameWithIndicators>();

                AlertInstrumentTokenWithIndicators alertTokensWithIndicators = new AlertInstrumentTokenWithIndicators();
                alertTokensWithIndicators.TimeFrameWithIndicators = timeFrameWithIndicators;
                alertTokensWithIndicators.Instrument = new GlobalLayer.Instrument();

                foreach (var alertTriggerCriterion in alertTrigger.AlertCriteria)
                {
                    foreach (var component in alertTriggerCriterion.Components)
                    {
                        //check this
                        if (typeof(IIndicator).IsAssignableFrom(component.GetType()))
                        {
                            IIndicator indicator = (IIndicator)component;
                            if (!alertTriggerData.ContainsKey(alertTrigger.InstrumentToken))
                            {
                                alertTokensWithIndicators = new AlertInstrumentTokenWithIndicators();

                                indicators = new List<IIndicator>() { indicator as IIndicator };
                                int propertyValue = (int)GetPropValue(indicator, "TimeSpanInMins");

                                timeFrameWithIndicators = new List<TimeFrameWithIndicators>
                            {
                                new TimeFrameWithIndicators {TimeFrame = new TimeSpan(0,  propertyValue, 0), Indicators = indicators },
                            };

                                alertTokensWithIndicators.TimeFrameWithIndicators = timeFrameWithIndicators;
                                alertTokensWithIndicators.Instrument = new GlobalLayer.Instrument();

                                //alertTriggerData.Add(alertTriggerCriterion.InstrumentToken, alertTokensWithIndicators);

                                ////RHS Indicator
                                //propertyValue = (int)GetPropValue(alertTriggerCriterion.RHSIndicator, "TimeSpan");
                                //TimeFrameWithIndicators tfIndicators = alertTokensWithIndicators.TimeFrameWithIndicators.Find(_ => _.TimeFrame.Minutes == propertyValue);

                                //if (tfIndicators != null)
                                //{
                                //    tfIndicators.Indicators.Add(alertTriggerCriterion.RHSIndicator);
                                //}
                                //else
                                //{
                                //    indicators = new List<IIndicator> { alertTriggerCriterion.RHSIndicator };
                                //    tfIndicators = new TimeFrameWithIndicators { TimeFrame = new TimeSpan(0, propertyValue, 0), Indicators = indicators };
                                //    alertTokensWithIndicators.TimeFrameWithIndicators.Add(tfIndicators);
                                //}

                                alertTriggerData.Add(alertTrigger.InstrumentToken, alertTokensWithIndicators);
                            }
                            else
                            {
                                AlertInstrumentTokenWithIndicators alertInstrumentTokenWithIndicators = alertTriggerData[alertTrigger.InstrumentToken];

                                //For same instrument , check time frames and for each time frame check indicators

                                timeFrameWithIndicators = alertInstrumentTokenWithIndicators.TimeFrameWithIndicators;

                                bool lhsTimeFrameExists = false;
                                //bool rhsTimeFrameExists = false;
                                int lhspropertyValue = (int)GetPropValue(indicator, "TimeSpanInMins");

                                for (int i = 0; i < timeFrameWithIndicators.Count; i++)
                                {
                                    //LHS Indicator
                                    lhsTimeFrameExists = lhsTimeFrameExists || AddIndicatorWithTimeFrame(timeFrameWithIndicators[i], lhspropertyValue, indicator);
                                }

                                if (!lhsTimeFrameExists)
                                {
                                    AddIndicatorWithTimeFrame(timeFrameWithIndicators, lhspropertyValue, indicator);
                                }
                                //if (!rhsTimeFrameExists)
                                //{
                                //    AddIndicatorWithTimeFrame(timeFrameWithIndicators, rhspropertyValue, alertTriggerCriterion.RHSIndicator);
                                //}
                            }
                        }
                    }
                }
            }
            return alertTriggerData;

        }

        //public static Dictionary<uint, AlertInstrumentTokenWithIndicators> LoadAlertTriggerData(List<AlertTriggerData> alertTriggers)
        //{

        //    //Parameters:
        //    //List of active parameters based on active users with funded accounts
        //    //All these should be pull from database
        //    //List of all time frames
        //    //List of all indicators
        //    //subscribers should generate events to event bus based on which alerts would be sent

        //    //Instruments->Timeframes->Indicators
        //    Dictionary<uint, AlertInstrumentTokenWithIndicators> alertTriggerData = new Dictionary<uint, AlertInstrumentTokenWithIndicators>();

        //    foreach (var alertTrigger in alertTriggers)
        //    {
        //        var alertCriteriaJson = alertTrigger.AlertCriteria;

        //        //List<uint> InstrumentTokenList = new List<uint>();
        //        //List<int> timeframes = alertCriteria.Select(_ => _.LHSTimeInMinutes).ToList();
        //        //timeframes.AddRange(alertCriteria.Select(_ => _.RHSTimeInMinutes).ToList());

        //        List<IIndicator> indicators = new List<IIndicator>();
        //        List<TimeFrameWithIndicators> timeFrameWithIndicators = new List<TimeFrameWithIndicators>();


        //        AlertInstrumentTokenWithIndicators alertTokensWithIndicators = new AlertInstrumentTokenWithIndicators();
        //        alertTokensWithIndicators.TimeFrameWithIndicators = timeFrameWithIndicators;
        //        alertTokensWithIndicators.Instrument = new GlobalLayer.Instrument();


        //        //var serializeOptions = new JsonSerializerOptions();
        //        //serializeOptions.Converters.Add(new IndicatorConverterWithTypeDiscriminator());

        //        //JsonSerializerOptions options = new() { TypeInfoResolver = new PolymorphicTypeResolver() };

        //        //List<AlertCriterion2> alertCriteria2 = JsonSerializer.Deserialize<List<AlertCriterion2>>(alertCriteriaJson);

        //        //List<AlertCriterion> alertCriteria = AlertCriteriaConverter(alertCriteria2);

        //        //string json = JsonConvert.SerializeObject(values, Formatting.Indented, new JsonSerializerSettings
        //        //{
        //        //    TypeNameHandling = TypeNameHandling.Auto,
        //        //    Binder = binder
        //        //});


        //        foreach (var alertTriggerCriterion in alertTrigger.AlertCriteria)
        //        {
        //            if (!alertTriggerData.ContainsKey(alertTrigger.InstrumentToken))
        //            {
        //                //InstrumentTokenList.Add(alertTriggerCriterion.InstrumentToken);

        //                //LHS Indicator
        //                indicators = new List<IIndicator> { alertTriggerCriterion.LHSIndicator };
        //                alertTokensWithIndicators = new AlertInstrumentTokenWithIndicators();

        //                int propertyValue = (int) GetPropValue(alertTriggerCriterion.LHSIndicator, "TimeSpan");

        //                timeFrameWithIndicators = new List<TimeFrameWithIndicators>
        //            {
        //                new TimeFrameWithIndicators {TimeFrame = new TimeSpan(0,  propertyValue, 0), Indicators = indicators },
        //            };

        //                alertTokensWithIndicators.TimeFrameWithIndicators = timeFrameWithIndicators;
        //                alertTokensWithIndicators.Instrument = new GlobalLayer.Instrument();

        //                //alertTriggerData.Add(alertTriggerCriterion.InstrumentToken, alertTokensWithIndicators);

        //                //RHS Indicator
        //                propertyValue = (int)GetPropValue(alertTriggerCriterion.RHSIndicator, "TimeSpan");
        //                TimeFrameWithIndicators tfIndicators = alertTokensWithIndicators.TimeFrameWithIndicators.Find(_ => _.TimeFrame.Minutes == propertyValue);

        //                if (tfIndicators != null)
        //                {
        //                    tfIndicators.Indicators.Add(alertTriggerCriterion.RHSIndicator);
        //                }
        //                else
        //                {
        //                    indicators = new List<IIndicator> { alertTriggerCriterion.RHSIndicator };
        //                    tfIndicators = new TimeFrameWithIndicators { TimeFrame = new TimeSpan(0, propertyValue, 0), Indicators = indicators };
        //                    alertTokensWithIndicators.TimeFrameWithIndicators.Add(tfIndicators);
        //                }

        //                alertTriggerData.Add(alertTrigger.InstrumentToken, alertTokensWithIndicators);
        //            }
        //            else
        //            {
        //                AlertInstrumentTokenWithIndicators alertInstrumentTokenWithIndicators = alertTriggerData[alertTrigger.InstrumentToken];

        //                //For same instrument , check time frames and for each time frame check indicators

        //                timeFrameWithIndicators = alertInstrumentTokenWithIndicators.TimeFrameWithIndicators;

        //                bool lhsTimeFrameExists = false;
        //                bool rhsTimeFrameExists = false;
        //                int lhspropertyValue = (int)GetPropValue(alertTriggerCriterion.LHSIndicator, "TimeSpan");
        //                int rhspropertyValue = (int)GetPropValue(alertTriggerCriterion.RHSIndicator, "TimeSpan");

        //                for (global::System.Int32 i = 0; i < timeFrameWithIndicators.Count; i++)
        //                {
        //                    //LHS Indicator
        //                    lhsTimeFrameExists = lhsTimeFrameExists || AddIndicatorWithTimeFrame(timeFrameWithIndicators[i], lhspropertyValue, alertTriggerCriterion.LHSIndicator);
        //                    //RHS Indicator
        //                    rhsTimeFrameExists = rhsTimeFrameExists || AddIndicatorWithTimeFrame(timeFrameWithIndicators[i], rhspropertyValue, alertTriggerCriterion.RHSIndicator);
        //                }

        //                if (!lhsTimeFrameExists)
        //                {
        //                    AddIndicatorWithTimeFrame(timeFrameWithIndicators, lhspropertyValue, alertTriggerCriterion.LHSIndicator);
        //                }
        //                if (!rhsTimeFrameExists)
        //                {
        //                    AddIndicatorWithTimeFrame(timeFrameWithIndicators, rhspropertyValue, alertTriggerCriterion.RHSIndicator);
        //                }
        //            }
        //        }
        //    }
        //    return alertTriggerData;

        //}
        public static object GetPropValue(object src, string propName)
        {
            return src.GetType().GetProperty(propName).GetValue(src, null);
        }
        private static IIndicator GetIndicatorObject(IndicatorType indicatorType)
        {
            IIndicator indicator = indicatorType switch
            {
                IndicatorType.RSI => new RelativeStrengthIndex(),
                IndicatorType.EMA => new ExponentialMovingAverage(),
                IndicatorType.SMA => new SimpleMovingAverage(),
                IndicatorType.Candle_Open => new TimeFrameCandleIndicator(CandlePrices.Open),
                IndicatorType.Candle_High => new TimeFrameCandleIndicator(CandlePrices.High),
                IndicatorType.Candle_Low => new TimeFrameCandleIndicator(CandlePrices.Low),
                IndicatorType.Candle_Close => new TimeFrameCandleIndicator(CandlePrices.Close),
                IndicatorType.MACD => new MovingAverageConvergenceDivergence(),
                //IndicatorType.Price => new decimal,
                IndicatorType.Range_Breakout => new RangeBreakoutRetraceIndicator(),
                IndicatorType.Stochastic => new StochasticOscillator(),
            };

            return indicator;
        }
        public static List<AlertCriterion> AlertCriteriaConverter (Dictionary<string, IndicatorOperator> indicatorOperatorView)
        {
            List<AlertCriterion> alertCriteria = new List<AlertCriterion>();

            AlertCriterion ac = new AlertCriterion();

            ac.Components = new List<IEquationComponent>();

            ExpressionContext context = new ExpressionContext();
            context.Imports.AddType(typeof(IndicatorHelper));


            StringBuilder expressionBuilder = new StringBuilder();
            
            StringBuilder expressionBuilder1 = new StringBuilder();
            StringBuilder expressionBuilder2 = new StringBuilder();

            for (int i = 0; i < indicatorOperatorView.Count; i++)
                //foreach (var ioView in indicatorOperatorView)
            {
                var io = indicatorOperatorView.ElementAt(i).Value;

                if (io.Type == IndicatorOperatorType.Indicator)
                {
                    var indicator = SetIndicator(io);
                    ac.Components.Add(indicator);

                    context.Variables.Add($"i{i}", indicator);
                    expressionBuilder1.Append($"i{i}.IsFormed and ");
                    expressionBuilder2.Append($"i{i}.GetCurrentDecimalValue()");

                }
                else
                {
                    ac.Components.Add(SetOperator(io, ref expressionBuilder2));
                }
            }

            expressionBuilder1 = expressionBuilder1.Remove(expressionBuilder1.Length - 4, 4);

            expressionBuilder.AppendFormat("({0}) and ({1})", expressionBuilder1.ToString(), expressionBuilder2);

            ac.AlertExpression = context.CompileDynamic(expressionBuilder.ToString());
            alertCriteria.Add(ac);
            
            return alertCriteria;
        }

        /// Element	Description	Example
        //+, -	Additive	100 + a
        //*, /, %	Multiplicative	100 * 2 / (3 % 2)
        //^	Power	2 ^ 16
        //-	Negation	-6 + 10
        //+	Concatenation	"abc" + "def"
        //<<, >>	Shift	0x80 >> 2
        //=, <>, <, >, <=, >=	Comparison	2.5 > 100
        //And, Or, Xor, Not Logical(1 > 10) and(true or not false)
        //And, Or, Xor, Not Bitwise	100 And 44 or(not 255)
        //If Conditional If(a > 100, "greater", "less")
        //Cast Cast and conversion cast(100.25, int)
        //[] Array index	1 + arr[i + 1]
        //.	Member varA.varB.function("a")
        //String literal		"string!"
        //Char literal		'c'
        //Boolean literal		true AND false
        //Real literal    Double and single	100.25 + 100.25f
        //Integer literal Signed/unsigned 32/64 bit	100 + 100U + 100L + 100LU
        //Hex literal		0xFF + 0xABCDU + 0x80L + 0xC9LU

        

        private static IIndicator SetIndicator(IndicatorOperator indicatorOperator)
        {
            int indicatorId = indicatorOperator.Id;

            IIndicator indicator = GetIndicatorObject((IndicatorType)indicatorId);

            DataLogic dl = new DataLogic();
            Dictionary<int, KeyValuePair<string, dynamic>> indicatorProperties = dl.GetIndicatorProperties(indicatorId);

            foreach(var dropDownProperty in indicatorOperator.DropDownComponentProperties)
            {
                int propertyValueId = Int32.Parse(dropDownProperty.SelectedValue);

                PropertyInfo propInfo = indicator.GetType().GetProperty(indicatorProperties[propertyValueId].Key);
                dynamic tpe = Convert.ChangeType(indicatorProperties[propertyValueId].Value, propInfo.PropertyType);
                propInfo.SetValue(indicator, tpe, null);
            }
            foreach (var textProperty in indicatorOperator.TextComponentProperties)
            {
                int propertyValueId = Int32.Parse(textProperty.Id);

                PropertyInfo propInfo = indicator.GetType().GetProperty(indicatorProperties[propertyValueId].Key);
                dynamic tpe = Convert.ChangeType(textProperty.Value, propInfo.PropertyType);
                propInfo.SetValue(indicator, tpe, null);
            }

            if (indicatorOperator.ChildIndicators.Count() != 0)
            {
                foreach (var childIOview in indicatorOperator.ChildIndicators)
                {
                    indicator.ChildIndicator = SetIndicator(childIOview.Value);
                    indicator.TimeSpanInMins = indicator.ChildIndicator.TimeSpanInMins;
                }
            }
            else
            {
                //Set the Timespan property of all indicators equal to the child indicator timespan. This will help segreate indicators based on time easily
                PropertyInfo pInfo = indicator.GetType().GetProperty("TimeSpan");
                indicator.TimeSpanInMins = (int) pInfo.GetValue(indicator, null);
            }

            return indicator;
        }

        private static Enumeration SetOperator(IndicatorOperator indicatorOperator, ref StringBuilder expressonBuilder)
        {
            int indicatorId = indicatorOperator.Id;

            Enumeration equationOperator;

            switch (indicatorOperator.Type)
            {
                case IndicatorOperatorType.MathOperator:
                    {
                        //switch (indicatorOperator.DropDownComponentProperties[0].Values[indicatorOperator.DropDownComponentProperties[0].SelectedValue])
                        switch (indicatorOperator.DropDownComponentProperties[0].SelectedValue)
                        {
                            case "50":
                                {
                                    expressonBuilder.Append(">");
                                    equationOperator = MathOperator.GREATER_THAN;
                                    break;
                                }
                            case "51":
                                {
                                    expressonBuilder.Append(">=");
                                    equationOperator = MathOperator.GREATER_THAN_OR_EQUAL_TO;
                                    break;
                                }
                            case "52":
                                {
                                    expressonBuilder.Append("<");
                                    equationOperator = MathOperator.LESS_THAN;
                                    break;
                                }
                            case "53":
                                {
                                    expressonBuilder.Append("<=");
                                    equationOperator = MathOperator.LESS_THAN_OR_EQUAL_TO;
                                    break;
                                }
                            case "54":
                                {
                                    expressonBuilder.Append("=");
                                    equationOperator = MathOperator.EQUAL_TO;
                                    break;
                                }
                            default:
                                {
                                    throw new InvalidOperationException("Inappropriate Math Operator");
                                }
                        }
                        break;
                    }
                case IndicatorOperatorType.LogicalOperator:
                    {
                        switch (indicatorOperator.DropDownComponentProperties[0].Values[indicatorOperator.DropDownComponentProperties[0].SelectedValue])
                        {
                            case "48":
                                {
                                    expressonBuilder.Append(" AND ");
                                    equationOperator = LogicalOperator.AND;
                                    break;
                                }
                            case "49":
                                {
                                    expressonBuilder.Append(" OR ");
                                    equationOperator = LogicalOperator.OR;
                                    break;
                                }
                            default:
                                {
                                    throw new InvalidOperationException("Inappropriate Logical Operator");
                                }
                        }
                        break;
                    }
                case IndicatorOperatorType.Bracket:
                    {
                        switch (indicatorOperator.DropDownComponentProperties[0].Values[indicatorOperator.DropDownComponentProperties[0].SelectedValue])
                        //switch (indicatorOperator.DropDownComponentProperties[0].SelectedValue)
                        {
                            case "55":
                                {
                                    expressonBuilder.Append(" ( ");
                                    equationOperator = BracketOperator.OpeningBracket;
                                    break;
                                }
                            case "56":
                                {
                                    expressonBuilder.Append(" ) ");
                                    equationOperator = BracketOperator.ClosingBracket;
                                    break;
                                }
                            default:
                                {
                                    throw new InvalidOperationException("Inappropriate Bracket Operator");
                                }
                        }
                        break;
                    }
                default:
                    {
                        throw new InvalidOperationException("Inappropriate Equation Operator");
                    }
            }

            return equationOperator;
        }
        //private static IIndicator SetIndicator(string indicatorJSon)
        //{
        //    string[] indicatorParts = indicatorJSon.Split(";");

        //    int indicatorId = Int32.Parse(indicatorParts[0].Split(":")[0]);
        //    int indicatorName = Int32.Parse(indicatorParts[0].Split(":")[0]);

        //    IIndicator indicator = GetIndicatorObject((IndicatorType)indicatorId);

        //    DataLogic dl = new DataLogic();
        //    Dictionary<int, KeyValuePair<string, dynamic>> indicatorProperties =  dl.GetIndicatorProperties(indicatorId);

        //    //start with i=1
        //    for (int j = 1; j < indicatorParts.Length; j++)
        //    {
        //        string indicatorPart = indicatorParts[j];

        //        int propertyValueId = Int32.Parse(indicatorPart.Split(":")[1]);

        //        PropertyInfo propertyInfo = indicator.GetType().GetProperty(indicatorProperties[propertyValueId].Key);

        //        dynamic type = Convert.ChangeType(indicatorProperties[propertyValueId].Value, propertyInfo.PropertyType);


        //        propertyInfo.SetValue(indicator, type, null);
        //    }
        //    return indicator;
        //}

        ///// <summary>
        ///// Dummy database pull for active alerts
        ///// </summary>
        ///// <returns></returns>
        //public static List<AlertTriggerCriterion> RetrieveAlertTriggerData()
        //{

        //    ///Pull from database Json data. and then segregate all the timeframes and its indicators for those timeframes
        //    //Parameters:
        //    //List of active parameters based on active users with funded accounts
        //    //All these should be pull from database
        //    //List of all time frames
        //    //List of all indicators
        //    //subscribers should generate events to event bus based on which alerts would be sent

        //    List<TimeSpan> timeFrames = new List<TimeSpan> { new TimeSpan(0, 3, 0), new TimeSpan(0, 5, 0), new TimeSpan(0, 15, 0)};

        //    ////These indicators should be based on some index, so that when database index is pulled, relevant indicator is formed. Similar to AlgoIndex.
        //    List<IIndicator> indicators = new List<IIndicator> { new ExponentialMovingAverage(32), new RelativeStrengthIndex() };

        //    ////There should be some logic to determine how many subscibers and how many alert services needed to cover for all the symbols
        //    ////that eligible users have selected
        //    List<uint> instrumentTokens = new List<uint> { 260105 };


        //    //List<TimeSpan> timeFrames = new List<TimeSpan> { new TimeSpan(0, 1, 0), new TimeSpan(0, 3, 0), new TimeSpan(0, 5, 0) };
        //    //List<IIndicator> indicators = new List<IIndicator> { new ExponentialMovingAverage(32), new RelativeStrengthIndex() };


        //    List<TimeFrameWithIndicators> timeFrameWithIndicators = new List<TimeFrameWithIndicators>
        //    {
        //        new TimeFrameWithIndicators {TimeFrame = new TimeSpan(0, 3, 0), Indicators = indicators },
        //    };


        //    //Instruments->Timeframes->Indicators


        //    List<AlertTriggerCriterion> alertTriggerCriteria = new List<AlertTriggerCriterion>();


        //    AlertTriggerCriterion alertTriggerCriterion = new AlertTriggerCriterion
        //    {
        //        InstrumentToken = 260105,
        //        LHSTimeInMinutes = 3,
        //        LHSIndicator = new ExponentialMovingAverage(32),
        //        RHSTimeInMinutes = 5,
        //        RHSIndicator = new ExponentialMovingAverage(32),
        //        MathOperator = MATH_OPERATOR.GREATER_THAN,
        //        LogicalCriteria = LOGICAL_CRITERIA.NULL
        //    };

        //    alertTriggerCriteria.Add(alertTriggerCriterion);

        //    //for each new object check if token exits, timeframe exits,so that there are no extra objects

        //    alertTriggerCriterion = new AlertTriggerCriterion
        //    {
        //        InstrumentToken = 260105,
        //        LHSTimeInMinutes = 5,
        //        LHSIndicator = alertTriggerCriteria[0].RHSIndicator,// new ExponentialMovingAverage(32),
        //        RHSTimeInMinutes = 15,
        //        RHSIndicator = new ExponentialMovingAverage(32),
        //        MathOperator = MATH_OPERATOR.GREATER_THAN,
        //        LogicalCriteria = LOGICAL_CRITERIA.NULL
        //    };

        //    alertTriggerCriteria.Add(alertTriggerCriterion);

        //    alertTriggerCriterion = new AlertTriggerCriterion
        //    {
        //        InstrumentToken = 260105,
        //        LHSTimeInMinutes = 3,
        //        LHSIndicator = alertTriggerCriteria[0].LHSIndicator,
        //        RHSTimeInMinutes = 15,
        //        RHSIndicator = alertTriggerCriteria[1].RHSIndicator,
        //        MathOperator = MATH_OPERATOR.GREATER_THAN,
        //        LogicalCriteria = LOGICAL_CRITERIA.NULL
        //    };

        //    alertTriggerCriteria.Add(alertTriggerCriterion);

        //    return alertTriggerCriteria;
        //}

        /// <summary>
        /// Load active alert criteria
        /// The database will be the sounce for both the data. Alert trigger data and alert trigger criteria
        /// </summary>
        //public static List<AlertCriterion> LoadAlertCriteria()
        //{

        //    ////Parameters:
        //    ////List of active parameters based on active users with funded accounts
        //    ////All these should be pull from database
        //    ////List of all time frames
        //    ////List of all indicators
        //    ////subscribers should generate events to event bus based on which alerts would be sent

        //    //List<TimeSpan> lhs_timeFrames = new List<TimeSpan> { new TimeSpan(0, 1, 0), new TimeSpan(0, 3, 0), new TimeSpan(0, 5, 0) };

        //    ////These indicators should be based on some index, so that when database index is pulled, relevant indicator is formed. Similar to AlgoIndex.
        //    //List<IIndicator> lhs_indicators = new List<IIndicator> { new ExponentialMovingAverage(32), new RelativeStrengthIndex() };

        //    //List<TimeSpan> rhs_timeFrames = new List<TimeSpan> { new TimeSpan(0, 1, 0), new TimeSpan(0, 3, 0), new TimeSpan(0, 5, 0) };
        //    //List<IIndicator> rhs_indicators = new List<IIndicator> { new ExponentialMovingAverage(32), new RelativeStrengthIndex() };

        //    ////There should be some logic to determine how many subscibers and how many alert services needed to cover for all the symbols
        //    ////that eligible users have selected
        //    //List<uint> instrumentTokens = new List<uint> { 260105, 256265, 257801 };

        //    List<AlertTriggerCriterion> alertTriggerCriteria = RetrieveAlertTriggerData();

        //    return alertTriggerCriteria;

        //}

        /// <summary>
        /// Load active alert criteria
        /// The database will be the sounce for both the data. Alert trigger data and alert trigger criteria
        /// </summary>
        //public static Dictionary<uint, AlertInstrumentTokenWithIndicators> LoadAlertTriggerData(List<AlertTriggerCriterion>  alertTriggerCriteria)
        //{

        //    //Parameters:
        //    //List of active parameters based on active users with funded accounts
        //    //All these should be pull from database
        //    //List of all time frames
        //    //List of all indicators
        //    //subscribers should generate events to event bus based on which alerts would be sent

        //    //Instruments->Timeframes->Indicators

        //    List<uint> InstrumentTokenList = new List<uint>();
        //    List<int> timeframes = alertTriggerCriteria.Select(_=>_.LHSTimeInMinutes).ToList();
        //    timeframes.AddRange(alertTriggerCriteria.Select(_ => _.RHSTimeInMinutes).ToList());

        //    List<IIndicator> indicators = new List<IIndicator>();
        //    List<TimeFrameWithIndicators> timeFrameWithIndicators = new List<TimeFrameWithIndicators>();

        //    Dictionary<uint, AlertInstrumentTokenWithIndicators> alertTriggerData = new Dictionary<uint, AlertInstrumentTokenWithIndicators>();

        //    AlertInstrumentTokenWithIndicators alertTokensWithIndicators = new AlertInstrumentTokenWithIndicators();
        //    alertTokensWithIndicators.TimeFrameWithIndicators = timeFrameWithIndicators;
        //    alertTokensWithIndicators.Instrument = new Instrument();

        //    foreach (var alertTriggerCriterion in alertTriggerCriteria)
        //    {
        //        if(!alertTriggerData.ContainsKey(alertTriggerCriterion.InstrumentToken))
        //        {
        //            //InstrumentTokenList.Add(alertTriggerCriterion.InstrumentToken);

        //            //LHS Indicator
        //            indicators = new List<IIndicator> { alertTriggerCriterion.LHSIndicator };
        //            alertTokensWithIndicators = new AlertInstrumentTokenWithIndicators();
        //            timeFrameWithIndicators = new List<TimeFrameWithIndicators>
        //            {
        //                new TimeFrameWithIndicators {TimeFrame = new TimeSpan(0, alertTriggerCriterion.LHSTimeInMinutes, 0), Indicators = indicators },
        //            };

        //            alertTokensWithIndicators.TimeFrameWithIndicators = timeFrameWithIndicators;
        //            alertTokensWithIndicators.Instrument = new Instrument();

        //            //alertTriggerData.Add(alertTriggerCriterion.InstrumentToken, alertTokensWithIndicators);

        //            //RHS Indicator

        //            TimeFrameWithIndicators tfIndicators = alertTokensWithIndicators.TimeFrameWithIndicators.Find(_ => _.TimeFrame.Minutes == alertTriggerCriterion.RHSTimeInMinutes);

        //            if(tfIndicators != null)
        //            {
        //                tfIndicators.Indicators.Add(alertTriggerCriterion.RHSIndicator);
        //            }
        //            else
        //            {
        //                indicators = new List<IIndicator> { alertTriggerCriterion.RHSIndicator };
        //                tfIndicators = new TimeFrameWithIndicators { TimeFrame = new TimeSpan(0, alertTriggerCriterion.RHSTimeInMinutes, 0), Indicators = indicators };
        //                alertTokensWithIndicators.TimeFrameWithIndicators.Add(tfIndicators);
        //            }

        //            alertTriggerData.Add(alertTriggerCriterion.InstrumentToken, alertTokensWithIndicators);
        //        }
        //        else 
        //        {
        //            AlertInstrumentTokenWithIndicators alertInstrumentTokenWithIndicators = alertTriggerData[alertTriggerCriterion.InstrumentToken];

        //            //For same instrument , check time frames and for each time frame check indicators

        //            timeFrameWithIndicators = alertInstrumentTokenWithIndicators.TimeFrameWithIndicators;

        //            bool lhsTimeFrameExists = false;
        //            bool rhsTimeFrameExists = false;
        //            for (global::System.Int32 i = 0; i < timeFrameWithIndicators.Count; i++)
        //            {
        //                //LHS Indicator
        //                lhsTimeFrameExists  = lhsTimeFrameExists || AddIndicatorWithTimeFrame(timeFrameWithIndicators[i], alertTriggerCriterion.LHSTimeInMinutes, alertTriggerCriterion.LHSIndicator);
        //                //RHS Indicator
        //                rhsTimeFrameExists = rhsTimeFrameExists || AddIndicatorWithTimeFrame(timeFrameWithIndicators[i], alertTriggerCriterion.RHSTimeInMinutes, alertTriggerCriterion.RHSIndicator);
        //            }

        //            if(!lhsTimeFrameExists)
        //            {
        //                AddIndicatorWithTimeFrame(timeFrameWithIndicators, alertTriggerCriterion.LHSTimeInMinutes, alertTriggerCriterion.LHSIndicator);
        //            }
        //            if (!rhsTimeFrameExists)
        //            {
        //                AddIndicatorWithTimeFrame(timeFrameWithIndicators, alertTriggerCriterion.RHSTimeInMinutes, alertTriggerCriterion.RHSIndicator);
        //            }
        //        }
        //    }
        //    return alertTriggerData;

        //}
        private static bool AddIndicatorWithTimeFrame(TimeFrameWithIndicators timeFrameWithIndicators, int timeFrameinMinutes, IIndicator indicator)
        {
            bool timeFramePresent = false;
            if (timeFrameWithIndicators.TimeFrame.Minutes == timeFrameinMinutes)
            {
                timeFramePresent = true;
                bool indicatorExists = false;
                foreach (IIndicator i in timeFrameWithIndicators.Indicators)
                {
                    if (i == indicator)
                    {
                        indicatorExists = true;
                        break;
                    }
                }
                if (!indicatorExists)
                {
                    timeFrameWithIndicators.Indicators.Add(indicator);
                }
            }
            return timeFramePresent;
        }
        private static void AddIndicatorWithTimeFrame(List<TimeFrameWithIndicators> timeFrameWithIndicators, int timeFrameinMinutes, IIndicator indicator)
        {
            timeFrameWithIndicators.Add(
                            new TimeFrameWithIndicators
                            {
                                TimeFrame = new TimeSpan(0, timeFrameinMinutes, 0),
                                Indicators = new List<IIndicator>
                                {
                                    indicator
                                }
                            });
        }

        public class TriggerredAlertData
        {
            public List<IIndicator> Indicators { get; set; }

            public List<int> TimeFramesinMinutes { get; set; }

            public List<uint> InstrumentTokens { get; set; }

            public TriggerredAlertData()
            {
                Indicators = new List<IIndicator>();
                TimeFramesinMinutes = new List<int>();
                InstrumentTokens = new List<uint>();
            }
        }
        public class AlertInstrumentTokenWithIndicators
        {
            public Instrument Instrument { get; set; }
            public List<TimeFrameWithIndicators> TimeFrameWithIndicators { get; set; }
        }
        public class TimeFrameWithIndicators
        {
            public TimeSpan TimeFrame { get; set; }
            public List<IIndicator> Indicators { get; set; }
        }
        /// <summary>
        /// This is a different view of alert trigger criteria or selector.
        /// This has list of all indicators, stock universe, parameters etc so that subscribers can generate trigger data.
        /// This should be based on heirarchy InstrumentTokens -> Indicators-> Timeframes
        /// </summary>



        public static void SendData()
        {
            AlertPublisher.Publish("Market Alert", "BNF Crossed CPR");
        }

        public static void LoadKotakTokens()
        {
            KoConnect.Login();
            List<Instrument> instruments = ZObjects.kotak.GetInstruments(Exchange: "NFO");
            MarketDAO dao = new MarketDAO();
            dao.StoreInstrumentList(instruments);

            instruments = ZObjects.kite.GetInstruments(Exchange: "NSE");
            dao = new MarketDAO();
            dao.StoreInstrumentList(instruments);
        }
        

    }

    public static class KestrelServerOptionsExtensions
    {
        public static void ConfigureEndpoints(this KestrelServerOptions options)
        {
            var configuration = options.ApplicationServices.GetRequiredService<IConfiguration>();
            var environment = options.ApplicationServices.GetRequiredService<IHostingEnvironment>();

            var endpoints = configuration.GetSection("HttpServer:Endpoints")
                .GetChildren()
                .ToDictionary(section => section.Key, section =>
                {
                    var endpoint = new EndpointConfiguration();
                    section.Bind(endpoint);
                    return endpoint;
                });

            foreach (var endpoint in endpoints)
            {
                var config = endpoint.Value;
                var port = config.Port ?? (config.Scheme == "https" ? 443 : 80);

                var ipAddresses = new List<IPAddress>();
                if (config.Host == "localhost")
                {
                    ipAddresses.Add(IPAddress.IPv6Loopback);
                    ipAddresses.Add(IPAddress.Loopback);
                }
                else if (IPAddress.TryParse(config.Host, out var address))
                {
                    ipAddresses.Add(address);
                }
                else
                {
                    ipAddresses.Add(IPAddress.IPv6Any);
                }

                foreach (var address in ipAddresses)
                {
                    options.Listen(address, port,
                        listenOptions =>
                        {
                            if (config.Scheme == "https")
                            {
                                var certificate = LoadCertificate(config, environment);
                                listenOptions.UseHttps(certificate);
                                listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
                            }
                        });
                }
            }
        }

        private static X509Certificate2 LoadCertificate(EndpointConfiguration config, IHostingEnvironment environment)
        {
            if (config.StoreName != null && config.StoreLocation != null)
            {
                using (var store = new X509Store(config.StoreName, Enum.Parse<StoreLocation>(config.StoreLocation)))
                {
                    store.Open(OpenFlags.ReadOnly);
                    var certificate = store.Certificates.Find(
                        X509FindType.FindBySubjectName,
                        config.Host,
                        validOnly: !environment.IsDevelopment());

                    if (certificate.Count == 0)
                    {
                        throw new InvalidOperationException($"Certificate not found for {config.Host}.");
                    }

                    return certificate[0];
                }
            }

            if (config.FilePath != null && config.Password != null)
            {
                return new X509Certificate2(config.FilePath, config.Password);
            }

            throw new InvalidOperationException("No valid certificate configuration found for the current endpoint.");
        }
    }

    public class EndpointConfiguration
    {
        public string Host { get; set; }
        public int? Port { get; set; }
        public string Scheme { get; set; }
        public string StoreName { get; set; }
        public string StoreLocation { get; set; }
        public string FilePath { get; set; }
        public string Password { get; set; }
    }

    public class IndicatorConverterWithTypeDiscriminator : JsonConverter<IIndicator>
    {
        enum TypeDiscriminator
        {
            EMA = 1,
            SMA = 2,
            RSI = 3,
            MACD = 4
        }

        public override bool CanConvert(Type typeToConvert) =>
            typeof(IIndicator).IsAssignableFrom(typeToConvert);

        public override IIndicator Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            IIndicator person = null;

            while (reader.Read())
            { 
                
                //reader.Read();
                if (reader.TokenType != JsonTokenType.PropertyName && reader.TokenType != JsonTokenType.EndObject)
                {
                    continue;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {

                
                    string? propertyName = reader.GetString();
                    if (propertyName == "Indicator")// || propertyName == "Indicator")
                    {


                        //if (propertyName != "LHSIndicator" && propertyName != "RHSIndicator")
                        //{
                        //    throw new JsonException();
                        //}

                        reader.Read();
                        if (reader.TokenType != JsonTokenType.Number)
                        {
                            //throw new JsonException();
                        }

                        TypeDiscriminator typeDiscriminator = (TypeDiscriminator)Int32.Parse(reader.GetString()); // GetInt32();

                        person = typeDiscriminator switch
                        {
                            TypeDiscriminator.EMA => new ExponentialMovingAverage(),
                            TypeDiscriminator.SMA => new SimpleMovingAverage(),
                            TypeDiscriminator.RSI => new RelativeStrengthIndex(),
                            TypeDiscriminator.MACD => new MovingAverageConvergenceDivergence(),
                            _ => throw new JsonException()
                        };

                    }
                    

                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        propertyName = reader.GetString();
                        reader.Read();
                        switch (propertyName)
                        {
                            //case "CreditLimit":
                            //    decimal creditLimit = reader.GetDecimal();
                            //    ((Customer)person).CreditLimit = creditLimit;
                            //    break;
                            //case "OfficeNumber":
                            //    string? officeNumber = reader.GetString();
                            //    ((Employee)person).OfficeNumber = officeNumber;
                            //    break;
                            //case "Name":
                            //    string? name = reader.GetString();
                            //    person.Name = name;
                            //    break;
                        }
                    }

                }
                else if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return person;
                }
            }
            throw new JsonException();
        }

        public override void Write(
            Utf8JsonWriter writer, IIndicator person, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            if (person is ExponentialMovingAverage customer)
            {
                
                writer.WriteString("Indicator", Convert.ToString((int)TypeDiscriminator.EMA));
                //writer.WriteNumber("CreditLimit", customer.CreditLimit);
            }
            else if (person is SimpleMovingAverage employee)
            {
                writer.WriteString("Indicator", Convert.ToString((int)TypeDiscriminator.SMA));
                //writer.WriteString("OfficeNumber", employee.OfficeNumber);
            }
            else if (person is RelativeStrengthIndex r)
            {
                writer.WriteString("Indicator", Convert.ToString((int)TypeDiscriminator.RSI));
                //writer.WriteString("OfficeNumber", employee.OfficeNumber);
            }
            else if (person is MovingAverageConvergenceDivergence s)
            {
                writer.WriteString("Indicator", Convert.ToString((int)TypeDiscriminator.MACD));
                //writer.WriteString("OfficeNumber", employee.OfficeNumber);
            }

            //writer.WriteString("Name", person.Name);

            writer.WriteEndObject();
        }
    }

    public class PolymorphicTypeResolver : DefaultJsonTypeInfoResolver
    {
        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

            Type basePointType = typeof(IIndicator);
            if (jsonTypeInfo.Type == basePointType)
            {
                jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                {
                    TypeDiscriminatorPropertyName = "LHSIndicator",
                    IgnoreUnrecognizedTypeDiscriminators = true,
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                    DerivedTypes =
                    {
                        new JsonDerivedType(typeof(ExponentialMovingAverage), "1"),
                        new JsonDerivedType(typeof(SimpleMovingAverage), "2"),
                        new JsonDerivedType(typeof(RelativeStrengthIndex), "3"),
                        new JsonDerivedType(typeof(MovingAverageConvergenceDivergence), "4")
                    }
                };
            }

            return jsonTypeInfo;
        }
    }
    public class TypeNameSerializationBinder : SerializationBinder
    {
        public string TypeFormat { get; private set; }

        public TypeNameSerializationBinder(string typeFormat)
        {
            TypeFormat = typeFormat;
        }

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = null;
            typeName = serializedType.Name;
        }

        public override Type BindToType(string assemblyName, string typeName)
        {
            string resolvedTypeName = string.Format(TypeFormat, typeName);

            return Type.GetType(resolvedTypeName, true);
        }
    }

}

