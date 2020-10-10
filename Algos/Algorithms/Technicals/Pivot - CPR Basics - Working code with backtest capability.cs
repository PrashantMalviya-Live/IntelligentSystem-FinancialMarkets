using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AdvanceAlgos.Utilities;
using Confluent.Kafka;
using KafkaStreams;
using Global;
using KiteConnect;
using ZConnectWrapper;
//using Pub_Sub;
using MarketDataTest;
using System.Data;

namespace Algos.TLogics
{
    public class PivotCPRBasics : IObserver<Tick[]>
    {
        SortedDictionary<int, PivotStrategy> ActivePivotStrategies = new SortedDictionary<int, PivotStrategy>();
        DataTable PivotTriggersTable;
        public readonly TimeSpan CLOSING_BELL = new TimeSpan(15, 20, 00);

        public PivotCPRBasics()
        {
            //LoadActiveData();

            TestLoadActiveData();
        }
        public virtual void OnNext(Tick[] ticks)
        {
            lock (ActivePivotStrategies)
            {
                for (int i = 0; i < ActivePivotStrategies.Count; i++)
                {
                    PivotStrategy pivotStrategy = ActivePivotStrategies.ElementAt(i).Value;

                    //TODO: This is ONLY FOR Backtest and workwith TestGetPivotInstrument stored procedure. Change procedure in case of live enviroment.
                    if (pivotStrategy.SubInstruments.ElementAt(0).Value.LastTradeTime.AddDays(1).Date != ticks[0].LastTradeTime.Value.Date)
                    {
                        continue;
                    }

                    TradePivots(pivotStrategy, ticks);
                }
            }
        }

        private void TradePivots(PivotStrategy pivotStrategy, Tick[] ticks)
        {
           // PivotInstrument primaryPivotInstrument = pivotStrategy.PrimaryInstrument;
            
            //Instrument instrument = primaryPivotInstrument.Instrument;

            //#region Trade Price Update
            //Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == instrument.InstrumentToken);
            //if (baseInstrumentTick.LastPrice != 0)
            //{
            //    instrument.LastPrice = baseInstrumentTick.LastPrice;
            //    primaryPivotInstrument.Instrument = instrument;
            //    pivotStrategy.PrimaryInstrument = primaryPivotInstrument;
            //}
            //else { return; }
            //#endregion


            ///Logic: If BPR or UPR is crossed buy/sell with SL
            ///If R1, S1 is crossed , increase lots with SL

            ///TODO:Below needs to come from database and initial inputs
            //pivotInstrument.TradedLot = 0;
            //pivotInstrument.MaximumTradeLot = 1;
            //pivotInstrument.TradingUnitInLots = 1;

            //decimal nearestStrikePrice = 0, triggerPrice = 0;
            //Pivot pivot = null;
            //Instrument instrumentToTrade;
            //Market is up. As long as it is above pivot
            TradeTriggers(pivotStrategy, ticks);

            #region commented code
            //else if (lastPrice <= cpr.R[0].Price[Constants.HIGH] && currentPrice > cpr.R[0].Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.R[0].Price[Constants.HIGH];
            //    pivot = cpr.R[0];
            //}
            //else if (lastPrice <= cpr.R[1].Price[Constants.HIGH] && currentPrice > cpr.R[1].Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.R[1].Price[Constants.HIGH];
            //    pivot = cpr.R[1];
            //}
            //else if (lastPrice <= cpr.R[2].Price[Constants.HIGH] && currentPrice > cpr.R[2].Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.R[2].Price[Constants.HIGH];
            //    pivot = cpr.R[2];
            //}
            //else if (lastPrice >= cpr.S[0].Price[Constants.LOW] && currentPrice < cpr.S[0].Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.S[0].Price[Constants.LOW];
            //    pivot = cpr.S[0];
            //}
            //else if (lastPrice >= cpr.S[1].Price[Constants.LOW] && currentPrice < cpr.S[1].Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.S[1].Price[Constants.LOW];
            //    pivot = cpr.S[1];
            //}
            //else if (lastPrice >= cpr.S[2].Price[Constants.LOW] && currentPrice < cpr.S[2].Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.S[2].Price[Constants.LOW];
            //    pivot = cpr.S[2];
            //}

            //    if (triggerPrice != 0 && Math.Abs(pivotInstrument.TradedLot) < pivotInstrument.MaximumTradeLot)
            //{
            //    triggerPrice = 0;
            //    nearestStrikePrice = Math.Round(triggerPrice / 100, 0, MidpointRounding.AwayFromZero) * 100;

            //    //Buy instrument
            //    ShortTrade trade = PlaceOrder(pivot.InstrumentToTrade.TradingSymbol, true, currentPrice,
            //        Convert.ToInt32(primaryInstrument.LotSize) * pivotInstrument.TradingUnitInLots,
            //        baseInstrumentTick.Timestamp, pivot.InstrumentToTrade.InstrumentToken);
            //    pivotInstrument.TradedLot += Convert.ToInt32(trade.Quantity / primaryInstrument.LotSize);
            //    //pivotInstrument.LastTradedPrice = trade.AveragePrice;

            //    //pivot trade instrument for which this traded instrument is primary should be sent
            //    UpdateTradeDetails(pivotInstrument, trade);
            //}

            //else if (lastPrice >= cpr.CentralPivot.Price[Constants.LOW] && currentPrice < cpr.CentralPivot.Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.CentralPivot.Price[Constants.LOW];
            //    pivot = cpr.CentralPivot;
            //}
            //else if (lastPrice >= cpr.R[0].Price[Constants.LOW] && currentPrice < cpr.R[0].Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.R[0].Price[Constants.LOW];
            //    pivot = cpr.R[0];
            //}
            //else if (lastPrice >= cpr.R[1].Price[Constants.LOW] && currentPrice < cpr.R[1].Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.R[1].Price[Constants.LOW];
            //    pivot = cpr.R[1];
            //}
            //else if (lastPrice >= cpr.R[2].Price[Constants.LOW] && currentPrice < cpr.R[2].Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.R[2].Price[Constants.LOW];
            //    pivot = cpr.R[2];
            //}
            //else if (lastPrice <= cpr.S[0].Price[Constants.HIGH] && currentPrice > cpr.S[0].Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.S[0].Price[Constants.HIGH];
            //    pivot = cpr.S[0];
            //}
            //else if (lastPrice <= cpr.S[1].Price[Constants.HIGH] && currentPrice > cpr.S[1].Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.S[1].Price[Constants.HIGH];
            //    pivot = cpr.S[1];
            //}
            //else if (lastPrice <= cpr.S[2].Price[Constants.HIGH] && currentPrice > cpr.S[2].Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.S[2].Price[Constants.HIGH];
            //    pivot = cpr.S[2];
            //}

            //if (triggerPrice != 0 && pivotInstrument.TradedLot >= pivotInstrument.TradingUnitInLots)
            //{
            //    triggerPrice = 0;
            //    nearestStrikePrice = Math.Round(triggerPrice / 100, 0, MidpointRounding.AwayFromZero) * 100;

            //    //Close instrument
            //    ShortTrade trade = PlaceOrder(pivot.InstrumentToTrade.TradingSymbol, false, currentPrice,
            //        Convert.ToInt32(primaryInstrument.LotSize) * pivotInstrument.TradingUnitInLots,
            //        baseInstrumentTick.Timestamp, pivot.InstrumentToTrade.InstrumentToken);
            //    pivotInstrument.TradedLot -= Convert.ToInt32(trade.Quantity / primaryInstrument.LotSize);
            //    //pivotInstrument.LastTradedPrice = trade.AveragePrice;

            //    //pivot trade instrument for which this traded instrument is primary should be sent
            //    UpdateTradeDetails(pivotInstrument, trade);
            //}

            //}

            //if (currentBasePrice > cpr.CentralPivot.Price[Constants.HIGH] && currentBasePrice < cpr.R[0].Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.CentralPivot.Price[Constants.CURRENT];
            //    instrumentToTrade = cpr.CentralPivot.InstrumentToTrade;
            //}
            //else if (currentBasePrice > cpr.R[0].Price[Constants.HIGH] && currentBasePrice < cpr.R[1].Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.R[0].Price[Constants.CURRENT];
            //    instrumentToTrade = cpr.R[0].InstrumentToTrade;
            //}
            //else if (currentBasePrice > cpr.R[1].Price[Constants.HIGH] && currentBasePrice < cpr.R[2].Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.R[1].Price[Constants.CURRENT];
            //    instrumentToTrade = cpr.R[1].InstrumentToTrade;
            //}
            //else if (currentBasePrice > cpr.R[2].Price[Constants.HIGH] && currentBasePrice < cpr.R[3].Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.R[2].Price[Constants.CURRENT];
            //    instrumentToTrade = cpr.R[2].InstrumentToTrade;
            //}
            //else if (currentBasePrice < cpr.CentralPivot.Price[Constants.LOW] && currentBasePrice > cpr.S[0].Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.CentralPivot.Price[Constants.CURRENT];
            //}
            //else if (currentBasePrice < cpr.S[0].Price[Constants.LOW] && currentBasePrice < cpr.S[1].Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.S[0].Price[Constants.CURRENT];
            //    instrumentToTrade = cpr.S[0].InstrumentToTrade;
            //}
            //else if (currentBasePrice < cpr.S[1].Price[Constants.LOW] && currentBasePrice < cpr.S[2].Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.S[1].Price[Constants.CURRENT];
            //    instrumentToTrade = cpr.S[1].InstrumentToTrade;
            //}
            //else if (currentBasePrice < cpr.S[2].Price[Constants.LOW] && currentBasePrice < cpr.S[3].Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.S[2].Price[Constants.CURRENT];
            //    instrumentToTrade = cpr.S[2].InstrumentToTrade;
            //}

            ////Based on trigger price, then the logic for trade will be tested.

            //if (lastBasePrice <= cpr.CentralPivot.Price[Constants.HIGH] && currentPrice > cpr.CentralPivot.Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.CentralPivot.Price[Constants.HIGH];
            //    pivot = cpr.CentralPivot;
            //}
            //else if (lastPrice <= cpr.R[0].Price[Constants.HIGH] && currentPrice > cpr.R[0].Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.R[0].Price[Constants.HIGH];
            //    pivot = cpr.R[0];
            //}
            //else if (lastPrice <= cpr.R[1].Price[Constants.HIGH] && currentPrice > cpr.R[1].Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.R[1].Price[Constants.HIGH];
            //    pivot = cpr.R[1];
            //}
            //else if (lastPrice <= cpr.R[2].Price[Constants.HIGH] && currentPrice > cpr.R[2].Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.R[2].Price[Constants.HIGH];
            //    pivot = cpr.R[2];
            //}
            //else if (lastPrice >= cpr.S[0].Price[Constants.LOW] && currentPrice < cpr.S[0].Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.S[0].Price[Constants.LOW];
            //    pivot = cpr.S[0];
            //}
            //else if (lastPrice >= cpr.S[1].Price[Constants.LOW] && currentPrice < cpr.S[1].Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.S[1].Price[Constants.LOW];
            //    pivot = cpr.S[1];
            //}
            //else if (lastPrice >= cpr.S[2].Price[Constants.LOW] && currentPrice < cpr.S[2].Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.S[2].Price[Constants.LOW];
            //    pivot = cpr.S[2];
            //}

            //if (triggerPrice != 0 && Math.Abs(pivotInstrument.TradedLot) < pivotInstrument.MaximumTradeLot)
            //{
            //    triggerPrice = 0;
            //    nearestStrikePrice = Math.Round(triggerPrice / 100, 0, MidpointRounding.AwayFromZero) * 100;

            //    //Buy instrument
            //    ShortTrade trade = PlaceOrder(pivot.InstrumentToTrade.TradingSymbol, true, currentPrice,
            //        Convert.ToInt32(primaryInstrument.LotSize) * pivotInstrument.TradingUnitInLots,
            //        baseInstrumentTick.Timestamp, pivot.InstrumentToTrade.InstrumentToken);
            //    pivotInstrument.TradedLot += Convert.ToInt32(trade.Quantity / primaryInstrument.LotSize);
            //    //pivotInstrument.LastTradedPrice = trade.AveragePrice;

            //    //pivot trade instrument for which this traded instrument is primary should be sent
            //    UpdateTradeDetails(pivotInstrument, trade);
            //}

            //else if (lastPrice >= cpr.CentralPivot.Price[Constants.LOW] && currentPrice < cpr.CentralPivot.Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.CentralPivot.Price[Constants.LOW];
            //    pivot = cpr.CentralPivot;
            //}
            //else if (lastPrice >= cpr.R[0].Price[Constants.LOW] && currentPrice < cpr.R[0].Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.R[0].Price[Constants.LOW];
            //    pivot = cpr.R[0];
            //}
            //else if (lastPrice >= cpr.R[1].Price[Constants.LOW] && currentPrice < cpr.R[1].Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.R[1].Price[Constants.LOW];
            //    pivot = cpr.R[1];
            //}
            //else if (lastPrice >= cpr.R[2].Price[Constants.LOW] && currentPrice < cpr.R[2].Price[Constants.LOW])
            //{
            //    triggerPrice = cpr.R[2].Price[Constants.LOW];
            //    pivot = cpr.R[2];
            //}
            //else if (lastPrice <= cpr.S[0].Price[Constants.HIGH] && currentPrice > cpr.S[0].Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.S[0].Price[Constants.HIGH];
            //    pivot = cpr.S[0];
            //}
            //else if (lastPrice <= cpr.S[1].Price[Constants.HIGH] && currentPrice > cpr.S[1].Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.S[1].Price[Constants.HIGH];
            //    pivot = cpr.S[1];
            //}
            //else if (lastPrice <= cpr.S[2].Price[Constants.HIGH] && currentPrice > cpr.S[2].Price[Constants.HIGH])
            //{
            //    triggerPrice = cpr.S[2].Price[Constants.HIGH];
            //    pivot = cpr.S[2];
            //}

            //if (triggerPrice != 0 && pivotInstrument.TradedLot >= pivotInstrument.TradingUnitInLots)
            //{
            //    triggerPrice = 0;
            //    nearestStrikePrice = Math.Round(triggerPrice / 100, 0, MidpointRounding.AwayFromZero) * 100;

            //    //Close instrument
            //    ShortTrade trade = PlaceOrder(pivot.InstrumentToTrade.TradingSymbol, false, currentPrice,
            //        Convert.ToInt32(primaryInstrument.LotSize) * pivotInstrument.TradingUnitInLots,
            //        baseInstrumentTick.Timestamp, pivot.InstrumentToTrade.InstrumentToken);
            //    pivotInstrument.TradedLot -= Convert.ToInt32(trade.Quantity / primaryInstrument.LotSize);
            //    //pivotInstrument.LastTradedPrice = trade.AveragePrice;

            //    //pivot trade instrument for which this traded instrument is primary should be sent
            //    UpdateTradeDetails(pivotInstrument, trade);
            //}

            //// Initial Trade & Stop Loss fir Pivot
            //if (lastPrice != 0 && (((lastPrice <= cpr.CentralPivot.Price[Constants.HIGH] && currentPrice > cpr.CentralPivot.Price[Constants.HIGH])
            //    || (lastPrice <= cpr.R[0].Price[Constants.HIGH] && currentPrice > cpr.R[0].Price[Constants.HIGH])
            //    || (lastPrice <= cpr.R[1].Price[Constants.HIGH] && currentPrice > cpr.R[1].Price[Constants.HIGH])
            //    || (lastPrice <= cpr.R[2].Price[Constants.HIGH] && currentPrice > cpr.R[2].Price[Constants.HIGH]))
            //    ||
            //    (((lastPrice <= cpr.S[0].Price[Constants.HIGH] && currentPrice > cpr.S[0].Price[Constants.HIGH])
            //    || (lastPrice <= cpr.S[1].Price[Constants.HIGH] && currentPrice > cpr.S[1].Price[Constants.HIGH])
            //    || (lastPrice <= cpr.S[2].Price[Constants.HIGH] && currentPrice > cpr.S[2].Price[Constants.HIGH])))

            //    ))
            //{
            //    if (Math.Abs(pivotInstrument.TradedLot) < pivotInstrument.MaximumTradeLot)
            //    {
            //        //Buy instrument
            //        ShortTrade trade = PlaceOrder(primaryInstrument.TradingSymbol, true, currentPrice,
            //            Convert.ToInt32(primaryInstrument.LotSize) * pivotInstrument.TradingUnitInLots,
            //            baseInstrumentTick.Timestamp, primaryInstrument.InstrumentToken);
            //        pivotInstrument.TradedLot += Convert.ToInt32(trade.Quantity / primaryInstrument.LotSize);
            //        pivotInstrument.LastTradedPrice = trade.AveragePrice;

            //        //pivot trade instrument for which this traded instrument is primary should be sent
            //        UpdateTradeDetails(pivotInstrument, trade);
            //    }
            //}
            //else if (lastPrice != 0 && (((lastPrice >= cpr.CentralPivot.Price[Constants.LOW] && currentPrice < cpr.CentralPivot.Price[Constants.LOW])
            //    || (lastPrice >= cpr.S[0].Price[Constants.LOW] && currentPrice < cpr.S[0].Price[Constants.LOW])
            //    || (lastPrice >= cpr.S[1].Price[Constants.LOW] && currentPrice < cpr.S[1].Price[Constants.LOW])
            //    || (lastPrice >= cpr.S[2].Price[Constants.LOW] && currentPrice < cpr.S[2].Price[Constants.LOW]))

            //    ||

            //    (((lastPrice >= cpr.R[0].Price[Constants.LOW] && currentPrice < cpr.R[0].Price[Constants.LOW])
            //    || (lastPrice >= cpr.R[1].Price[Constants.LOW] && currentPrice < cpr.R[1].Price[Constants.LOW])
            //    || (lastPrice >= cpr.R[2].Price[Constants.LOW] && currentPrice < cpr.R[2].Price[Constants.LOW])))
            //    ))
            //{
            //    if (pivotInstrument.TradedLot >= pivotInstrument.MaximumTradeLot)
            //    {
            //        //Buy instrument
            //        ShortTrade trade = PlaceOrder(primaryInstrument.TradingSymbol, false, currentPrice,
            //            Convert.ToInt32(primaryInstrument.LotSize) * pivotInstrument.TradingUnitInLots,
            //            baseInstrumentTick.Timestamp, primaryInstrument.InstrumentToken);
            //        pivotInstrument.TradedLot += Convert.ToInt32(trade.Quantity / primaryInstrument.LotSize);
            //        pivotInstrument.LastTradedPrice = trade.AveragePrice;

            //        //pivot trade instrument for which this traded instrument is primary should be sent
            //        UpdateTradeDetails(pivotInstrument, trade);
            //    }
            //}
            #endregion
        }

        /// <summary>
        /// Buy and sell depending on CPR up and down and with trailing stoplosses
        /// </summary>
        /// <param name="pivotStrategy"></param>
        /// <param name="ticks"></param>
        private void TradeTriggers(PivotStrategy pivotStrategy, Tick[] ticks)
        {
            decimal lastPrice = 0, currentPrice = 0;

            foreach (KeyValuePair<string, PivotInstrument> subPivotInstrument in pivotStrategy.SubInstruments)
            {
                PivotInstrument subPI = subPivotInstrument.Value;
                Instrument subInstrument = subPI.Instrument;
                CentralPivotRange cpr = subPI.CPR;

                Tick tick = ticks.FirstOrDefault(x => x.InstrumentToken == subInstrument.InstrumentToken);
                if (tick.LastPrice != 0)
                {
                    lastPrice = subInstrument.LastPrice;
                    subInstrument.LastPrice = currentPrice = tick.LastPrice;
                    //subPI.Instrument = subInstrument;
                    pivotStrategy.SubInstruments[subPivotInstrument.Key].Instrument = subInstrument;
                }
                else
                { continue; }
                if (lastPrice == 0)
                { continue; }
                //BUY if an instrument breaks resistance highs, Sell if an instrument breaks support lows
                //Close instrument if it is already traded and breaks resistance lows and support highs

                Decimal[,] pivotTradeTriggers = CreateTriggerMatrix(cpr);  //Store PivotTradeTriggers as a property. Why calculate it every second?
                if (Math.Abs(pivotStrategy.TradedLot) < pivotStrategy.MaximumTradeLot && !pivotStrategy.TradedInstruments.Contains(subPI))
                {
                    for (int i = 0; i < pivotTradeTriggers.GetLength(0); i++)
                    {
                        decimal startUpCross = pivotTradeTriggers[i, 2];

                        if ((startUpCross == 1 && lastPrice <= pivotTradeTriggers[i, 1] && currentPrice > pivotTradeTriggers[i, 1])
                            || (startUpCross == 0 && lastPrice >= pivotTradeTriggers[i, 1] && currentPrice < pivotTradeTriggers[i, 1])
                            )
                        {
                            //TODO: DONOT BUY ANYTHING NEAR END OF THE DAY. ALL EXISITING POSITIONS TO BE CLOSED.
                            if (tick.Timestamp.Value.TimeOfDay > CLOSING_BELL)
                            {
                                break;
                            }

                            subPI.TriggerID = i; // pivotTradeTriggers[i, 0];. This is necessary as sometimes some triggers will be set inActive.

                            pivotStrategy.TradedLot += TradeInstrument(subInstrument, pivotStrategy.TradingUnitInLots,
                                pivotStrategy.StrategyID, startUpCross == 1, currentPrice, tick.Timestamp, subPI.TriggerID);

                            pivotStrategy.TradedInstruments.Add(subPI);
                            break;
                        }
                    }
                }
                else if (pivotStrategy.TradedInstruments.Contains(subPI))
                {
                    int tradedTrigger = Convert.ToInt32(subPI.TriggerID);
                    //Determine likly closing possibilities for this trigger
                    decimal startLevel = pivotTradeTriggers[tradedTrigger, 1];
                    decimal startUpCross = pivotTradeTriggers[tradedTrigger, 2];

                    //TODO: EMERGYNCY BREAK AT THE END OF THE DAY. CLOSE ALL POSITIONS BY 3:20 PM. EVERY DAY
                    if (tick.Timestamp.Value.TimeOfDay > CLOSING_BELL)
                    {
                        pivotStrategy.TradedLot -= TradeInstrument(subInstrument, pivotStrategy.TradingUnitInLots,
                                  pivotStrategy.StrategyID, startUpCross != 1, currentPrice, tick.Timestamp, subPI.TriggerID);

                        pivotStrategy.TradedInstruments.Remove(subPI);
                        break;
                    }

                    for (int i = 0; i < pivotTradeTriggers.GetLength(0); i++)
                    {
                        if (startLevel == pivotTradeTriggers[i, 1] && startUpCross == pivotTradeTriggers[i, 2])
                        {
                            decimal closeUpCross = pivotTradeTriggers[i, 4];

                            if ((closeUpCross == 1 && lastPrice <= pivotTradeTriggers[i, 3] && currentPrice > pivotTradeTriggers[i, 3])
                                || (closeUpCross == 0 && lastPrice >= pivotTradeTriggers[i, 3] && currentPrice < pivotTradeTriggers[i, 3]))
                            {
                                pivotStrategy.TradedLot -= TradeInstrument(subInstrument, pivotStrategy.TradingUnitInLots,
                                    pivotStrategy.StrategyID, startUpCross != 1, currentPrice, tick.Timestamp, subPI.TriggerID);

                                pivotStrategy.TradedInstruments.Remove(subPI);
                                break;
                            }
                        }
                    }
                }
            }
        }

        private Decimal[,] CreateTriggerMatrix(CentralPivotRange cpr)
        {
            Decimal[,] pivotTriggers = new decimal[PivotTriggersTable.Rows.Count, PivotTriggersTable.Columns.Count];

            for (int i=0;i< PivotTriggersTable.Rows.Count;i++)
            {
                for(int j=0;j< PivotTriggersTable.Columns.Count; j++)
                {
                    pivotTriggers[i, j] = Convert.ToDecimal(PivotTriggersTable.Rows[i][j]);
                }
                pivotTriggers[i, 1] = cpr.Prices[Convert.ToInt32(pivotTriggers[i, 1])];
                pivotTriggers[i, 3] = cpr.Prices[Convert.ToInt32(pivotTriggers[i, 3])];
            }
            return pivotTriggers;
        }
        /// <summary>
        /// Buy on H on R and Sell on L on S. CPR too involved
        /// </summary>
        /// <param name="pivotStrategy"></param>
        /// <param name="ticks"></param>
        
        private int TradeInstrument(Instrument instrument, int tradingUnitInLots, int strategyID, bool buyOrder, decimal currentPrice, DateTime? timeStamp, decimal triggerID)
        {
            //Buy instrument
            ShortTrade trade = PlaceOrder(instrument.TradingSymbol, buyOrder, currentPrice,
                Convert.ToInt32(instrument.LotSize) * tradingUnitInLots,
                timeStamp, instrument.InstrumentToken);
            int tradedLot = Convert.ToInt32(trade.Quantity / instrument.LotSize);
            

            //pivot trade instrument for which this traded instrument is primary should be sent
            UpdateTradeDetails(strategyID, instrument.InstrumentToken, tradedLot, trade, Convert.ToInt32(triggerID));
            return tradedLot;
        }

        private string GenerateTradingSymbol(string baseInstrumentSymbol, decimal strikePrice, DateTime? expiry, string instrumentType)
        {
            return string.Format("{0}{1}{2}{3}", baseInstrumentSymbol.Substring(0, baseInstrumentSymbol.LastIndexOf("FUT")), expiry, strikePrice, instrumentType);
        }
        private ShortTrade PlaceOrder(string tradingSymbol, bool buyOrder, decimal currentPrice, int quantity, DateTime? tickTime = null, uint token = 0)
        {
            //Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
            //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, quantity, Product: Constants.PRODUCT_MIS,
            //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            ///TEMP, REMOVE Later
            if (currentPrice == 0)
            {
                DataLogic dl = new DataLogic();
                currentPrice = dl.RetrieveLastPrice(token, tickTime, buyOrder);
            }

            string orderId = "0";
            decimal averagePrice = 0;
            ////if (orderStatus["data"]["order_id"] != null)
            ////{
            ////    orderId = orderStatus["data"]["order_id"];
            ////}
            //if (orderId != "0")
            //{
            //    System.Threading.Thread.Sleep(200);
            //    List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
            //    averagePrice = orderInfo[orderInfo.Count - 1].AveragePrice;
            //}
            if (averagePrice == 0)
                averagePrice = buyOrder ? currentPrice + 0.2m : currentPrice - 0.2m;
            averagePrice = buyOrder ? averagePrice * -1 : averagePrice;

            ShortTrade trade = new ShortTrade();
            trade.AveragePrice = averagePrice;
            trade.ExchangeTimestamp = tickTime == null ? DateTime.Now : tickTime;
            //trade.Quantity = buyOrder ? quantity : quantity * -1;
            trade.Quantity = quantity;
            trade.OrderId = orderId;
            trade.TransactionType = buyOrder ? "Buy" : "Sell";

            return trade;
        }

        private void UpdateTradeDetails(int strategyID, uint instrumentToken, int tradedLot, ShortTrade trade, int triggerID)
        {
            DataLogic dl = new DataLogic();
            dl.UpdateTrade(strategyID, instrumentToken, trade, AlgoIndex.PivotedBasedTrade, tradedLot, triggerID);
        }
        private void LoadActiveData()
        {
            AlgoIndex algoIndex = AlgoIndex.PivotedBasedTrade;
            DataLogic dl = new DataLogic();
            DataSet activePivotInstruments = dl.RetrieveActivePivotInstruments(algoIndex);

            DataRelation strategy_Token_Relation = activePivotInstruments.Relations.Add("Strategy_Instrument", new DataColumn[] { activePivotInstruments.Tables[0].Columns["StrategyID"] },
                new DataColumn[] { activePivotInstruments.Tables[1].Columns["StrategyId"] });

            //DataRelation strategy_Trades_Relation = activePivotInstruments.Relations.Add("Instrument_Prices", new DataColumn[] { activePivotInstruments.Tables[1].Columns["InstrumentToken"] },
            //    new DataColumn[] { activePivotInstruments.Tables[2].Columns["InstrumentToken"] });

            PivotStrategy pivotStrategy = new PivotStrategy();
            pivotStrategy.TradedInstruments = new List<PivotInstrument>();
            PivotInstrument pivotInstrument = new PivotInstrument();

            foreach (DataRow piRow in activePivotInstruments.Tables[0].Rows) // Pivot Strategy Level
            {
                pivotStrategy.StrategyID = Convert.ToInt32(piRow["StrategyID"]);
                pivotStrategy.TradedLot = Convert.ToInt32(piRow["TradedLot"]);
                pivotStrategy.MaximumTradeLot = Convert.ToInt32(piRow["MaximumTradeLot"]);
                pivotStrategy.TradingUnitInLots = Convert.ToInt32(piRow["TradingUnitInLots"]);
                pivotStrategy.PivotFrequency = (PivotFrequency)piRow["PivotFrequency"];
                pivotStrategy.PivotFrequencyWindow = (int?)piRow["PivotWindow"];
                //if (strangleTokenRow["PivotFrequencyWindow"] != DBNull.Value)
                //{
                //    cpr.PivotFrequencyWindow = Convert.ToInt32(piRow["PivotWindow"]);
                //}

                foreach (DataRow strangleTokenRow in piRow.GetChildRows(strategy_Token_Relation)) //Pivot Instrument Level
                {
                    Instrument instrument = new Instrument()
                    {
                        //BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
                        InstrumentToken = Convert.ToUInt32(strangleTokenRow["InstrumentToken"]),
                        InstrumentType = (string)strangleTokenRow["Instrument_Type"],
                        Strike = (Decimal)strangleTokenRow["Strike"],
                        TradingSymbol = (string)strangleTokenRow["TradingSymbol"],
                        LotSize = Convert.ToUInt32(strangleTokenRow["Lot_size"])
                    };

                    if (strangleTokenRow["Expiry"] != DBNull.Value)
                    {
                        instrument.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);
                    }

                    CentralPivotRange cpr = new CentralPivotRange(GetOHLC(strangleTokenRow));

                    pivotInstrument = new PivotInstrument();
                    pivotInstrument.CPR = cpr;
                    pivotInstrument.StrategyID = pivotStrategy.StrategyID;
                    pivotInstrument.Instrument = instrument;

                    if ((bool)strangleTokenRow["IsPrimary"] == true)
                    {
                        pivotStrategy.PrimaryInstrument = pivotInstrument;
                    }
                    else
                    {
                        if (pivotStrategy.SubInstruments == null)
                        {
                            pivotStrategy.SubInstruments = new SortedDictionary<string, PivotInstrument>();
                        }
                        pivotStrategy.SubInstruments.Add(instrument.TradingSymbol, pivotInstrument);
                    }
                }
                ActivePivotStrategies.Add(pivotStrategy.StrategyID, pivotStrategy);
            }

            PivotTriggersTable = activePivotInstruments.Tables[2];
        }

        private void TestLoadActiveData()
        {
            AlgoIndex algoIndex = AlgoIndex.PivotedBasedTrade;
            DataLogic dl = new DataLogic();
            DataSet activePivotInstruments = dl.TestRetrieveActivePivotInstruments(algoIndex);

            DataRelation strategy_Token_Relation = activePivotInstruments.Relations.Add("Strategy_Instrument", new DataColumn[] { activePivotInstruments.Tables[0].Columns["StrategyID"] },
                new DataColumn[] { activePivotInstruments.Tables[1].Columns["StrategyId"] });

            //DataRelation strategy_Trades_Relation = activePivotInstruments.Relations.Add("Instrument_Prices", new DataColumn[] { activePivotInstruments.Tables[1].Columns["InstrumentToken"] },
            //    new DataColumn[] { activePivotInstruments.Tables[2].Columns["InstrumentToken"] });




            PivotInstrument pivotInstrument;
            foreach (DataRow piRow in activePivotInstruments.Tables[0].Rows) // Pivot Strategy Level
            {
                PivotStrategy pivotStrategy = new PivotStrategy();
                pivotStrategy.TradedInstruments = new List<PivotInstrument>();

                pivotStrategy.StrategyID = Convert.ToInt32(piRow["StrategyID"]);
                pivotStrategy.TradedLot = Convert.ToInt32(piRow["TradedLot"]);
                pivotStrategy.MaximumTradeLot = Convert.ToInt32(piRow["MaximumTradeLot"]);
                pivotStrategy.TradingUnitInLots = Convert.ToInt32(piRow["TradingUnitInLots"]);
                pivotStrategy.PivotFrequency = (PivotFrequency)piRow["PivotFrequency"];
                pivotStrategy.PivotFrequencyWindow = (int?)piRow["PivotWindow"];
                //if (strangleTokenRow["PivotFrequencyWindow"] != DBNull.Value)
                //{
                //    cpr.PivotFrequencyWindow = Convert.ToInt32(piRow["PivotWindow"]);
                //}

                foreach (DataRow strangleTokenRow in piRow.GetChildRows(strategy_Token_Relation)) //Pivot Instrument Level
                {
                    Instrument instrument = new Instrument()
                    {
                        //BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
                        InstrumentToken = Convert.ToUInt32(strangleTokenRow["InstrumentToken"]),
                        InstrumentType = (string)strangleTokenRow["Instrument_Type"],
                        Strike = (Decimal)strangleTokenRow["Strike"],
                        TradingSymbol = (string)strangleTokenRow["TradingSymbol"],
                        LotSize = Convert.ToUInt32(strangleTokenRow["Lot_size"])
                    };

                    if (strangleTokenRow["Expiry"] != DBNull.Value)
                    {
                        instrument.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);
                    }

                    CentralPivotRange cpr = new CentralPivotRange(GetOHLC(strangleTokenRow));

                    pivotInstrument = new PivotInstrument();
                    pivotInstrument.CPR = cpr;
                    pivotInstrument.StrategyID = pivotStrategy.StrategyID;
                    pivotInstrument.Instrument = instrument;

                    if (strangleTokenRow["LastTradeTime"] != null)
                    {
                        pivotInstrument.LastTradeTime = Convert.ToDateTime(strangleTokenRow["LastTradeTime"]);
                    }

                    if ((bool)strangleTokenRow["IsPrimary"] == true)
                    {
                        pivotStrategy.PrimaryInstrument = pivotInstrument;
                    }
                    else
                    {
                        if (pivotStrategy.SubInstruments == null)
                        {
                            pivotStrategy.SubInstruments = new SortedDictionary<string, PivotInstrument>();
                        }
                        pivotStrategy.SubInstruments.Add(instrument.TradingSymbol, pivotInstrument);
                    }
                }
                ActivePivotStrategies.Add(pivotStrategy.StrategyID, pivotStrategy);
            }

            PivotTriggersTable = activePivotInstruments.Tables[2];
        }
        public void StorePivotInstruments(uint primaryInst, DateTime expiry, int initialQty, int maxQty, int stepQty,
            int pivotFrequency, int pivotWindow, DateTime timeOfOrder = default(DateTime))
        {
            //Update Database
            DataLogic dl = new DataLogic();
            dl.StorePivotInstruments(primaryInst, expiry, AlgoIndex.PivotedBasedTrade, maxQty, stepQty, (PivotFrequency) pivotFrequency, pivotWindow);
        }

        private OHLC GetOHLC(DataRow ohlcRow)
        {
            OHLC ohlc = new OHLC();
            ohlc.Close = Convert.ToDecimal(ohlcRow["Close"]);
            ohlc.Open = Convert.ToDecimal(ohlcRow["Open"]);
            ohlc.High = Convert.ToDecimal(ohlcRow["High"]);
            ohlc.Low = Convert.ToDecimal(ohlcRow["Low"]);
            return ohlc;
        }

        #region Pub Sub Methods
        public IDisposable UnsubscriptionToken;
        public virtual void OnError(Exception ex)
        {
            ///TODO: Log the error. Also handle the error.
        }

        public virtual void Subscribe(Publisher publisher)
        {
            UnsubscriptionToken = publisher.Subscribe(this);
        }
        public virtual void Subscribe(Ticker publisher)
        {
            UnsubscriptionToken = publisher.Subscribe(this);
        }

        public virtual void Subscribe(TickDataStreamer publisher)
        {
            UnsubscriptionToken = publisher.Subscribe(this);
        }


        public virtual void Unsubscribe()
        {
            UnsubscriptionToken.Dispose();
        }

        public virtual void OnCompleted()
        {
        }
        #endregion
    }
}
