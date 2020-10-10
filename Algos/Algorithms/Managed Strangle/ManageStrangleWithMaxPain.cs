using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Algorithms.Utilities;
using GlobalLayer;
using KiteConnect;
using ZConnectWrapper;
//using Pub_Sub;
//using MarketDataTest;
using System.Data;

namespace Algos.TLogics
{
    public class ManageStrangleWithMaxPain : IObserver<Tick[]>
    {
        public IDisposable UnsubscriptionToken;
        List<StrangleData> ActiveStrangles = new List<StrangleData>();
        public int rowCounter = 1;
        public ManageStrangleWithMaxPain()
        {
            LoadActiveData();
        }
        private decimal UpdateMaxPainStrike(StrangleData strangleNode, Tick[] ticks)
        {
            //Check and Fill Reference Strangle Data
            //if (strikePrice == 0 || strikePrice != strangleNode.MaxPainStrike)
            //{
            bool allpopulated = PopulateReferenceStrangleData(strangleNode, ticks);
            //}
            if (!allpopulated) return 0;

            SortedList<decimal, Instrument> calls = strangleNode.Calls;
            SortedList<decimal, Instrument> puts = strangleNode.Puts;

            IList<decimal> strikePrices = calls.Keys;
            decimal pain, maxPain = 0;
            decimal maxPainStrike = 0;
            foreach (decimal expiryStrike in strikePrices)
            {
                pain = 0;
                IEnumerable<Decimal> putStrikes = strikePrices.Where(x => x < expiryStrike);
                IEnumerable<Decimal> callStrikes = strikePrices.Where(x => x > expiryStrike);

                foreach (decimal strikePrice in putStrikes)
                {
                    pain += puts[strikePrice].OI * (expiryStrike - strikePrice);
                }
                foreach (decimal strikePrice in callStrikes)
                {
                    pain += calls[strikePrice].OI * (strikePrice - expiryStrike);
                }
                if (pain < maxPain || (maxPain == 0 && pain != 0))
                {
                    maxPain = pain;
                    maxPainStrike = expiryStrike;
                }
            }

            return maxPainStrike;
        }
        public SortedList<Decimal, Instrument>[] GetNewStrikes(uint baseInstrumentToken, decimal baseInstrumentPrice, DateTime? expiry, int strikePriceIncrement)
        {
            DataLogic dl = new DataLogic();
            SortedList<Decimal, Instrument>[] nodeData = dl.RetrieveNextStrangleNodes(baseInstrumentToken, expiry.GetValueOrDefault(DateTime.Now), baseInstrumentPrice, baseInstrumentPrice, 0);
            return nodeData;

            //decimal nearestStrike = Math.Round(baseInstrumentPrice / strikePriceIncrement, 0) * strikePriceIncrement;

            //SortedList<Decimal, Instrument> dInstruments = new SortedList<decimal, Instrument>();

            //for (int i = -4; i <= 4; i++)
            //{
            //    decimal strike = nearestStrike + i * strikePriceIncrement;
            //    dInstruments.Add(strike, new Instrument() { Strike = strike, Expiry = expiry, BaseInstrumentToken = baseInstrumentToken });
            //}
            //return dInstruments;
        } 
        private bool PopulateReferenceStrangleData(StrangleData strangleNode, Tick[] ticks)
        {
            decimal lowerband = strangleNode.BaseInstrumentPrice - strangleNode.StrikePriceIncrement * 2;
            decimal upperband = strangleNode.BaseInstrumentPrice + strangleNode.StrikePriceIncrement * 2;

            if (strangleNode.Calls == null || strangleNode.Puts == null || lowerband < strangleNode.Calls.First().Value.Strike || upperband > strangleNode.Calls.Last().Value.Strike)
            {
                SortedList<Decimal, Instrument>[] nodeData = GetNewStrikes(strangleNode.BaseInstrumentToken, strangleNode.BaseInstrumentPrice, strangleNode.Expiry, strangleNode.StrikePriceIncrement);

                if (strangleNode.Calls != null)
                {
                    foreach (KeyValuePair<decimal, Instrument> keyValuePair in nodeData[0])
                    {
                        if (!strangleNode.Calls.ContainsKey(keyValuePair.Key))
                            strangleNode.Calls.Add(keyValuePair.Key, keyValuePair.Value);
                    }
                }
                else
                {
                    strangleNode.Calls = nodeData[0];

                }
                if (strangleNode.Puts != null)
                {
                    foreach (KeyValuePair<decimal, Instrument> keyValuePair in nodeData[1])
                    {
                        if (!strangleNode.Puts.ContainsKey(keyValuePair.Key))
                            strangleNode.Puts.Add(keyValuePair.Key, keyValuePair.Value);
                    }
                }
                else
                {
                    strangleNode.Puts = nodeData[1];
                }
            }

            for (int i = 0; i < strangleNode.Calls.Count; i++)
            {
                Instrument instrument = strangleNode.Calls.ElementAt(i).Value;
                decimal strike = strangleNode.Calls.ElementAt(i).Key;

                Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == instrument.InstrumentToken);
                if (optionTick.LastPrice != 0)
                {
                    instrument.LastPrice = optionTick.LastPrice;
                    instrument.InstrumentType = "CE";
                    instrument.Bids = optionTick.Bids;
                    instrument.Offers = optionTick.Offers;
                    instrument.OI = optionTick.OI;
                    instrument.OIDayHigh = optionTick.OIDayHigh;
                    instrument.OIDayLow = optionTick.OIDayLow;
                    strangleNode.Calls[strike] = instrument;
                }
            }

            for (int i = 0; i < strangleNode.Puts.Count; i++)
            {
                Instrument instrument = strangleNode.Puts.ElementAt(i).Value;
                decimal strike = strangleNode.Puts.ElementAt(i).Key;

                Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == instrument.InstrumentToken);
                if (optionTick.LastPrice != 0)
                {
                    instrument.LastPrice = optionTick.LastPrice;
                    instrument.InstrumentType = "PE";
                    instrument.Bids = optionTick.Bids;
                    instrument.Offers = optionTick.Offers;
                    instrument.OI = optionTick.OI;
                    instrument.OIDayHigh = optionTick.OIDayHigh;
                    instrument.OIDayLow = optionTick.OIDayLow;
                    strangleNode.Puts[strike] = instrument;
                }
            }

            //return !Convert.ToBoolean(strangleNode.Calls.Values.Count(x => x.OI == 0) + strangleNode.Puts.Values.Count(x => x.OI == 0));//

            return !Convert.ToBoolean(strangleNode.Calls.Values.Count(x => x.LastPrice == 0) + strangleNode.Puts.Values.Count(x => x.LastPrice == 0));
        }
        private void ReviewStrangle(StrangleData strangleNode, Tick[] ticks)
        {
            Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == strangleNode.BaseInstrumentToken);
            if (baseInstrumentTick.LastPrice != 0)
            {
                strangleNode.BaseInstrumentPrice = baseInstrumentTick.LastPrice;
            }
            if (strangleNode.BaseInstrumentPrice == 0)
            {
                return;
            }

            //Get Max Pain
            decimal maxPainStrike = UpdateMaxPainStrike(strangleNode, ticks);
            if (maxPainStrike == 0)
            {
                return;
            }

            TradedOption callOption = strangleNode.CurrentCall;
            TradedOption putOption = strangleNode.CurrentPut;

            //Initial sell
            if (callOption.TradingStatus == PositionStatus.NotTraded && putOption.TradingStatus == PositionStatus.NotTraded)
            {
                strangleNode.MaxPainStrike = maxPainStrike;
                NewPositions(callOption, putOption, strangleNode, ticks[0].LastTradeTime);
            }

            decimal currentPnL = strangleNode.NetPnL;

            callOption.Option = strangleNode.Calls[callOption.Option.Strike];
            putOption.Option = strangleNode.Puts[putOption.Option.Strike];

            Instrument currentCall = callOption.Option;//strangleNode.Calls.ElementAt(callOption.Index).Value;
            Instrument currentPut = putOption.Option; //strangleNode.Puts.ElementAt(putOption.Index).Value;

            if (currentCall.LastPrice == 0 || currentPut.LastPrice == 0) return;
            currentPnL -= (currentCall.LastPrice + currentPut.LastPrice);

            //WriteToExcel(++rowCounter, ticks[0].Timestamp.Value.ToString(), currentPnL.ToString());
            WriteToFile(ticks[0].LastTradeTime.Value.ToString(), currentPnL.ToString());

            //Operational check for PnL
            if (callOption.TradingStatus == PositionStatus.Open && putOption.TradingStatus == PositionStatus.Open)
            {
                //if loss is more than threshold or profit target is met, close the trade
                //if (strangleNode.NetPnL > 0 && (currentPnL * -1 > strangleNode.MaxLossThreshold || currentPnL > strangleNode.ProfitTarget))
                if (strangleNode.NetPnL > 0 && (currentPnL * -1 > strangleNode.MaxLossThreshold || currentPnL > strangleNode.ProfitTarget))
                {
                    ClosePositions(currentCall, currentPut, callOption, putOption, strangleNode, ticks[0].LastTradeTime);
                }
                //if (strangleNode.MaxPainStrike != maxPainStrike)
                if (strangleNode.MaxPainStrike > maxPainStrike + 105 || strangleNode.MaxPainStrike < maxPainStrike - 105) //Putting a range for max pain shift
                {
                    ClosePositions(currentCall, currentPut, callOption, putOption, strangleNode, ticks[0].LastTradeTime);
                    callOption.TradingStatus = PositionStatus.NotTraded;
                    putOption.TradingStatus = PositionStatus.NotTraded;
                }
            }
            else if (callOption.TradingStatus == PositionStatus.Closed && putOption.TradingStatus == PositionStatus.Closed)
            {
                //if (strangleNode.MaxPainStrike != maxPainStrike)
                if (strangleNode.MaxPainStrike > maxPainStrike + 105 || strangleNode.MaxPainStrike < maxPainStrike - 105) //Putting a range for max pain shift
                {
                    strangleNode.MaxPainStrike = maxPainStrike;
                    NewPositions(callOption, putOption, strangleNode, ticks[0].LastTradeTime);
                }
            }
        }
        private void ClosePositions(Instrument currentCall, Instrument currentPut, TradedOption callOption, TradedOption putOption, StrangleData strangleNode, DateTime? orderTime)
        {
            callOption.BuyTrade = PlaceOrder(strangleNode.ID, currentCall, buyOrder: true, strangleNode.TradingQuantity, orderTime);
            putOption.BuyTrade = PlaceOrder(strangleNode.ID, currentPut, buyOrder: true, strangleNode.TradingQuantity, orderTime);

            callOption.TradingStatus = PositionStatus.Closed;
            putOption.TradingStatus = PositionStatus.Closed;
        }
        private void NewPositions(TradedOption callOption, TradedOption putOption, StrangleData strangleNode, DateTime? orderTime)
        {
            strangleNode.NetPnL = 0;
            int maxPainCallIndex = strangleNode.Calls.IndexOfKey(strangleNode.MaxPainStrike);

            int callIndex = strangleNode.Calls.Count -1 >= maxPainCallIndex + 2 ? maxPainCallIndex + 2 : strangleNode.Calls.Count-1 >= maxPainCallIndex + 1 ? maxPainCallIndex + 1 : maxPainCallIndex;

            Instrument instrument = strangleNode.Calls.ElementAt(callIndex).Value; //TODO: Check if +- 2 index is still available in the reference calls and puts

            callOption.SellTrade = PlaceOrder(strangleNode.ID, instrument, buyOrder: false, strangleNode.TradingQuantity, orderTime);
            instrument.LastPrice = callOption.SellTrade.AveragePrice;
            callOption.Option = instrument;

            callOption.TradingStatus = PositionStatus.Open;
            strangleNode.NetPnL += callOption.SellTrade.AveragePrice;

            int maxPainPutIndex = strangleNode.Puts.IndexOfKey(strangleNode.MaxPainStrike); //TODO: Check if +- 2 index is still available in the reference calls and puts

            int putIndex = maxPainCallIndex >= 2 ? maxPainCallIndex - 2 : maxPainCallIndex >= 1 ? maxPainCallIndex - 1 : maxPainCallIndex;
            instrument = strangleNode.Puts.ElementAt(putIndex).Value;

            putOption.SellTrade = PlaceOrder(strangleNode.ID, instrument, buyOrder: false, strangleNode.TradingQuantity, orderTime);
            instrument.LastPrice = putOption.SellTrade.AveragePrice;
            putOption.Option = instrument;

            putOption.TradingStatus = PositionStatus.Open;
            strangleNode.NetPnL += putOption.SellTrade.AveragePrice;
        }


        private void UpdateTradeDetails(int strategyID, uint instrumentToken, int tradedLot, ShortTrade trade, int triggerID)
        {
            DataLogic dl = new DataLogic();
            dl.UpdateTrade(strategyID, instrumentToken, trade, AlgoIndex.ManageStrangleWithMaxPain, tradedLot, triggerID);
        }

        public virtual void OnNext(Tick[] ticks)
        {
            lock (ActiveStrangles)
            {
                for (int i = 0; i < ActiveStrangles.Count; i++)
                {
                    ReviewStrangle(ActiveStrangles.ElementAt(i), ticks);
                }
            }
        }
        private ShortTrade PlaceOrder(int strangleID, Instrument instrument, bool buyOrder, int quantity, DateTime? tickTime = null, uint token = 0, int triggerID = 0)
        {
            string tradingSymbol = instrument.TradingSymbol;
            decimal currentPrice = instrument.LastPrice;
            //Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
            //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, quantity, Product: Constants.PRODUCT_MIS,
            //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            ///TEMP, REMOVE Later
            if (currentPrice == 0)
            {
                DataLogic dl = new DataLogic();
                currentPrice = dl.RetrieveLastPrice(instrument.InstrumentToken, tickTime, buyOrder);
            }

            string orderId = "0";
            decimal averagePrice = 0;
            //if (orderStatus["data"]["order_id"] != null)
            //{
            //    orderId = orderStatus["data"]["order_id"];
            //}
            if (orderId != "0")
            {
                System.Threading.Thread.Sleep(200);
                List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
                averagePrice = orderInfo[orderInfo.Count - 1].AveragePrice;
            }
            if (averagePrice == 0)
                averagePrice = buyOrder ? currentPrice : currentPrice;
            // averagePrice = buyOrder ? averagePrice * -1 : averagePrice;

            ShortTrade trade = new ShortTrade();
            trade.AveragePrice = averagePrice;
            trade.ExchangeTimestamp = tickTime;// DateTime.Now;
            trade.Quantity = quantity;
            trade.OrderId = orderId;
            trade.TransactionType = buyOrder ? "Buy" : "Sell";
            trade.TriggerID = triggerID;

            UpdateTradeDetails(strangleID, instrument.InstrumentToken, quantity, trade, triggerID);
            
            return trade;
        }

        private void LoadActiveData()
        {
            AlgoIndex algoIndex = AlgoIndex.ManageStrangleWithMaxPain;
            DataLogic dl = new DataLogic();
            DataSet activeStrangles = dl.RetrieveActiveStrangleData(algoIndex);
            DataRelation strategy_Token_Relation = activeStrangles.Relations.Add("Strangle_Token", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"] },
                new DataColumn[] { activeStrangles.Tables[1].Columns["StrategyId"] });

            DataRelation strategy_Trades_Relation = activeStrangles.Relations.Add("Strangle_Trades", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"] },
                new DataColumn[] { activeStrangles.Tables[2].Columns["StrategyId"] });

            Instrument call, put;

            foreach (DataRow strangleRow in activeStrangles.Tables[0].Rows)
            {
                StrangleData strangleNode = new StrangleData();
                strangleNode.BaseInstrumentToken = Convert.ToUInt32(strangleRow["BToken"]);
                strangleNode.Expiry = Convert.ToDateTime(strangleRow["Expiry"]);
                strangleNode.MaxLossThreshold = Convert.ToDecimal(strangleRow["MaxLossPoints"]);
                strangleNode.ProfitTarget = Convert.ToDecimal(strangleRow["MaxProfitPoints"]);
                strangleNode.TradingQuantity = Convert.ToInt32(strangleRow["MaxQty"]);
                strangleNode.StrikePriceIncrement = Convert.ToInt32(strangleRow["StrikePriceIncrement"]);
                strangleNode.ID = Convert.ToInt32(strangleRow["ID"]);
                strangleNode.CurrentCall = new TradedOption();
                strangleNode.CurrentPut = new TradedOption();
                strangleNode.CurrentCall.TradingStatus = PositionStatus.NotTraded;
                strangleNode.CurrentPut.TradingStatus = PositionStatus.NotTraded;

                DataRow[] strangleTokenRows = strangleRow.GetChildRows(strategy_Token_Relation);
                if (strangleTokenRows.Count() == 0)
                {
                    ActiveStrangles.Add(strangleNode);
                    continue;
                }

                DataRow strangleTokenRow = strangleTokenRows[0];
                uint baseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]);

                call = new Instrument()
                {
                    BaseInstrumentToken = baseInstrumentToken,
                    InstrumentToken = Convert.ToUInt32(strangleTokenRow["CallToken"]),
                    InstrumentType = "CE",
                    Strike = (Decimal)strangleTokenRow["CallStrike"],
                    TradingSymbol = (string)strangleTokenRow["CallSymbol"]
                };
                put = new Instrument()
                {
                    BaseInstrumentToken = baseInstrumentToken,
                    InstrumentToken = Convert.ToUInt32(strangleTokenRow["PutToken"]),
                    InstrumentType = "PE",
                    Strike = (Decimal)strangleTokenRow["PutStrike"],
                    TradingSymbol = (string)strangleTokenRow["PutSymbol"]
                };

                if (strangleTokenRow["Expiry"] != DBNull.Value)
                {
                    call.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);
                    put.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);
                }
                strangleNode.CurrentPut.Option = put;
                strangleNode.CurrentCall.Option = call;

                ShortTrade trade;
                decimal netPnL = 0;
                foreach (DataRow strangleTradeRow in strangleRow.GetChildRows(strategy_Trades_Relation))
                {
                    trade = new ShortTrade();
                    trade.AveragePrice = (Decimal)strangleTradeRow["Price"];
                    trade.ExchangeTimestamp = (DateTime?)strangleTradeRow["TimeStamp"];
                    trade.OrderId = (string)strangleTradeRow["OrderId"];
                    trade.TransactionType = (string)strangleTradeRow["TransactionType"];
                    trade.Quantity = (int)strangleTradeRow["Quantity"];
                    trade.TriggerID = (int)strangleTradeRow["TriggerID"];
                    if (Convert.ToUInt32(strangleTradeRow["InstrumentToken"]) == call.InstrumentToken)
                    {
                        strangleNode.CurrentCall.SellTrade = trade;
                    }
                    else if (Convert.ToUInt32(strangleTradeRow["InstrumentToken"]) == put.InstrumentToken)
                    {
                        strangleNode.CurrentPut.SellTrade = trade;
                    }
                    netPnL += trade.AveragePrice * Math.Abs(trade.Quantity);
                }
                strangleNode.BaseInstrumentToken = call.BaseInstrumentToken;
                strangleNode.TradingQuantity = strangleNode.CurrentPut.SellTrade.Quantity; //PutTrades.Sum(x => x.Quantity);
                strangleNode.TradingQuantity = strangleNode.CurrentCall.SellTrade.Quantity;
                strangleNode.CurrentCall.TradingStatus = PositionStatus.Open;
                strangleNode.CurrentPut.TradingStatus = PositionStatus.Open;
                //strangleNode.MaxQty = (int)strangleRow["MaxQty"];
                //strangleNode.StepQty = Convert.ToInt32(strangleRow["MaxProfitPoints"]);
                strangleNode.NetPnL = netPnL;
                strangleNode.ID = (int)strangleRow["Id"];
                ActiveStrangles.Add(strangleNode);
            }

        }

        public void StoreIndexForMainPainStrangle(uint bToken, DateTime expiry, int tradingQty, int strikePriceIncrement, decimal maxLossThreshold, decimal profitTarget, DateTime timeOfOrder = default(DateTime))
        {
            timeOfOrder = DateTime.Now;
            
            //Update Database
            DataLogic dl = new DataLogic();
            dl.StoreIndexForMainPainStrangle(bToken: bToken, expiry, strikePriceIncrement: strikePriceIncrement, algoIndex: AlgoIndex.ManageStrangleWithMaxPain, tradingQty: tradingQty, maxLossThreshold, profitTarget, timeOfOrder);
        }

        private void WriteToFile(string timeText, string profitText)
        {
            string fullFileName = @"D:\Prashant\Intelligent Software\2019\MarketData\Output\ManageStrangleShiftWithMaxPain\ProfitStream.txt";
            //if (!System.IO. File.Exists(fullFileName))
            //{
            //    // Create a file to write to.
            //    using (System.IO.StreamWriter sw = System.IO.File.CreateText(fullFileName))
            //    {
            //        sw.WriteLine("\n");
            //        sw.WriteLine("{0} - {1}", timeText, profitText);
            //    }
            //}
            //else
            //{
                // Create a file to write to.
                using (System.IO.StreamWriter sw = System.IO.File.AppendText(fullFileName))
                {
                    sw.WriteLine(timeText + "-" + profitText);
                }
            //}
        }
        //private void WriteToExcel(int rowcounter, string timeText, string profitText)
        //{
        //    Microsoft.Office.Interop.Excel.Application oXL;
        //    Microsoft.Office.Interop.Excel._Workbook oWB;
        //    Microsoft.Office.Interop.Excel._Worksheet oSheet;
        //    Microsoft.Office.Interop.Excel.Range oRng;
        //    object misvalue = System.Reflection.Missing.Value;

        //    //Start Excel and get Application object.
        //    oXL = new Microsoft.Office.Interop.Excel.Application();
        //    oXL.Visible = false;

        //    //Get a new workbook.
        //    //oWB = (Microsoft.Office.Interop.Excel._Workbook)(oXL.Workbooks.Add(""));
        //    string fullFileName = @"D:\Prashant\Intelligent Software\2019\MarketData\Output\ManageStrangleShiftWithMaxPain\ProfitStream.xls";
        //    if (!System.IO.File.Exists(fullFileName))
        //    {
        //        System.IO.FileStream fs = System.IO.File.Create(fullFileName);
        //        fs.Close();
        //    }
        //    oWB = oXL.Workbooks.Open(fullFileName);

        //    oSheet = (Microsoft.Office.Interop.Excel._Worksheet)oWB.ActiveSheet;

        //    //Add table headers going cell by cell.
        //    oSheet.Cells[1, 1] = "ProfitDelta";

        //    //Format A1:D1 as bold, vertical alignment = center.
        //    oSheet.get_Range("A1", "D1").Font.Bold = true;
        //    oSheet.get_Range("A1", "D1").VerticalAlignment =
        //        Microsoft.Office.Interop.Excel.XlVAlign.xlVAlignCenter;

        //    oSheet.Cells[rowcounter, 1] = timeText;
        //    oSheet.Cells[rowcounter, 2] = profitText;


        //    //AutoFit columns A:D.
        //    oRng = oSheet.get_Range("A1", "D1");
        //    oRng.EntireColumn.AutoFit();

        //    oXL.Visible = false;
        //    oXL.UserControl = false;
        //    oWB.Save();
        //    //oWB.SaveAs(@"D:\Prashant\Intelligent Software\2019\MarketData\Output\ManageStrangleShiftWithMaxPain\ProfitStream.xls", Microsoft.Office.Interop.Excel.XlFileFormat.xlWorkbookDefault, Type.Missing, Type.Missing,
        //    //    false, false, Microsoft.Office.Interop.Excel.XlSaveAsAccessMode.xlNoChange,
        //    //    Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing);

        //    oWB.Close();
        //    oXL.Quit();
        //}
        public virtual void OnError(Exception ex)
        {
            ///TODO: Log the error. Also handle the error.
        }


        public virtual void OnCompleted()
        {
        }
    }
}
