using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AdvanceAlgos.Utilities;
using Global;
using KiteConnect;
using ZConnectWrapper;
//using Pub_Sub;
//using MarketDataTest;
using System.Data;
using System.IO;


using Microsoft.Office.Interop.Excel;
using System.Net.Http.Headers;
//using ZeroMQMessaging;

namespace AdvanceAlgos.Algorithms
{

    /// <summary>
    /// Stratety based on total strangle value.
    /// Increase lots on strangle till, 1 side becomes half 
    /// Start with 5 lots (5x40) and then book 2 lots when strangle looses 20% value, and 3 lots when strangle looses 50% value and then move 1 point near on both side or form appropriate strangle
    /// If strangle value increases by 20%, add 2 lots; another 2 lots on the side when 1 side becomes 50% of another side
    /// keep a check on next strangle price and close the profit side and make another strangle with high size. This way strangle will get near the stock price
    /// Try delta neutral
    /// Do this till next strike price cross one of strangle strike
    /// </summary>
    public class ExpiryTrade_Lab //: //IZMQ// IObserver<Tick[]>
    {
        public IDisposable UnsubscriptionToken;
        Dictionary<int, StrangleDataStructure> ActiveStrangles = new Dictionary<int, StrangleDataStructure>();

        private const int INSTRUMENT_TOKEN = 0;
        private const int INITIAL_TRADED_PRICE = 1;
        private const int CURRENT_PRICE = 2;
        private const int QUANTITY = 3;
        private const int TRADE_ID = 4;
        private const int TRADING_STATUS = 5;
        private const int POSITION_PnL = 6;
        private const int STRIKE = 7;
        private const int PRICEDELTA = 8;
        private decimal MULTIPLIER = 1;
        private bool InitialOI = false;
        private const int CE = 0;
        private const int PE = 1;
        private decimal previousbaseinstrumentprice=0;
        public ExpiryTrade_Lab()
        {
            LoadActiveData();
        }

        private bool CheckStrangleValue(StrangleDataList strangleNode, List<TradedInstrument> tradedCalls, List<TradedInstrument> tradedPuts, DateTime timeOfOrder)
        {
            decimal previousStrangleValue = strangleNode.UnBookedPnL;
            bool maxQtytraded = true;
            int totalCallQty = strangleNode.NetCallQtyInTrade, totalPutQty = strangleNode.NetPutQtyInTrade;

            decimal currentStrangleValue = tradedCalls.Sum(x => x.SellTrades.Sum(p => p.Quantity * x.Option.LastPrice));
            currentStrangleValue += tradedPuts.Sum(x => x.SellTrades.Sum(p => p.Quantity * x.Option.LastPrice));

            if (totalPutQty < strangleNode.MaxQty)
            {
                maxQtytraded = false;
                if (currentStrangleValue > previousStrangleValue * 1.1m) //Add if strangle looses 10% on total strangle value
                {
                    //Add to quantity
                    foreach (TradedInstrument ti in tradedCalls)
                    {
                        ShortTrade addlTrade = PlaceOrder(strangleNode.ID, ti.Option, false, strangleNode.StepQty, timeOfOrder);
                        ti.SellTrades.Add(addlTrade);
                        ti.UnbookedPnl += addlTrade.AveragePrice * addlTrade.Quantity;
                        strangleNode.UnBookedPnL += addlTrade.AveragePrice * addlTrade.Quantity;
                    }
                    //Add to quantity
                    foreach (TradedInstrument ti in tradedPuts)
                    {
                        ShortTrade addlTrade = PlaceOrder(strangleNode.ID, ti.Option, false, strangleNode.StepQty, timeOfOrder);
                        ti.SellTrades.Add(addlTrade);
                        ti.UnbookedPnl += addlTrade.AveragePrice * addlTrade.Quantity;
                        strangleNode.UnBookedPnL += addlTrade.AveragePrice * addlTrade.Quantity;
                    }

                }
            }
            if (currentStrangleValue < previousStrangleValue * 0.9m) //Reset the threshold if strangle looses 10%
            {
                if (totalCallQty > strangleNode.InitialQty && totalPutQty > strangleNode.InitialQty)
                {
                    //take out trades till Initial qty and do not change the total strangle value
                }
                strangleNode.UnBookedPnL = currentStrangleValue;

                //Add to quantity
                foreach (TradedInstrument ti in tradedCalls)
                {
                    ShortTrade addlTrade = PlaceOrder(strangleNode.ID, ti.Option, false, strangleNode.StepQty, timeOfOrder);
                    ti.SellTrades.Add(addlTrade);
                    ti.UnbookedPnl += addlTrade.AveragePrice * addlTrade.Quantity;
                    strangleNode.UnBookedPnL += addlTrade.AveragePrice * addlTrade.Quantity;
                }
                //Add to quantity
                foreach (TradedInstrument ti in tradedPuts)
                {
                    ShortTrade addlTrade = PlaceOrder(strangleNode.ID, ti.Option, false, strangleNode.StepQty, timeOfOrder);
                    ti.SellTrades.Add(addlTrade);
                    ti.UnbookedPnl += addlTrade.AveragePrice * addlTrade.Quantity;
                    strangleNode.UnBookedPnL += addlTrade.AveragePrice * addlTrade.Quantity;
                }
            }
            return maxQtytraded;
        }
        private void WriteToExcel(SortedList<string, decimal> data, decimal baseInstrumentPrice)
        {
            Microsoft.Office.Interop.Excel.Application oXL;
            Microsoft.Office.Interop.Excel._Workbook oWB;
            Microsoft.Office.Interop.Excel._Worksheet oSheet;
            Microsoft.Office.Interop.Excel.Range oRng;
            object misvalue = System.Reflection.Missing.Value;

            //Start Excel and get Application object.
            oXL = new Microsoft.Office.Interop.Excel.Application();
            oXL.Visible = true;
            oXL.DisplayAlerts = false;
            //Get a new workbook.
            oWB = oXL.Workbooks.Open(@"D:\Prashant\Intelligent Software\2019\MarketData\ExcelStudy\oivolume.xlsx",Editable:true);
            //oWB = (Microsoft.Office.Interop.Excel._Workbook)(oXL.Workbooks.Add(""));
            oSheet = (Microsoft.Office.Interop.Excel._Worksheet)oWB.ActiveSheet;

            Microsoft.Office.Interop.Excel.Range last = oSheet.Cells.SpecialCells(Microsoft.Office.Interop.Excel.XlCellType.xlCellTypeLastCell, Type.Missing);
            int lastUsedRow = last.Row;
            
            int i = lastUsedRow +1;
            
            i = i == 1 ? 2 : i;

            int u = 2;
            foreach (KeyValuePair<string, decimal> strikePain in data)
            {
                oSheet.Cells[2, u++] = strikePain.Key;
            }
            oSheet.Cells[i, 1] = baseInstrumentPrice;
            int j = 2;
            foreach (KeyValuePair<string, decimal> strikePain in data)
            {
                for (int k = 2; k < 25; k++)
                {
                    Microsoft.Office.Interop.Excel.Range range = (Microsoft.Office.Interop.Excel.Range)oSheet.Cells[2, k];
                    if (range.Value == null)
                    {
                        break;
                    }
                    string cellValue = range.Value.ToString();

                    // string header = oSheet.Cells[1, k];
                    if (cellValue == strikePain.Key)
                    {
                        oSheet.Cells[i, k] = strikePain.Value;
                    }
                }
                //oSheet.Cells[i, j++] = strikePain.Value;
            }




            oXL.Visible = false;
            oXL.UserControl = false;

            //oWB.SaveAs(@"D:\Prashant\Intelligent Software\2019\MarketData\ExcelStudy\oiVolumne.xls", Microsoft.Office.Interop.Excel.XlFileFormat.xlWorkbookDefault, Type.Missing, Type.Missing,
            //    false, false, Microsoft.Office.Interop.Excel.XlSaveAsAccessMode.xlNoChange,
            //    Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing);

            oWB.Save();
            oWB.Close();
            oXL.Quit();
        }
        private void ManageStrangle(StrangleDataStructure strangleNode, Tick[] ticks)
        {
            Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == strangleNode.BaseInstrumentToken);
            if (baseInstrumentTick.LastPrice != 0)
            {
                strangleNode.BaseInstrumentPrice = baseInstrumentTick.LastPrice;
            }
            if (strangleNode.BaseInstrumentPrice == 0)// * callOption.LastPrice * putOption.LastPrice == 0)
            {
                return;
            }

            //Get Max Pain
            SortedList<string, decimal> maxPainStrike = UpdateMaxPainStrike_OIChange(strangleNode, ticks);
            //if (Math.Abs(previousbaseinstrumentprice - strangleNode.BaseInstrumentPrice) > 20 && maxPainStrike != null && maxPainStrike.Count != 0)
            //{
            //    previousbaseinstrumentprice = strangleNode.BaseInstrumentPrice;
            //    WriteToExcel(maxPainStrike, strangleNode.BaseInstrumentPrice);
            //}
            //return;
            if (maxPainStrike == null || maxPainStrike.Count != 0)
            {
                return;
            }
            


            DateTime timeOfOrder = ticks[0].Timestamp.Value;
            SortedList<decimal, Instrument> callUniverse = strangleNode.CallUniverse;
            SortedList<decimal, Instrument> putUniverse = strangleNode.PutUniverse;

            Decimal[][,] optionMatix = strangleNode.OptionMatrix;

            //Each trade should have a record and pnl, and then this trade should be closed first when delta to be neutralized.

            if (optionMatix[0] == null)
            {
                //int tradeCounter = 0;
                //Take Initial Position
                decimal[][] optionTrade = InitialTrade(strangleNode, ticks);
                for (int i = 0; i < optionTrade.GetLength(0); i++)
                {
                    optionMatix[i] = new decimal[1, 10];
                    optionMatix[i][0, INSTRUMENT_TOKEN] = optionTrade[i][INSTRUMENT_TOKEN]; //Instrument Token
                    optionMatix[i][0, INITIAL_TRADED_PRICE] = optionTrade[i][INITIAL_TRADED_PRICE]; //TradedPrice
                    optionMatix[i][0, CURRENT_PRICE] = optionTrade[i][CURRENT_PRICE]; //CurrentPrice
                    optionMatix[i][0, QUANTITY] = optionTrade[i][QUANTITY]; //Quantity
                    optionMatix[i][0, TRADE_ID] = optionTrade[i][TRADE_ID]; //TradeID
                    optionMatix[i][0, TRADING_STATUS] = optionTrade[i][TRADING_STATUS]; // Trading Status: Open
                    optionMatix[i][0, POSITION_PnL] = optionTrade[i][POSITION_PnL];// PnL of trade as price updates
                    optionMatix[i][0, STRIKE] = optionTrade[i][STRIKE];// Strike Price of Instrument
                    
                    //string tradingSymbol = i == PE ? strangleNode.PutUniverse[optionTrade[i][STRIKE]].TradingSymbol : strangleNode.CallUniverse[optionTrade[i][STRIKE]].TradingSymbol;


                    //callMatix[tradeCounter, PRICEDELTA] = callTrade[1];// Price detal between consicutive positions
                    strangleNode.OptionMatrix[i] = optionMatix[i];
                }
               
                return;
            }

            UpdateMatrix(callUniverse, ref optionMatix[CE]);
            UpdateMatrix(putUniverse, ref optionMatix[PE]);

            ///Step 2: Higher value strangle has not reached within 100 points to Binstruments -  Delta threshold
            ///TODO: MOVE THE TRADE AND NOT JUST CLOSE IT
            ///16-04-20: This step has been moved out as new ATM options are checked regularly to avoid Gamma play.
            
            decimal currentPutQty = GetQtyInTrade(optionMatix[PE]);
            decimal currentCallQty = GetQtyInTrade(optionMatix[CE]);
            decimal currentQty = currentCallQty + currentPutQty;
            decimal qtyAvailable = strangleNode.MaxQty - currentQty;
            
            //Stop trade when premium reaches lower level
            if(StopTrade(ref optionMatix, strangleNode, timeOfOrder, Convert.ToInt32(qtyAvailable)))
            {
                return;
            }
            CloseNearATMTrades(ref optionMatix, strangleNode, timeOfOrder, Convert.ToInt32(qtyAvailable));

            currentPutQty = GetQtyInTrade(optionMatix[PE]);
            currentCallQty = GetQtyInTrade(optionMatix[CE]);
            currentQty = currentCallQty + currentPutQty;
            qtyAvailable = strangleNode.MaxQty - currentQty;


            decimal callInitialValue = GetMatrixInitialValue(optionMatix[CE]);
            decimal putInitialValue = GetMatrixInitialValue(optionMatix[PE]);

            decimal callValue = GetMatrixCurrentValue(optionMatix[CE]);
            decimal putValue = GetMatrixCurrentValue(optionMatix[PE]);
            decimal higherOptionValue = Math.Max(callValue, putValue);
            decimal lowerOptionValue = Math.Min(callValue, putValue);
            int highValueOptionType = callValue > putValue ? CE : PE;

            decimal initialStrangleValue = callInitialValue + putInitialValue; // only for trades that are not closed yet. Open strangle value.
            decimal currentStrangleValue = callValue + putValue;

           
            ///step:if next strike cross this strike on opposide side
            #region Manage one side
            if (higherOptionValue > lowerOptionValue * 1.7m)
            {
                int stepQty = strangleNode.StepQty;
                
                decimal maxQty = strangleNode.MaxQty;
                decimal maxQtyForStrangle = strangleNode.MaxQty; // There can be 2 max qtys, a lower one for strangle and higher for side balancing
                decimal bInstrumentPrice = strangleNode.BaseInstrumentPrice;
                decimal lotSize = strangleNode.CallUniverse.First().Value.LotSize;
                decimal optionToken = 0;

                //decimal premiumNeeded = (higherOptionValue - lowerOptionValue) / stepQty;
                decimal premiumNeeded = (higherOptionValue - lowerOptionValue);

                ///Step 1: Check if there are any profitable calltrades that can be bought back
                CloseProfitableTrades(ref optionMatix[highValueOptionType], strangleNode, timeOfOrder, premiumNeeded, lotSize, highValueOptionType);

                /////Step 2: Higher value strangle has not reached within 100 points to Binstruments -  Delta threshold
                /////TODO: MOVE THE TRADE AND NOT JUST CLOSE IT
                //CloseNearATMTrades(optionMatix[highValueOptionType], strangleNode, timeOfOrder, (InstrumentType)highValueOptionType);

                ///Step 3: Check to see if trade can be taken out from lower value strangle matrix
                ///TODO: This step may not be needed as lower value option gets moved to maintain delta. This should be a totally seperate step.
                BookProfitAndMoveClosure();

                ///Intermediate Step
                ///Recheck value of options to see if further continuation is needed.
                higherOptionValue = highValueOptionType == CE ? GetMatrixCurrentValue(optionMatix[CE]) : GetMatrixCurrentValue(optionMatix[PE]);

                if (higherOptionValue <= lowerOptionValue * 1.5m)
                {
                    return;
                }

                //Step 3: keep checking on the next in the money option and see if the value ot next strike price of opposite side crosses this option price

                ///Step4: Look for increasing quantity on lower strike prices to increase the value of strangle just above loss making high value strangle
                currentPutQty = GetQtyInTrade(optionMatix[PE]);
                currentCallQty = GetQtyInTrade(optionMatix[CE]);
                currentQty = currentCallQty + currentPutQty;
                qtyAvailable = maxQty - currentQty;
                decimal valueNeeded = 0;
                decimal[,] matrix;

                if (qtyAvailable > lotSize)
                {
                    valueNeeded = Math.Abs(higherOptionValue - lowerOptionValue);
                    matrix = callValue > putValue ? optionMatix[PE] : optionMatix[CE];
                    

                    SortMatrixByPrice(ref matrix);

                    for (int i = 0; i < matrix.GetLength(0); i++)
                    {
                        if (matrix[i, TRADING_STATUS] == (decimal)TradeStatus.Open && (matrix[i, CURRENT_PRICE] * qtyAvailable >= valueNeeded || i == matrix.GetLength(0) - 1)) //2: CurrentPrice
                        {
                            string tradingSymbol = callValue > putValue ? strangleNode.PutUniverse[matrix[i, STRIKE]].TradingSymbol : strangleNode.CallUniverse[matrix[i, STRIKE]].TradingSymbol;

                            //Book this trade and update matix with only open trades
                            int qtyToBeBooked = Convert.ToInt32(Math.Ceiling((valueNeeded / matrix[i, CURRENT_PRICE]) / lotSize) * lotSize);
                            

                            optionToken = matrix[i, INSTRUMENT_TOKEN];
                            //Instrument option = strangleNode.TradedStrangle.Options.First(x => x.InstrumentToken == optionToken);
                            ShortTrade shortTrade = PlaceOrder(strangleNode.ID, tradingSymbol, matrix[i, CURRENT_PRICE], Convert.ToUInt32(optionToken),
                                false, Convert.ToInt32(Math.Min(qtyToBeBooked, qtyAvailable)), timeOfOrder, triggerID: 1);

                            decimal[,] data = new decimal[1, 10];
                            data[0, INSTRUMENT_TOKEN] = optionToken;
                            data[0, INITIAL_TRADED_PRICE] = shortTrade.AveragePrice;
                            data[0, CURRENT_PRICE] = shortTrade.AveragePrice;
                            data[0, QUANTITY] = shortTrade.Quantity;
                            data[0, TRADING_STATUS] = (decimal)TradeStatus.Open;
                            data[0, TRADE_ID] = shortTrade.TriggerID;
                            data[0, POSITION_PnL] = 0;// PnL of trade as price updates
                            data[0, STRIKE] = matrix[i, STRIKE];// Strike Price of Instrument

                            //check the universe for appropriate strike price for addition. The price should be such that 1 step qty should help
                            //break;

                            if (callValue > putValue)
                            {
                                strangleNode.AddMatrixRow(data, InstrumentType.PE);
                            }
                            else
                            {
                                strangleNode.AddMatrixRow(data, InstrumentType.CE);
                            }
                            return;
                        }
                    }


                }
                
                ///Step 5: If no additional quantity available in Step 4, then move the strangles
                MoveNearTerm(strangleNode, ref optionMatix, timeOfOrder);
            }
            #endregion
            #region Manage at strangle level
            else if (currentStrangleValue > 1.2m * initialStrangleValue)
            {
                //int stepQty = strangleNode.StepQty;
                //decimal maxQty = strangleNode.MaxQty;
                //decimal maxQtyForStrangle = strangleNode.MaxQty; // There can be 2 max qtys, a lower one for strangle and higher for side balancing
                //decimal bInstrumentPrice = strangleNode.BaseInstrumentPrice;
                //decimal lotSize = strangleNode.CallUniverse.First().Value.LotSize;
                //decimal optionStrike = 0;

                ////Determine proper structure with minimum trade and first taking profit out from existing trade

                //// Step 1: Check if max quantity is reached. If yes move to step 2 , else increase qty on both sides and balance
                //currentQty = GetQtyInTrade(optionMatix[CE]) + GetQtyInTrade(optionMatix[PE]);
                //maxQtyForStrangle = maxQty - currentQty;

                //if (maxQtyForStrangle >= (stepQty * 2)) // Add qtys to near ATM options
                //{
                //    optionStrike = GetMinStrike(optionMatix[CE]);

                //    string tradingSymbol = strangleNode.CallUniverse[optionStrike].TradingSymbol;
                //    uint optionToken = strangleNode.CallUniverse[optionStrike].InstrumentToken;

                //    //Instrument option = strangleNode.TradedStrangle.Options.First(x => x.InstrumentToken == optionToken);

                //    ShortTrade shortTrade = PlaceOrder(strangleNode.ID,tradingSymbol, optionMatix[CE][0, CURRENT_PRICE],
                //        optionToken, false, stepQty, timeOfOrder); //TODO: What qtys should be added to both side? same as original or same as step qty?

                //    decimal[,] data = new decimal[1, 10];
                //    data[0, INSTRUMENT_TOKEN] = optionToken;
                //    data[0, INITIAL_TRADED_PRICE] = shortTrade.AveragePrice;
                //    data[0, CURRENT_PRICE] = shortTrade.AveragePrice;
                //    data[0, QUANTITY] = shortTrade.Quantity;
                //    data[0, TRADING_STATUS] = (decimal)TradeStatus.Open;
                //    data[0, TRADE_ID] = shortTrade.TriggerID;
                //    data[0, POSITION_PnL] = 0;// PnL of trade as price updates
                //    data[0, STRIKE] = optionStrike;// Strike Price of Instrument
                //    strangleNode.AddMatrixRow(data, InstrumentType.CE);

                //    optionStrike = GetMaxStrike(optionMatix[PE]);
                //    tradingSymbol = strangleNode.PutUniverse[optionStrike].TradingSymbol;
                //    optionToken = strangleNode.PutUniverse[optionStrike].InstrumentToken;

                //    shortTrade = PlaceOrder(strangleNode.ID, tradingSymbol, optionMatix[PE][0, CURRENT_PRICE],
                //       optionToken, false, stepQty, timeOfOrder);

                //    data = new decimal[1, 10];
                //    data[0, INSTRUMENT_TOKEN] = optionToken;
                //    data[0, INITIAL_TRADED_PRICE] = shortTrade.AveragePrice;
                //    data[0, CURRENT_PRICE] = shortTrade.AveragePrice;
                //    data[0, QUANTITY] = shortTrade.Quantity;
                //    data[0, TRADING_STATUS] = (decimal)TradeStatus.Open;
                //    data[0, TRADE_ID] = shortTrade.TriggerID;
                //    data[0, POSITION_PnL] = 0;// PnL of trade as price updates
                //    data[0, STRIKE] = optionStrike;// Strike Price of Instrument
                //    strangleNode.AddMatrixRow(data, InstrumentType.PE);

                //}
                ////Step 2: move far OTMs to near ATMS. Same as step 5 below
                //else if (currentQty < maxQtyForStrangle)
                //{
                //    //optionToken = GetOptionWithMaxStrike(callMatix);
                //    //Instrument option = strangleNode.TradedStrangle.Options.First(x => x.InstrumentToken == optionToken);
                //    //PlaceOrder(strangleNode.ID, option, false, stepQty, timeOfOrder, triggerID: tradeCounter + 1);

                //    //optionToken = GetOptionWithMinStrike(putMatix);
                //    //option = strangleNode.TradedStrangle.Options.First(x => x.InstrumentToken == optionToken);
                //    //PlaceOrder(strangleNode.ID, option, false, stepQty, timeOfOrder, triggerID: tradeCounter + 1);
                //}

            }
            #endregion
            //Book this trade and update matix with only open trades
            strangleNode.OptionMatrix[CE] = optionMatix[CE];
            strangleNode.OptionMatrix[PE] = optionMatix[PE];
        }

        /// <summary>
        /// Updates Options matrix with closed profitable trades
        /// </summary>
        /// <param name="optionMatrix"></param>
        /// <param name="strangleNode"></param>
        /// <param name="tradeQty"></param>
        /// <param name="timeOfOrder"></param>
        private void CloseProfitableTrades(ref decimal[,] optionMatrix, StrangleDataStructure strangleNode, DateTime? timeOfOrder, decimal valueNeeded, decimal lotSize, int optionType)
        {
            SortedList<decimal, Instrument> optionUniverse = optionType == CE ? strangleNode.CallUniverse : strangleNode.PutUniverse;
            int closeCounter = 0;
            for (int i = 0; i < optionMatrix.GetLength(0); i++)
            {
                if (optionMatrix[i, POSITION_PnL] > 0 && optionMatrix[i, POSITION_PnL] * lotSize <= valueNeeded)
                {
                    decimal optionValue = optionMatrix[i, POSITION_PnL] * optionMatrix[i, QUANTITY];
                    decimal qtyToBeClosed = Math.Round(valueNeeded / optionMatrix[i, CURRENT_PRICE] / lotSize) * lotSize;

                    decimal quantity = Math.Min(qtyToBeClosed, optionMatrix[i, QUANTITY]);
                    uint instrumentToken = Convert.ToUInt32(optionMatrix[i, INSTRUMENT_TOKEN]);
                    Instrument option = optionUniverse.FirstOrDefault(x => x.Value.InstrumentToken == instrumentToken).Value;

                    ShortTrade trade = PlaceOrder(strangleNode.ID, option, true, Convert.ToInt32(quantity), timeOfOrder, triggerID: i);

                    optionMatrix[i, CURRENT_PRICE] = trade.AveragePrice;

                    if (quantity == optionMatrix[i, QUANTITY])
                    {
                        optionMatrix[i, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                        closeCounter++;
                    }
                    else
                    {
                        optionMatrix[i, TRADING_STATUS] = (decimal)TradeStatus.Open;
                    }
                    optionMatrix[i, QUANTITY] = optionMatrix[i, QUANTITY] - quantity;
                    valueNeeded -= optionMatrix[i, CURRENT_PRICE] * quantity;
                }
            }

            if (closeCounter > 0)
            {
                decimal[,] result = new decimal[optionMatrix.GetLength(0) - closeCounter, optionMatrix.GetLength(1)];

                for (int i = 0, j = 0; i < optionMatrix.GetLength(0); i++)
                {
                    if (optionMatrix[i, TRADING_STATUS] == (decimal)TradeStatus.Closed)
                    {
                        continue;
                    }
                    else
                    {
                        for (int k = 0, u = 0; k < optionMatrix.GetLength(1); k++)
                        {
                            result[j, u] = optionMatrix[i, k];
                            u++;
                        }
                    }
                    j++;
                }
                optionMatrix = result;
            }
        }
        public static decimal[,] TrimArray(int rowToRemove, decimal[,] originalArray)
        {
            decimal[,] result = new decimal[originalArray.GetLength(0) - 1, originalArray.GetLength(1)];

            for (int i = 0, j = 0; i < originalArray.GetLength(0); i++)
            {
                if (i == rowToRemove)
                    continue;

                for (int k = 0, u = 0; k < originalArray.GetLength(1); k++)
                {
                    result[j, u] = originalArray[i, k];
                    u++;
                }
                j++;
            }

            return result;
        }


        private bool StopTrade(ref decimal[][,] optionMatrices, StrangleDataStructure strangleNode, DateTime? timeOfOrder, int quantityAvailable)
        {
            bool stopTrade = true;
            decimal minPrice = 8;
            for (int j = 0; j < optionMatrices.GetLength(0); j++)
            {
                decimal[,] optionMatrix = optionMatrices[j];
                for (int i = 0; i < optionMatrix.GetLength(0); i++)
                {
                    if (optionMatrix[i, CURRENT_PRICE] > minPrice)
                    {
                        stopTrade = false;
                        break;
                    }
                }
            }
            return stopTrade;
        }
            private void CloseNearATMTrades(ref decimal[][,] optionMatrices, StrangleDataStructure strangleNode, DateTime? timeOfOrder, int quantityAvailable)
        {
            for (int j = 0; j < optionMatrices.GetLength(0); j++)
            {
                int closeCounter = 0;
                decimal[,] optionMatrix = optionMatrices[j];
                InstrumentType optionType = j == CE ? InstrumentType.CE : InstrumentType.PE;

            for (int i = 0; i < optionMatrix.GetLength(0); i++)
            {
                if (optionMatrix[i, STRIKE] > 0 && optionMatrix[i, TRADING_STATUS] == (decimal)TradeStatus.Open)
                //strike price increment/ Manage Delta
                {
                    //close this trade
                    int quantity = Convert.ToInt32(optionMatrix[i, QUANTITY]);
                    decimal DistanceFromBaseInstrumentPrice = 100;
                    decimal strike = optionMatrix[i, STRIKE];
                    ///TODO: Need to move option 1 strike behind
                    if(optionType == InstrumentType.CE && strike <= strangleNode.BaseInstrumentPrice + strangleNode.StrikePriceIncrement * 0.6m)
                    {
                        Instrument option = strangleNode.CallUniverse[strike];
                        ShortTrade trade = PlaceOrder(strangleNode.ID, option.TradingSymbol, optionMatrix[i, CURRENT_PRICE], option.InstrumentToken, true, quantity, timeOfOrder, triggerID: i);
                        optionMatrix[i, CURRENT_PRICE] = trade.AveragePrice;
                        optionMatrix[i, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                        closeCounter++;

                        SortedList<decimal, Instrument> callUniverse = strangleNode.CallUniverse;
                        KeyValuePair<decimal, Instrument> callNode = callUniverse.
                            Where(x => x.Key > strike && x.Key >= strangleNode.BaseInstrumentPrice + DistanceFromBaseInstrumentPrice).OrderBy(x => x.Key).First();

                        quantityAvailable = quantityAvailable + quantity;
                        quantity = Convert.ToInt32(optionMatrix[i, CURRENT_PRICE] / callNode.Value.LastPrice) * quantity;
                        quantity = quantity < quantityAvailable ? quantity : quantityAvailable;

                        PlaceOrderAndUpdateMatrix(strangleNode.ID, ref optionMatrix, callNode.Value, false, quantity, timeOfOrder, triggerID: i);
                        //strangleNode.OptionMatrix[CE] = optionMatrix; This is getting assigned in the main function, so no need here
                    }
                    else if (optionType == InstrumentType.PE && strike >= strangleNode.BaseInstrumentPrice - strangleNode.StrikePriceIncrement * 0.6m)
                    {
                        Instrument option = strangleNode.PutUniverse[strike];
                        ShortTrade trade = PlaceOrder(strangleNode.ID, option.TradingSymbol, optionMatrix[i, CURRENT_PRICE], option.InstrumentToken, true, quantity, timeOfOrder, triggerID: i);
                        optionMatrix[i, CURRENT_PRICE] = trade.AveragePrice;
                        optionMatrix[i, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                        closeCounter++;

                        SortedList<decimal, Instrument> putUniverse = strangleNode.PutUniverse;
                        KeyValuePair<decimal, Instrument> putNode = putUniverse.
                             Where(x => x.Key < strike && x.Key <= strangleNode.BaseInstrumentPrice - DistanceFromBaseInstrumentPrice).OrderByDescending(x => x.Key).First();

                        quantityAvailable = quantityAvailable + quantity;
                        quantity = Convert.ToInt32(optionMatrix[i, CURRENT_PRICE] / putNode.Value.LastPrice) * quantity;
                        quantity = quantity < quantityAvailable ? quantity : quantityAvailable;

                        PlaceOrderAndUpdateMatrix(strangleNode.ID, ref optionMatrix, putNode.Value, false, quantity, timeOfOrder, triggerID: i);
                        //strangleNode.OptionMatrix[PE] = optionMatrix; This is getting assigned in the main function, so no need here
                    }
                }
            }

                if (closeCounter > 0)
                {
                    decimal[,] result = new decimal[optionMatrix.GetLength(0) - closeCounter, optionMatrix.GetLength(1)];

                    for (int i = 0, w = 0; i < optionMatrix.GetLength(0); i++)
                    {
                        if (optionMatrix[i, TRADING_STATUS] == (decimal)TradeStatus.Closed)
                        {
                            continue;
                        }
                        else
                        {
                            for (int k = 0, u = 0; k < optionMatrix.GetLength(1); k++)
                            {
                                result[w, u] = optionMatrix[i, k];
                                u++;
                            }
                        }
                        w++;
                    }
                    optionMatrix = result;

                    optionMatrices[j] = optionMatrix;
                }
            }
        }
        private void BookProfitAndMoveClosure()
        {
            //for (int i = 0; i < putMatix.GetLength(0); i++)
            //{
            //    if(putMatix[i, 2] < 0.5m * putMatix[i, 1]) 
            //    {
            //        //Book this trade and update matix with only open trades

            //        //close this trade
            //        optionToken = callMatix[i, INSTRUMENT_TOKEN];
            //        Instrument option = strangleNode.TradedStrangle.Options.First(x => x.InstrumentToken == optionToken);
            //        PlaceOrder(strangleNode.ID, option, false, stepQty, timeOfOrder, triggerID: i);
            //        callMatix[i, TRADING_STATUS] = (decimal)TradeStatus.Closed;
            //    }
            //}
        }
        private void UpdateMatrix(SortedList<decimal, Instrument> optionUniverse, ref decimal[,] optionMatrix)
        {
            for (int i = 0; i < optionMatrix.GetLength(0); i++)
            {
                if (optionMatrix[i, TRADING_STATUS] == (decimal)TradeStatus.Open)
                {
                    Instrument option = optionUniverse[optionMatrix[i, STRIKE]];
                    if (optionMatrix[i, INSTRUMENT_TOKEN] != option.InstrumentToken)
                    {
                        throw new Exception("Incorrect Option");
                    }
                    optionMatrix[i, CURRENT_PRICE] = option.LastPrice; //CurrentPrice
                    optionMatrix[i, POSITION_PnL] = optionMatrix[i, INITIAL_TRADED_PRICE] - optionMatrix[i, CURRENT_PRICE];// PnL of trade as price updates
                    optionMatrix[i, STRIKE] = option.Strike;// Strike Price of Instrument
                    //optionMatrix[i, PRICEDELTA] = putTrade[1];// Price detal between consicutive positions
                }
            }
        }
        private decimal GetMatrixPnL(decimal[,] matrix)
        {
            decimal matrixPnL = 0;
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                if (matrix[i, TRADING_STATUS] == (decimal)TradeStatus.Open)
                    matrixPnL += matrix[i, POSITION_PnL];
            }
            return matrixPnL;
        }
        private decimal GetMatrixInitialValue(decimal[,] matrix)
        {
            decimal matrixPnL = 0;
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                if (matrix[i, TRADING_STATUS] == (decimal)TradeStatus.Open)
                    matrixPnL += matrix[i, INITIAL_TRADED_PRICE] * matrix[i, QUANTITY];
            }
            return matrixPnL;
        }
        private decimal GetMatrixCurrentValue(decimal[,] matrix)
        {
            decimal matrixPnL = 0;
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                if (matrix[i, TRADING_STATUS] == (decimal)TradeStatus.Open)
                    matrixPnL += matrix[i, CURRENT_PRICE] * matrix[i, QUANTITY];
            }
            return matrixPnL;
        }
        private decimal GetQtyInTrade(decimal[,] matrix)
        {
            decimal currentQty = 0;
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                if (matrix[i, TRADING_STATUS] == (decimal)TradeStatus.Open)
                    currentQty += matrix[i, QUANTITY]; //3: Quantity
            }
            return currentQty;
        }
        private void SortMatrixByPrice(ref decimal[,] matrix)
        {
            decimal[] tempVector = new decimal[10];
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = i + 1; j < matrix.GetLength(0); j++)
                {
                    if (matrix[i, CURRENT_PRICE] > matrix[j, CURRENT_PRICE] )//&& matrix[j, CURRENT_PRICE] != 0)
                    {
                        for (int t = 0; t < matrix.GetLength(1); t++)
                        {
                            tempVector[t] = matrix[i, t];
                            matrix[i, t] = matrix[j, t];
                            matrix[j, t] = tempVector[t];
                        }
                    }
                }
            }
        }
        private void MoveNearTerm(StrangleDataStructure strangleNode, ref decimal[][,] optionMatrix, DateTime? timeOfTrade)
        {
            SortMatrixByPrice(ref optionMatrix[CE]);
            SortMatrixByPrice(ref optionMatrix[PE]);

            decimal callValue = GetMatrixCurrentValue(optionMatrix[CE]);
            decimal putValue = GetMatrixCurrentValue(optionMatrix[PE]);

            decimal currentPutQty = GetQtyInTrade(optionMatrix[PE]);
            decimal currentCallQty = GetQtyInTrade(optionMatrix[CE]);

            
            decimal higherOptionValue = callValue;
            decimal lowerOptionValue = putValue;
            int lowerValueOptionType = PE;
            decimal[,] lowerMatrix = optionMatrix[PE];
            SortedList<decimal, Instrument> lowerOptionUniverse = strangleNode.PutUniverse;
            decimal lowerOptionQty = currentPutQty;
            if (putValue > callValue)
            {
                lowerValueOptionType = CE;
                lowerMatrix = optionMatrix[CE];
                lowerOptionUniverse = strangleNode.CallUniverse;
                higherOptionValue = putValue;
                lowerOptionValue = callValue;
                lowerOptionQty = currentCallQty;
            }

            //TODO: Update callvalue and putvalue at every step above, so that we have updated value here
            decimal valueNeeded = Math.Abs(callValue - putValue);

            int strategyId = strangleNode.ID;
            decimal valueGain = 0;
            bool stoploop = false;
            Instrument option;
            //int nodeCount = 0;
            int startNode = 0;
            int endNode = 0;
            //Check if current lower matrix options can suffice
            int closeCounter = 0;
            ///TODO: GET ONLY ACTIVE OPTIONS HERE..CHECK FOR REMOVING RATHER THAN SETTING CLOSED STATUS
            if (lowerMatrix[lowerMatrix.GetLength(0) - 1, CURRENT_PRICE] * lowerOptionQty > higherOptionValue)
            {
                //No need to new options from universe
                closeCounter = 0;
                GetNodesforValueGain(lowerMatrix, valueNeeded, out startNode, out endNode);

                valueGain = 0;
                //move all nodes less than J to I
                for (int j = 0; j < endNode; j++)//TODO: take length of only those items that are open
                {
                    option = lowerValueOptionType == CE ? strangleNode.CallUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[j, INSTRUMENT_TOKEN])
                        : strangleNode.PutUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[j, INSTRUMENT_TOKEN]);

                    ShortTrade order = PlaceOrder(strangleNode.ID, option, true, Convert.ToInt32(lowerMatrix[j, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);
                    lowerMatrix[j, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                    lowerMatrix[j, CURRENT_PRICE] = order.AveragePrice;
                    closeCounter++;

                    option = lowerValueOptionType == CE ? strangleNode.CallUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[startNode, INSTRUMENT_TOKEN])
                        : strangleNode.PutUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[startNode, INSTRUMENT_TOKEN]);

                    PlaceOrderAndUpdateMatrix(strangleNode.ID, ref lowerMatrix, option, false, Convert.ToInt32(lowerMatrix[j, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);

                    //PlaceOrder(strangleNode.ID, option, false, Convert.ToInt32(lowerMatrix[j, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);
                    //lowerMatrix[startNode, TRADING_STATUS] = (decimal)TradeStatus.Open;
                    //lowerMatrix[startNode, CURRENT_PRICE] = order.AveragePrice;
                    valueGain += (lowerMatrix[startNode, CURRENT_PRICE] - lowerMatrix[j, CURRENT_PRICE]) * lowerMatrix[j, QUANTITY];
                }

                if (valueGain < valueNeeded) // Run the check between J and I..so understand if node from J can be shifted inbetween instead to moving all the way to i
                {
                    valueNeeded -= valueGain; // remaining value
                    valueGain = 0;
                    for (int p = endNode; p <= startNode; p++)
                    {
                        valueGain = (lowerMatrix[p, CURRENT_PRICE] - lowerMatrix[endNode, CURRENT_PRICE]) * lowerMatrix[endNode, QUANTITY];
                        if (valueGain > valueNeeded)
                        {
                            option = lowerValueOptionType == CE ? strangleNode.CallUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[endNode, INSTRUMENT_TOKEN])
                        : strangleNode.PutUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[endNode, INSTRUMENT_TOKEN]);


                            ShortTrade order = PlaceOrder(strangleNode.ID, option, true, Convert.ToInt32(lowerMatrix[endNode, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[endNode, TRADE_ID]);
                            lowerMatrix[endNode, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                            closeCounter++;
                            lowerMatrix[endNode, CURRENT_PRICE] = order.AveragePrice;

                            option = lowerValueOptionType == CE ? strangleNode.CallUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[p, INSTRUMENT_TOKEN])
                       : strangleNode.PutUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[p, INSTRUMENT_TOKEN]);

                            PlaceOrderAndUpdateMatrix(strangleNode.ID, ref lowerMatrix, option, false, Convert.ToInt32(lowerMatrix[endNode, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[endNode, TRADE_ID]);

                            //PlaceOrder(strangleNode.ID, option, false, Convert.ToInt32(lowerMatrix[endNode, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[endNode, TRADE_ID]);
                            //lowerMatrix[p, QUANTITY] += lowerMatrix[endNode, QUANTITY];
                            //lowerMatrix[p, TRADING_STATUS] = (decimal)TradeStatus.Open;
                            //lowerMatrix[p, CURRENT_PRICE] = order.AveragePrice;
                            break;
                        }
                    }
                }
            }
            else
            {
                //options from universe are needed
                //go through all the levels and see where total qty x price > value needed.
                closeCounter = 0;
                SortMatrixByPrice(ref lowerMatrix);
                    List<KeyValuePair<decimal, Instrument>> lowerValueOptions = lowerOptionUniverse.Where(x => x.Value.LastPrice > lowerMatrix[lowerMatrix.GetLength(0) - 1, CURRENT_PRICE]).OrderBy(x => x.Value.LastPrice).ToList();

                    decimal currentQty = currentCallQty + currentPutQty;
                    decimal qtyAvailable = strangleNode.MaxQty - currentQty; //qtyavailable should be 0 here as all the qtys have been checked and taken out already

                //close existing ones and get new ones

                for (int i = 0; i < lowerValueOptions.Count; i++)
                {
                    KeyValuePair<decimal, Instrument> lowerOption = lowerValueOptions.ElementAt(i);
                    if (lowerOption.Value.LastPrice * (qtyAvailable + lowerOptionQty) > higherOptionValue //TODO: Shouldn't this check be with value needed and not higheroptionvalue?
                        && (
                        lowerValueOptionType == CE && lowerOption.Value.Strike >= strangleNode.BaseInstrumentPrice + strangleNode.StrikePriceIncrement * 0.6m ||
                        lowerValueOptionType == PE && lowerOption.Value.Strike <= strangleNode.BaseInstrumentPrice - strangleNode.StrikePriceIncrement * 0.6m
                        )
                        ) 
                    {
                        uint lotSize = strangleNode.CallUniverse.ElementAt(0).Value.LotSize;
                        decimal optionPrice = lowerOption.Value.LastPrice;
                        decimal qty = Math.Floor(higherOptionValue / optionPrice / lotSize) * lotSize;
                        decimal qtyRollover = 0;
                        decimal remainingValue = 0;
                        decimal qty2 = 0;
                        bool optionFound = false;
                        int nodeCount = 0;
                        for (int j = 0; j < lowerMatrix.GetLength(0); j++)
                        {
                            remainingValue = higherOptionValue - qty * optionPrice;

                            if ((lowerOptionQty - qty) * lowerMatrix[j, CURRENT_PRICE] > remainingValue)
                            {
                                qty2 = Math.Ceiling(remainingValue / lowerMatrix[j, CURRENT_PRICE]/lotSize) * lotSize;
                                optionFound = true;
                                nodeCount = j;
                                break;
                            }
                        }
                        if (!optionFound)
                        {
                            qty = Math.Floor(higherOptionValue / optionPrice / lotSize) * lotSize;
                            qty2 = 0;
                        }

                        Instrument instrument = lowerOptionUniverse.Where(x => x.Value.InstrumentToken == lowerMatrix[nodeCount, INSTRUMENT_TOKEN]).First().Value;

                        for (int j = 0; j < lowerMatrix.GetLength(0); j++)
                        {
                            Instrument instrument1 = lowerOptionUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[j, INSTRUMENT_TOKEN]);
                            int quantity = Convert.ToInt32(lowerMatrix[j, QUANTITY]); ;
                            if (j != nodeCount)
                            {
                                ShortTrade order1 = PlaceOrder(strangleNode.ID, instrument1, true, Convert.ToInt32(lowerMatrix[j, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);
                                lowerMatrix[j, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                                closeCounter++;
                                lowerMatrix[j, CURRENT_PRICE] = order1.AveragePrice;
                            }
                            else
                            {
                                if (lowerMatrix[j, QUANTITY] - qty2 > 0)
                                {
                                    quantity = Convert.ToInt32(lowerMatrix[j, QUANTITY] - qty2);
                                    ShortTrade order1 = PlaceOrder(strangleNode.ID, instrument1, true, quantity, timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);
                                    lowerMatrix[j, TRADING_STATUS] = (decimal)TradeStatus.Open;
                                    lowerMatrix[j, CURRENT_PRICE] = order1.AveragePrice;
                                    lowerMatrix[j, QUANTITY] = qty2;
                                }
                                else
                                {
                                    PlaceOrderAndUpdateMatrix(strangleNode.ID, ref lowerMatrix, instrument, false, qty2, timeOfTrade, triggerID: 1);
                                }
                            }
                            //ShortTrade order = PlaceOrder(strangleNode.ID, instrument1, true, quantity, timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);
                            //lowerMatrix[j, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                            //lowerMatrix[j, CURRENT_PRICE] = order.AveragePrice;
                        }


                        //for (int j = 0; j < lowerMatrix.GetLength(0); j++)
                        //{
                        //    Instrument instrument1 = lowerOptionUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[j, INSTRUMENT_TOKEN]);
                        //    int quantity = Convert.ToInt32(lowerMatrix[j, QUANTITY]); ;
                        //    if(instrument1.InstrumentToken == instrument.InstrumentToken )
                        //    {
                        //        if (lowerMatrix[j, QUANTITY] - qty2 > 0)
                        //        {
                        //            quantity = Convert.ToInt32(lowerMatrix[j, QUANTITY] - qty2);
                        //            ShortTrade order1 = PlaceOrder(strangleNode.ID, instrument1, true, quantity, timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);
                        //            lowerMatrix[j, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                        //            lowerMatrix[j, CURRENT_PRICE] = order1.AveragePrice;
                        //            lowerMatrix[j, QUANTITY] = qty2;
                        //        }
                        //        else
                        //        {
                        //            PlaceOrderAndUpdateMatrix(strangleNode.ID, ref lowerMatrix, instrument, false, qty2, timeOfTrade, triggerID: 1);
                        //        }
                        //    }
                        //    ShortTrade order = PlaceOrder(strangleNode.ID, instrument1, true, quantity, timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);
                        //    lowerMatrix[j, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                        //    lowerMatrix[j, CURRENT_PRICE] = order.AveragePrice;
                        //}


                        PlaceOrderAndUpdateMatrix(strangleNode.ID, ref lowerMatrix, lowerOption.Value, false, qty, timeOfTrade, triggerID: 1);
                        //PlaceOrderAndUpdateMatrix(strangleNode.ID, ref lowerMatrix, instrument, false, qty2, timeOfTrade, triggerID: 1);
                        
                        
                        break;
                    }
                }
                //then apply in that and the previous one appropriate level.
            }


            if (closeCounter > 0)
            {
                decimal[,] result = new decimal[lowerMatrix.GetLength(0) - closeCounter, lowerMatrix.GetLength(1)];

                for (int i = 0, j = 0; i < lowerMatrix.GetLength(0); i++)
                {
                    if (lowerMatrix[i, TRADING_STATUS] == (decimal)TradeStatus.Closed)
                    {
                        continue;
                    }
                    else
                    {
                        for (int k = 0, u = 0; k < lowerMatrix.GetLength(1); k++)
                        {
                            result[j, u] = lowerMatrix[i, k];
                            u++;
                        }
                    }
                    j++;
                }
                lowerMatrix = result;
            }

            if (callValue > putValue)
            {
                optionMatrix[PE] = lowerMatrix;
            }
            else
            {
                optionMatrix[CE] = lowerMatrix;
            }

        }

        private void GetNodesforValueGain(decimal[,] lowerMatrix,decimal valueNeeded, out int startNode, out int endNode)
    {
        decimal valueGain = 0;
        for (int i = 1; i < lowerMatrix.GetLength(0); i++)//TODO: take length of only those items that are open
        {
            valueGain = 0;
            for (int j = 0; j < i; j++)//TODO: take length of only those items that are open
            {
                valueGain += (lowerMatrix[i, CURRENT_PRICE] - lowerMatrix[j, CURRENT_PRICE]) * lowerMatrix[j, QUANTITY]; //why is quantity being fixed and not fungible.. make partial trade in qty possible based on lotsize.
                if (valueGain > valueNeeded)
                {
                    startNode = i;
                    endNode = j;
                    return;
                }
            }
        }
            throw new Exception("logic failed!!");
    }
        private void MoveNearTerm_1(StrangleDataStructure strangleNode, ref decimal[][,] optionMatrix, DateTime? timeOfTrade)
        {
            SortMatrixByPrice(ref optionMatrix[CE]);
            SortMatrixByPrice(ref optionMatrix[PE]);

            decimal callValue = GetMatrixCurrentValue(optionMatrix[CE]);
            decimal putValue = GetMatrixCurrentValue(optionMatrix[PE]);

            decimal higherOptionValue = Math.Max(callValue, putValue);
            decimal lowerOptionValue = Math.Min(callValue, putValue);

            int lowValueOptionType;
            decimal[,] lowerMatrix;
            SortedList<decimal, Instrument> lowerOptionUniverse;
            if (callValue > putValue)
            {
                lowValueOptionType = PE;
                lowerMatrix = optionMatrix[PE];
                lowerOptionUniverse = strangleNode.PutUniverse;
            }
            else
            {
                lowValueOptionType = CE;
                lowerMatrix = optionMatrix[CE];
                lowerOptionUniverse = strangleNode.CallUniverse;
            }

            //TODO: Update callvalue and putvalue at every step above, so that we have updated value here
            decimal valueNeeded = Math.Abs(callValue - putValue);

            int strategyId = strangleNode.ID;
            decimal valueGain = 0;
            bool stoploop = false;
            Instrument option;
            int nodeCount = 0;
            //Iterate over sorted array and move far OTMs to near TMs
            //step 1: Check the value needed and check the value difference between each positions. 
            //step 2: Run fibbonacci to determine movements for value needed. 
            //Step 3: on last run check for each position
            for (int i = 1; i < lowerMatrix.GetLength(0); i++)//TODO: take length of only those items that are open
            {
                valueGain = 0;
                for (int j = 0; j < i; j++)//TODO: take length of only those items that are open
                {
                    valueGain += (lowerMatrix[i, CURRENT_PRICE] - lowerMatrix[j, CURRENT_PRICE]) * lowerMatrix[j, QUANTITY]; //why is quantity being fixed and not fungible.. make partial trade in qty possible based on lotsize.
                    if (valueGain > valueNeeded)
                    {
                        stoploop = true;
                        nodeCount = j;
                        break;
                    }
                }
                if (!stoploop || valueGain == 0) // can you check with i and J instead of stoploop
                {
                    continue;
                }
                else
                {
                    valueGain = 0;
                    //move all nodes less than J to I
                    for (int j = 0; j < nodeCount; j++)//TODO: take length of only those items that are open
                    {
                        option = strangleNode.TradedStrangle.Options.First(x => x.InstrumentToken == lowerMatrix[j, INSTRUMENT_TOKEN]);
                        ShortTrade order = PlaceOrder(strangleNode.ID, option, true, Convert.ToInt32(lowerMatrix[j, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);
                        lowerMatrix[j, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                        lowerMatrix[j, CURRENT_PRICE] = order.AveragePrice;

                        option = strangleNode.TradedStrangle.Options.First(x => x.InstrumentToken == lowerMatrix[i, INSTRUMENT_TOKEN]);
                        PlaceOrder(strangleNode.ID, option, false, Convert.ToInt32(lowerMatrix[j, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);
                        lowerMatrix[i, TRADING_STATUS] = (decimal)TradeStatus.Open;
                        lowerMatrix[i, CURRENT_PRICE] = order.AveragePrice;
                        valueGain += (lowerMatrix[i, CURRENT_PRICE] - lowerMatrix[j, CURRENT_PRICE]) * lowerMatrix[j, QUANTITY];
                    }

                    if (valueGain < valueNeeded) // Run the check between J and I..so understand if node from J can be shifted inbetween instead to moving all the way to i
                    {
                        valueNeeded -= valueGain;
                        valueGain = 0;
                        for (int p = nodeCount; p <= i; p++)
                        {
                            valueGain += (lowerMatrix[p, CURRENT_PRICE] - lowerMatrix[nodeCount, CURRENT_PRICE]) * lowerMatrix[nodeCount, QUANTITY];
                            if (valueGain > valueNeeded)
                            {
                                option = strangleNode.TradedStrangle.Options.First(x => x.InstrumentToken == lowerMatrix[nodeCount, INSTRUMENT_TOKEN]);
                                ShortTrade order = PlaceOrder(strangleNode.ID, option, true, Convert.ToInt32(lowerMatrix[nodeCount, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[nodeCount, TRADE_ID]);
                                lowerMatrix[nodeCount, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                                lowerMatrix[nodeCount, CURRENT_PRICE] = order.AveragePrice;

                                option = strangleNode.TradedStrangle.Options.First(x => x.InstrumentToken == lowerMatrix[p, INSTRUMENT_TOKEN]);
                                PlaceOrder(strangleNode.ID, option, false, Convert.ToInt32(lowerMatrix[p, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[nodeCount, TRADE_ID]);
                                lowerMatrix[p, TRADING_STATUS] = (decimal)TradeStatus.Open;
                                lowerMatrix[p, CURRENT_PRICE] = order.AveragePrice;
                                break;
                            }
                        }
                    }

                    break;
                }
            }

            lowerOptionValue = GetMatrixCurrentValue(lowerMatrix);

            if (lowerOptionValue < 0.9m * higherOptionValue)
            {
                valueNeeded = higherOptionValue - lowerOptionValue;


                SortMatrixByPrice(ref lowerMatrix);
                List<KeyValuePair<decimal, Instrument>> lowerValueOptions = lowerOptionUniverse.Where(x => x.Value.LastPrice > lowerMatrix[lowerMatrix.GetLength(0) - 1, CURRENT_PRICE]).OrderBy(x => x.Value.LastPrice).ToList();

                //decimal currentPutQty = GetQtyInTrade(lowerMatrix);

                decimal currentPutQty = GetQtyInTrade(optionMatrix[PE]);
                decimal currentCallQty = GetQtyInTrade(optionMatrix[CE]);
                decimal lowerOptionQty = lowValueOptionType == CE ? currentCallQty : currentPutQty;

                decimal currentQty = currentCallQty + currentPutQty;
                decimal qtyAvailable = strangleNode.MaxQty - currentQty;

                for (int i = 0; i < lowerValueOptions.Count; i++)
                {
                    KeyValuePair<decimal, Instrument> lowerOption = lowerValueOptions.ElementAt(i);

                    lowerOptionValue = GetMatrixCurrentValue(lowerMatrix);
                    if (lowerOptionValue >= 0.9m * higherOptionValue)
                    {
                        break;
                    }
                    if (lowerOption.Value.LastPrice * (qtyAvailable + lowerOptionQty) > valueNeeded)
                    {
                        uint lotSize = strangleNode.CallUniverse.ElementAt(0).Value.LotSize;
                        decimal qty = Math.Ceiling(valueNeeded / lowerOption.Value.LastPrice / lotSize) * lotSize;
                        decimal qtyRollover = 0;
                        for (int j = 0; j < lowerMatrix.GetLength(0); j++)
                        {
                            qtyRollover += lowerMatrix[j, QUANTITY];

                            if (qtyRollover <= qty) //Qty should be fungible so that partial qtys can be trasferred to more appropriate delta neutral
                            {
                                ShortTrade order = PlaceOrder(strangleNode.ID, lowerOption.Value, true, Convert.ToInt32(lowerMatrix[j, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[nodeCount, TRADE_ID]);
                                lowerMatrix[j, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                                lowerMatrix[j, CURRENT_PRICE] = order.AveragePrice;

                                order = PlaceOrder(strangleNode.ID, lowerOption.Value, false, Convert.ToInt32(lowerMatrix[j, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[nodeCount, TRADE_ID]);


                                decimal[,] data = new decimal[1, 10];
                                data[0, INSTRUMENT_TOKEN] = lowerOption.Value.InstrumentToken;
                                data[0, INITIAL_TRADED_PRICE] = order.AveragePrice;
                                data[0, CURRENT_PRICE] = order.AveragePrice;
                                data[0, QUANTITY] = order.Quantity;
                                data[0, TRADING_STATUS] = (decimal)TradeStatus.Open;
                                data[0, TRADE_ID] = order.TriggerID;
                                data[0, POSITION_PnL] = 0;// PnL of trade as price updates
                                data[0, STRIKE] = lowerOption.Value.Strike;// Strike Price of Instrument

                                strangleNode.AddMatrixRow(data, (InstrumentType)lowValueOptionType);



                                break;
                            }
                        }
                    }
                }

            }
            //if (callValue > putValue)
            //{
            //    optionMatrix[1] = lowerMatrix;
            //}
            //else
            //{
            //    optionMatrix[0] = lowerMatrix;
            //}
        }
        private decimal GetMaxStrike(decimal[,] matrix)
        {
            decimal instrumentToken = 0;
            decimal strike = 0;
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                if (/*matrix[i, TRADING_STATUS] == Convert.ToInt32(TradeStatus.Open) &&*/ (matrix[i, STRIKE] > strike || strike == 0))
                {
                    strike = matrix[i, STRIKE];
                    instrumentToken = matrix[i, INSTRUMENT_TOKEN];
                }
            }
            return strike;
        }
        private decimal GetMinStrike(decimal[,] matrix)
        {
            decimal instrumentToken = 0;
            decimal strike = 0;
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                if (/*matrix[i, TRADING_STATUS] == Convert.ToInt32(TradeStatus.Open) && */ (matrix[i, STRIKE] < strike || strike == 0))
                {
                    strike = matrix[i, STRIKE];
                    instrumentToken = matrix[i, INSTRUMENT_TOKEN];
                }
            }
            return strike;
        }
        private decimal[][] InitialTrade(StrangleDataStructure strangleNode, Tick[] ticks)
        {
            decimal baseInstrumentPrice = strangleNode.BaseInstrumentPrice;
            decimal DistanceFromBaseInstrumentPrice = 100;
            decimal minPrice = 10;
            decimal maxPrice = 500;

            SortedList<decimal, Instrument> callUniverse = strangleNode.CallUniverse;
            SortedList<decimal, Instrument> putUniverse = strangleNode.PutUniverse;

            KeyValuePair<decimal, Instrument> callNode = callUniverse.
                Where(x => x.Value.LastPrice > minPrice && x.Value.LastPrice < maxPrice
                && x.Key > baseInstrumentPrice + DistanceFromBaseInstrumentPrice).OrderBy(x => x.Key).First();

            KeyValuePair<decimal, Instrument> putNode = putUniverse.
                Where(x => x.Value.LastPrice > minPrice && x.Value.LastPrice < maxPrice
                && x.Key < baseInstrumentPrice - DistanceFromBaseInstrumentPrice).OrderBy(x => x.Key).Last();

            Instrument call = callNode.Value;
            Instrument put = putNode.Value;

            strangleNode.UnBookedPnL = 0;
            strangleNode.BookedPnL = 0;

            //TradedStrangle tradedStrangle = strangleNode.TradedStrangle == null ? new TradedStrangle() : strangleNode.TradedStrangle;
            //tradedStrangle.Options.Add(call);
            //tradedStrangle.Options.Add(put);

            int callQty = 0; decimal callPrice = call.LastPrice; int putQty = 0; decimal putPrice = put.LastPrice;
            if (callPrice >= putPrice)
            {
                callQty = Convert.ToInt32(putPrice * strangleNode.InitialQty / callPrice);
                callQty = callQty - Convert.ToInt32(callQty % call.LotSize);
                putQty = strangleNode.InitialQty;
            }
            else
            {
                putQty = Convert.ToInt32(callPrice * strangleNode.InitialQty / putPrice);
                putQty = putQty - Convert.ToInt32(putQty % put.LotSize);
                callQty = strangleNode.InitialQty;
            }

            int tradeId =  0;// tradedStrangle.SellTrades.Count;

            ShortTrade callSellTrade = PlaceOrder(strangleNode.ID, call, buyOrder: false, callQty, tickTime: ticks[0].Timestamp, triggerID: tradeId);
            //tradedStrangle.SellTrades.Add(callSellTrade);
            ShortTrade putSellTrade = PlaceOrder(strangleNode.ID, put, buyOrder: false, putQty, tickTime: ticks[0].Timestamp, triggerID: tradeId);
            //tradedStrangle.SellTrades.Add(putSellTrade);

            strangleNode.NetCallQtyInTrade += callSellTrade.Quantity + putSellTrade.Quantity;
            strangleNode.UnBookedPnL += (callSellTrade.Quantity * callSellTrade.AveragePrice + putSellTrade.Quantity * putSellTrade.AveragePrice);

            //tradedStrangle.UnbookedPnl = strangleNode.UnBookedPnL;
            //tradedStrangle.TradingStatus = PositionStatus.Open;

            //strangleNode.TradedStrangle = tradedStrangle;

            Decimal[][] tradeDetails = new decimal[2][];
            tradeDetails[CE] = new decimal[10];
            tradeDetails[CE][INSTRUMENT_TOKEN] = call.InstrumentToken;
            tradeDetails[CE][INITIAL_TRADED_PRICE] = callSellTrade.AveragePrice;
            tradeDetails[CE][CURRENT_PRICE] = callSellTrade.AveragePrice;
            tradeDetails[CE][QUANTITY] = callSellTrade.Quantity;
            tradeDetails[CE][TRADE_ID] = tradeId;
            tradeDetails[CE][TRADING_STATUS] = Convert.ToDecimal(TradeStatus.Open);
            tradeDetails[CE][POSITION_PnL] = callSellTrade.Quantity * callSellTrade.AveragePrice;
            tradeDetails[CE][STRIKE] = call.Strike;
            tradeDetails[PE] = new decimal[10];
            tradeDetails[PE][INSTRUMENT_TOKEN] = put.InstrumentToken;
            tradeDetails[PE][INITIAL_TRADED_PRICE] = putSellTrade.AveragePrice;
            tradeDetails[PE][CURRENT_PRICE] = putSellTrade.AveragePrice;
            tradeDetails[PE][QUANTITY] = putSellTrade.Quantity;
            tradeDetails[PE][TRADE_ID] = tradeId;
            tradeDetails[PE][TRADING_STATUS] = Convert.ToDecimal(TradeStatus.Open);
            tradeDetails[PE][POSITION_PnL] = putSellTrade.Quantity * putSellTrade.AveragePrice;
            tradeDetails[PE][STRIKE] = put.Strike;
            return tradeDetails;
        }
        private void PlaceOrderAndUpdateMatrix(int strangleID, ref decimal[,] optionMatrix, Instrument instrument, 
            bool buyOrder, decimal quantity, DateTime? tickTime = null, uint token = 0, decimal triggerID = 0)
        {
            ShortTrade trade = PlaceOrder(strangleID, instrument, buyOrder, Convert.ToInt32(quantity), tickTime, token, triggerID);

            Decimal[,] newMatrix = new decimal[optionMatrix.GetLength(0) + 1, optionMatrix.GetLength(1)];
            Array.Copy(optionMatrix, newMatrix, optionMatrix.Length - 1);

            newMatrix[newMatrix.GetLength(0) - 1, INSTRUMENT_TOKEN] = instrument.InstrumentToken;
            newMatrix[newMatrix.GetLength(0) - 1, INITIAL_TRADED_PRICE] = trade.AveragePrice;
            newMatrix[newMatrix.GetLength(0) - 1, CURRENT_PRICE] = trade.AveragePrice;
            newMatrix[newMatrix.GetLength(0) - 1, QUANTITY] = trade.Quantity;
            newMatrix[newMatrix.GetLength(0) - 1, TRADE_ID] = triggerID;
            newMatrix[newMatrix.GetLength(0) - 1, TRADING_STATUS] = Convert.ToDecimal(TradeStatus.Open);
            newMatrix[newMatrix.GetLength(0) - 1, POSITION_PnL] = trade.Quantity * trade.AveragePrice;
            newMatrix[newMatrix.GetLength(0) - 1, STRIKE] = instrument.Strike;

            optionMatrix = newMatrix;
        }


//private decimal[] InitialTrade(StrangleDataStructure strangleNode,SortedList<decimal, Instrument> optionUniverse,  Tick[] ticks)
//        {
//            decimal baseInstrumentPrice = strangleNode.BaseInstrumentPrice;
//            decimal DistanceFromBaseInstrumentPrice = 150;
//            decimal minPrice = 10;
//            decimal maxPrice = 60;

//            KeyValuePair<decimal, Instrument> optionNode = optionUniverse.
//                Where(x => x.Value.LastPrice > minPrice && x.Value.LastPrice < maxPrice
//                && Math.Abs(x.Key - Math.Round(baseInstrumentPrice / 100, 0) * 100) >= DistanceFromBaseInstrumentPrice).OrderBy(x => x.Key).First();

//            Instrument option = optionNode.Value;
//            strangleNode.UnBookedPnL = 0;
//            strangleNode.BookedPnL = 0;

//            TradedStrangle tradedStrangle = strangleNode.TradedStrangle == null? new TradedStrangle(): strangleNode.TradedStrangle;
//            tradedStrangle.Options.Add(option);

//            int tradeId = tradedStrangle.SellTrades.Count;

//            ShortTrade sellTrade = PlaceOrder(strangleNode.ID, option, buyOrder: false, strangleNode.InitialQty, tickTime: ticks[0].Timestamp, triggerID: tradeId);
//            tradedStrangle.SellTrades.Add(sellTrade);

//            strangleNode.NetCallQtyInTrade += sellTrade.Quantity;
//            strangleNode.UnBookedPnL += sellTrade.Quantity * sellTrade.AveragePrice;

//            tradedStrangle.UnbookedPnl = strangleNode.UnBookedPnL;
//            tradedStrangle.TradingStatus = PositionStatus.Open;

//            strangleNode.TradedStrangle = tradedStrangle;

//            Decimal[] tradeDetails = new decimal[10];
//            tradeDetails[INSTRUMENT_TOKEN] = option.InstrumentToken;
//            tradeDetails[INITIAL_TRADED_PRICE] = sellTrade.AveragePrice;
//            tradeDetails[CURRENT_PRICE] = sellTrade.AveragePrice;
//            tradeDetails[QUANTITY] = sellTrade.Quantity;
//            tradeDetails[TRADE_ID] = tradeId;
//            tradeDetails[TRADING_STATUS] = Convert.ToDecimal(TradeStatus.Open);
//            tradeDetails[POSITION_PnL] = sellTrade.Quantity * sellTrade.AveragePrice;
//            tradeDetails[STRIKE] = option.Strike;
//            return tradeDetails;
//        }

        /// <summary>
        /// move to next node when the premium equals to next node
        /// </summary>
        /// <param name="strangleNode"></param>
        /// <param name="tradedStrangle"></param>
        /// <param name="timeOfOrder"></param>
        /// <param name="ticks"></param>
        //private void CheckAndTrade(StrangleDataList strangleNode, TradedStrangle tradedStrangle, IGrouping<int, ShortTrade> strangleTrade, DateTime timeOfOrder, Tick[] ticks)
        //{

        //    int triggerId = strangleTrade.Key;
        //    List<ShortTrade> optionTrades = strangleTrade.ToList();
        //    List<Instrument> options = tradedStrangle.Options;

        //    //List<Instrument> tradedCalls = tradedStrangle.Options.Where(c=>c.InstrumentToken = optionTrades.Where(x => x.InstrumentType == "CE").Select(it=>it.InstrumentToken)).ToList();

        //    //var innerJoin = tradedOptions.Join(optionTrades, ot => ot.InstrumentToken, c => c.InstrumentToken, (ot, c) => ot);

        //    var tradedOptions = from option in options
        //                        join trade in optionTrades on option.InstrumentToken equals trade.InstrumentToken
        //                         select option;

        //    List<Instrument> tradedCalls = tradedOptions.Where(x => x.InstrumentType == "CE").ToList();
        //    List<Instrument> tradedPuts = tradedOptions.Where(x => x.InstrumentType == "PE").ToList();

        //    decimal tradedCallValue = optionTrades.Where(x => x.InstrumentType == "CE").Sum(x => x.Quantity * x.AveragePrice);
        //    decimal tradedPutValue = optionTrades.Where(x => x.InstrumentType == "PE").Sum(x => x.Quantity * x.AveragePrice);
        //    decimal tradedValue = tradedCallValue + tradedPutValue;

        //    decimal currentCallValue = optionTrades.Where(x => x.InstrumentType == "CE").Sum(x => x.Quantity * tradedStrangle.Options.First(c => c.InstrumentToken == x.InstrumentToken).LastPrice);
        //    decimal currentPutValue = optionTrades.Where(x => x.InstrumentType == "PE").Sum(x => x.Quantity * tradedStrangle.Options.First(c => c.InstrumentToken == x.InstrumentToken).LastPrice);
        //    decimal currentValue = currentCallValue + currentPutValue;


        //    if(currentCallValue > currentPutValue)
        //    {
        //        var sortedPuts = from tp in tradedPuts
        //                         orderby tp.Strike
        //                         select new { tp.Strike, tp.LastPrice };



        //        //check if currentcallvalue is equal to next put node current value

        //        //Move 50% to next node
        //        decimal nextCallStrike = bookProfit ? openOption.Option.Strike - strangleNode.StrikePriceIncrement : openOption.Option.Strike + strangleNode.StrikePriceIncrement;
        //        nextOption = strangleNode.TradedCalls.FirstOrDefault(x => x.Option.Strike == nextCallStrike);

        //        if (nextOption == null)
        //        {
        //            //Instrument nextOptionFromUniverse = strangleNode.CallUniverse[nextCallStrike];

        //            if (strangleNode.CallUniverse.TryGetValue(nextCallStrike, out nextOptionFromUniverse))
        //            {
        //                nextOption = new TradedInstrument() { Option = nextOptionFromUniverse };
        //            }
        //            else
        //            {
        //                PopulateReferenceStrangleData(strangleNode, ticks, nextCallStrike);
        //                return;
        //            }
        //        }
        //        strangleNode.TradedCalls.Add(nextOption);



        //    }

        //    List<Instrument> tradedOptions = strangleTrade.Options;
        //    for (int i = 0; i < tradedOptions.Count; i++)
        //    {
        //        Instrument openOption = tradedOptions[i];

        //        ShortTrade lastSellTrade = openOption.SellTrades.Last();

        //        int lastSellQty = lastSellTrade.Quantity > strangleNode.StepQty ? lastSellTrade.Quantity / 2 : strangleNode.StepQty;
        //        decimal lastSellPrice = lastSellTrade.AveragePrice;
        //        decimal currentPrice = openOption.Option.LastPrice;

        //        //TODO: Do we need to move one node based on opposite type of node, so move call based on put or vice-versa
        //        if (openOption.TradingStatus == PositionStatus.Open)
        //        {
        //            if (currentPrice > lastSellPrice * 1.2m)
        //            {
        //                MoveNodes(openOption, PositionStatus.PercentClosed50, lastSellQty, lastSellPrice, timeOfOrder, strangleNode, false, ticks);
        //            }
        //            if (currentPrice < lastSellPrice * 0.8m)
        //            {
        //                MoveNodes(openOption, PositionStatus.PercentClosed50, lastSellQty, lastSellPrice, timeOfOrder, strangleNode, true, ticks);
        //            }
        //        }
        //        else if (openOption.TradingStatus == PositionStatus.PercentClosed50)
        //        {
        //            if (currentPrice > lastSellPrice * 1.4m)
        //            {
        //                MoveNodes(openOption, PositionStatus.Closed, lastSellQty, lastSellPrice, timeOfOrder, strangleNode, false, ticks);
        //            }
        //            if (currentPrice < lastSellPrice * 0.6m)
        //            {
        //                MoveNodes(openOption, PositionStatus.Closed, lastSellQty, lastSellPrice, timeOfOrder, strangleNode, true, ticks);
        //            }
        //        }

        //        #region Commented Code
        //        ////if option lost more than 20% from last trade, close last trade
        //        //if (openOption.TradingStatus == PositionStatus.Open &&
        //        //    (openOption.Option.LastPrice > lastSellTrade.AveragePrice * 1.2m || openOption.Option.LastPrice < lastSellTrade.AveragePrice * 0.8m))
        //        //{
        //        //    openOption.TradingStatus = PositionStatus.PercentClosed50; //TODO: Check qty is half of initial qty to assign percentclosed50 weightage. Else assign closed.
        //        //    ShortTrade closeTrade = PlaceOrder(strangleNode.ID, openOption.Option, true, lastSellQty, timeOfOrder);
        //        //    openOption.BuyTrades.Add(closeTrade);
        //        //    openOption.BookedPnL += lastSellTrade.AveragePrice * lastSellQty - closeTrade.AveragePrice * closeTrade.Quantity;
        //        //    strangleNode.BookedPnL += openOption.BookedPnL;

        //        //    TradedInstrument nextOption= null;
        //        //    if (openOption.Option.InstrumentType.Trim() == "CE")
        //        //    {
        //        //        //Move 50% to next node
        //        //        decimal nextCallStrike = openOption.Option.Strike + strangleNode.StrikePriceIncrement;
        //        //        nextOption = strangleNode.TradedCalls.FirstOrDefault(x => x.Option.Strike == nextCallStrike);

        //        //        if (nextOption == null)
        //        //        {
        //        //            Instrument nextOptionFromUniverse = strangleNode.CallUniverse[nextCallStrike];
        //        //            nextOption = new TradedInstrument() { Option = nextOptionFromUniverse };
        //        //        }
        //        //        strangleNode.TradedCalls.Add(nextOption);
        //        //    }
        //        //    else if (openOption.Option.InstrumentType.Trim() == "PE")
        //        //    {
        //        //        //Move 50% to next node
        //        //        decimal nextPutStrike = openOption.Option.Strike - strangleNode.StrikePriceIncrement;
        //        //        nextOption = strangleNode.TradedPuts.FirstOrDefault(x => x.Option.Strike == nextPutStrike);
        //        //        if (nextOption == null)
        //        //        {
        //        //            Instrument nextOptionFromUniverse = strangleNode.PutUniverse[nextPutStrike];
        //        //            nextOption = new TradedInstrument() { Option = nextOptionFromUniverse };
        //        //        }
        //        //            strangleNode.TradedPuts.Add(nextOption);
        //        //    }

        //        //    nextOption.TradingStatus = PositionStatus.Open;
        //        //    ShortTrade openTrade = PlaceOrder(strangleNode.ID, nextOption.Option, false, lastSellQty, timeOfOrder);
        //        //    nextOption.SellTrades.Add(openTrade);
        //        //    nextOption.UnbookedPnl += openTrade.AveragePrice * lastSellQty;
        //        //    strangleNode.UnBookedPnL += nextOption.UnbookedPnl;

        //        //}
        //        //else if (openOption.TradingStatus == PositionStatus.PercentClosed50 &&
        //        //    (openOption.Option.LastPrice > lastSellTrade.AveragePrice * 1.4m || openOption.Option.LastPrice < lastSellTrade.AveragePrice * 0.6m))
        //        //{
        //        //    openOption.TradingStatus = PositionStatus.Closed;
        //        //    ShortTrade closeTrade = PlaceOrder(strangleNode.ID, openOption.Option, true, lastSellQty, timeOfOrder);
        //        //    openOption.BuyTrades.Add(closeTrade);
        //        //    openOption.BookedPnL += lastSellTrade.AveragePrice * lastSellQty - closeTrade.AveragePrice * closeTrade.Quantity;
        //        //    strangleNode.BookedPnL += openOption.BookedPnL;

        //        //    TradedInstrument nextOption = null;
        //        //    if (openOption.Option.InstrumentType.Trim() == "CE")
        //        //    {
        //        //        //Move 50% to next node
        //        //        decimal nextCallStrike = openOption.Option.Strike + strangleNode.StrikePriceIncrement;
        //        //        nextOption = strangleNode.TradedCalls.FirstOrDefault(x => x.Option.Strike == nextCallStrike);

        //        //        if (nextOption == null)
        //        //        {
        //        //            Instrument nextOptionFromUniverse = strangleNode.CallUniverse[nextCallStrike];
        //        //            nextOption = new TradedInstrument() { Option = nextOptionFromUniverse };
        //        //        }
        //        //        strangleNode.TradedCalls.Add(nextOption);
        //        //    }
        //        //    else if (openOption.Option.InstrumentType.Trim() == "PE")
        //        //    {
        //        //        //Move 50% to next node
        //        //        decimal nextPutStrike = openOption.Option.Strike - strangleNode.StrikePriceIncrement;
        //        //        nextOption = strangleNode.TradedPuts.FirstOrDefault(x => x.Option.Strike == nextPutStrike);
        //        //        if (nextOption == null)
        //        //        {
        //        //            Instrument nextOptionFromUniverse = strangleNode.PutUniverse[nextPutStrike];
        //        //            nextOption = new TradedInstrument() { Option = nextOptionFromUniverse };
        //        //        }
        //        //        strangleNode.TradedPuts.Add(nextOption);
        //        //    }

        //        //    nextOption.TradingStatus = PositionStatus.Open;
        //        //    ShortTrade openTrade = PlaceOrder(strangleNode.ID, nextOption.Option, false, lastSellQty, timeOfOrder);
        //        //    nextOption.SellTrades.Add(openTrade);
        //        //    nextOption.UnbookedPnl += openTrade.AveragePrice * lastSellQty;
        //        //    strangleNode.UnBookedPnL += nextOption.UnbookedPnl;
        //        //}
        //        #endregion
        //    }
        //}
        private void BookProfitFromLastTrade(TradedInstrument openOption, PositionStatus positionStatus, int tradeQty, decimal lastTradePrice, DateTime? tradeTime, StrangleDataList strangleNode, bool bookProfit, Tick[] ticks)
        {
            openOption.TradingStatus = (PositionStatus)((int)positionStatus - 1); //PositionStatus.Open; //TODO: Check qty is half of initial qty to assign percentclosed50 weightage. Else assign closed.
            ShortTrade closeTrade = PlaceOrder(strangleNode.ID, openOption.Option, true, tradeQty, tradeTime);
            openOption.BuyTrades.Add(closeTrade);
            openOption.BookedPnL += lastTradePrice * tradeQty - closeTrade.AveragePrice * closeTrade.Quantity;
            strangleNode.BookedPnL += openOption.BookedPnL;
        }
       
        private void TakeInitialPositions(StrangleDataStructure strangleNode, Tick[] ticks)
        {
            decimal baseInstrumentPrice = strangleNode.BaseInstrumentPrice;
            decimal DistanceFromBaseInstrumentPrice = 150;
            decimal minPrice = 10;
            decimal maxPrice = 40;

            KeyValuePair<decimal, Instrument> call = strangleNode.CallUniverse.
                Where(x => x.Value.LastPrice > minPrice && x.Value.LastPrice < maxPrice 
                && x.Key >= Math.Round(baseInstrumentPrice / 100, 0) * 100 + DistanceFromBaseInstrumentPrice).OrderBy(x=>x.Key).First();

            KeyValuePair<decimal, Instrument> put = strangleNode.PutUniverse.
                Where(x => x.Value.LastPrice > minPrice && x.Value.LastPrice < maxPrice
                && x.Key <= Math.Round(baseInstrumentPrice / 100, 0) * 100 - DistanceFromBaseInstrumentPrice).OrderByDescending(x => x.Key).First();

            Instrument callOption = call.Value;
            Instrument putOption = put.Value;

            strangleNode.UnBookedPnL = 0;
            strangleNode.BookedPnL = 0;

            TradedStrangle tradedStrangle = new TradedStrangle();
            //tradedStrangle.Call.Add(callOption);
            //tradedStrangle.Put.Add(putOption);
            tradedStrangle.Options.Add(callOption);
            tradedStrangle.Options.Add(putOption);

            int tradeId = tradedStrangle.SellTrades.Count;
            
            ShortTrade sellTrade = PlaceOrder(strangleNode.ID, callOption, buyOrder: false, strangleNode.InitialQty, tickTime: ticks[0].Timestamp, triggerID:tradeId);
            tradedStrangle.SellTrades.Add(sellTrade);

            //TradedInstrument cInstrument = new TradedInstrument() { Option = callOption };
            //cInstrument.SellTrades = new List<ShortTrade>();
            //cInstrument.SellTrades.Add(sellTrade);
            //cInstrument.TradingStatus = PositionStatus.Open;
            strangleNode.NetCallQtyInTrade += sellTrade.Quantity;
            strangleNode.UnBookedPnL += sellTrade.Quantity * sellTrade.AveragePrice;

            sellTrade = PlaceOrder(strangleNode.ID, putOption, buyOrder: false, strangleNode.InitialQty, tickTime: ticks[0].Timestamp, triggerID:tradeId);
            tradedStrangle.SellTrades.Add(sellTrade);
            //TradedInstrument pInstrument = new TradedInstrument() { Option = putOption };
            //pInstrument.SellTrades = new List<ShortTrade>();
            //pInstrument.SellTrades.Add(sellTrade);
            //pInstrument.TradingStatus = PositionStatus.Open;
            strangleNode.NetPutQtyInTrade += sellTrade.Quantity;
            strangleNode.UnBookedPnL += sellTrade.Quantity * sellTrade.AveragePrice;

            tradedStrangle.UnbookedPnl = strangleNode.UnBookedPnL;
            tradedStrangle.TradingStatus = PositionStatus.Open;

            //strangleNode.TradedCalls = new List<TradedInstrument>();
            //strangleNode.TradedCalls.Add(cInstrument);
            //strangleNode.TradedPuts = new List<TradedInstrument>();
            //strangleNode.TradedPuts.Add(pInstrument);
            //ActiveStrangles.Add(strangleNode.ID, strangleNode);

            

        }
        private SortedList<decimal, decimal> UpdateMaxPainStrike(StrangleDataStructure strangleNode, Tick[] ticks)
        {
            //Check and Fill Reference Strangle Data
            //if (strikePrice == 0 || strikePrice != strangleNode.MaxPainStrike)
            //{
            bool allpopulated = PopulateReferenceStrangleData(strangleNode, ticks);
            //}
            if (!allpopulated) return null;

            SortedList<decimal, Instrument> calls = strangleNode.CallUniverse;
            SortedList<decimal, Instrument> puts = strangleNode.PutUniverse;

            IList<decimal> strikePrices = calls.Keys;
            decimal pain, maxPain = 0;
            decimal maxPainStrike = 0;
            SortedList<decimal, decimal> maxPainStrikes = new SortedList<decimal, decimal>();

            foreach (decimal expiryStrike in strikePrices)
            {
                pain = 0;
                IEnumerable<Decimal> putStrikes = strikePrices.Where(x => x < expiryStrike);
                IEnumerable<Decimal> callStrikes = strikePrices.Where(x => x > expiryStrike);

                foreach (decimal strikePrice in putStrikes)
                {
                    pain += (puts[strikePrice].OI) * (expiryStrike - strikePrice);
                }
                foreach (decimal strikePrice in callStrikes)
                {
                    pain += (calls[strikePrice].OI) * (strikePrice - expiryStrike);
                }

                //if (pain < maxPain || (maxPain == 0 && pain != 0))
                //{
                //    maxPain = pain;
                //    maxPainStrike = expiryStrike;
                //}

                if(pain != 0)
                maxPainStrikes.Add(pain, expiryStrike);
            }

            return maxPainStrikes;
        }
        private SortedList<string, decimal> UpdateMaxPainStrike_OIChange(StrangleDataStructure strangleNode, Tick[] ticks)
        {
            //Check and Fill Reference Strangle Data
            //if (strikePrice == 0 || strikePrice != strangleNode.MaxPainStrike)
            //{
            bool allpopulated = PopulateReferenceStrangleData(strangleNode, ticks);
            //} 
            if (!allpopulated) return null;

            SortedList<decimal, Instrument> calls = strangleNode.CallUniverse;
            SortedList<decimal, Instrument> puts = strangleNode.PutUniverse;

            IList<decimal> strikePrices = calls.Keys;
            decimal pain, maxPain = 0;
            decimal maxPainStrike = 0;
            SortedList<string, decimal> maxPainStrikes = new SortedList<string, decimal>();

            decimal expiryStrike = strangleNode.BaseInstrumentPrice;
            //foreach (decimal expiryStrike in strikePrices)
            //{
                pain = 0;
            //IEnumerable<Decimal> putStrikes = strikePrices.Where(x => x < expiryStrike);
            //IEnumerable<Decimal> callStrikes = strikePrices.Where(x => x > expiryStrike);

            

                foreach (decimal strikePrice in strikePrices)
                {
                maxPainStrikes.Add(strikePrice + "PE", puts[strikePrice].OI);
                }
                foreach (decimal strikePrice in strikePrices)
                {
                maxPainStrikes.Add(strikePrice + "CE", calls[strikePrice].OI);
                }

                //if (pain < maxPain || (maxPain == 0 && pain != 0))
                //{
                //    maxPain = pain;
                //    maxPainStrike = expiryStrike;
                //}

              
               
            //}

            return maxPainStrikes;
        }

        private SortedList<decimal, decimal> UpdateMaxPainStrike_Delta(StrangleDataStructure strangleNode, Tick[] ticks)
        {
            //Check and Fill Reference Strangle Data
            //if (strikePrice == 0 || strikePrice != strangleNode.MaxPainStrike)
            //{
            bool allpopulated = PopulateReferenceStrangleData(strangleNode, ticks);
            //}
            if (!allpopulated) return null;

            SortedList<decimal, Instrument> calls = strangleNode.CallUniverse;
            SortedList<decimal, Instrument> puts = strangleNode.PutUniverse;

            IList<decimal> strikePrices = calls.Keys;
            decimal pain, maxPain = 0;
            decimal maxPainStrike = 0;
            SortedList<decimal, decimal> maxPainStrikes = new SortedList<decimal, decimal>();
            foreach (decimal expiryStrike in strikePrices)
            {
                pain = 0;
                IEnumerable<Decimal> putStrikes = strikePrices.Where(x => x < expiryStrike);
                IEnumerable<Decimal> callStrikes = strikePrices.Where(x => x > expiryStrike);

                foreach (decimal strikePrice in putStrikes)
                {
                    pain += (puts[strikePrice].OI - puts[strikePrice].BeginingPeriodOI) * (expiryStrike - strikePrice);
                }
                foreach (decimal strikePrice in callStrikes)
                {
                    pain += (calls[strikePrice].OI - calls[strikePrice].BeginingPeriodOI) * (strikePrice - expiryStrike);
                }

                //if (pain < maxPain || (maxPain == 0 && pain != 0))
                //{
                //    maxPain = pain;
                //    maxPainStrike = expiryStrike;
                //}

                if (pain != 0)
                    maxPainStrikes.Add(pain, expiryStrike);
            }

            return maxPainStrikes;
        }
        private bool PopulateReferenceStrangleData(StrangleDataStructure strangleNode, Tick[] ticks, decimal includeStrike = 0)
        {
            decimal lowerband = strangleNode.BaseInstrumentPrice - strangleNode.StrikePriceIncrement * 2;
            decimal upperband = strangleNode.BaseInstrumentPrice + strangleNode.StrikePriceIncrement * 2;

            if (strangleNode.CallUniverse.Count == 0 
                || strangleNode.PutUniverse.Count == 0 
                || lowerband < strangleNode.CallUniverse.First().Value.Strike 
                || upperband > strangleNode.CallUniverse.Last().Value.Strike 
                || includeStrike !=0)
            {
                SortedList<Decimal, Instrument>[] nodeData;
                if (includeStrike == 0)
                {
                    nodeData = GetNewStrikes(strangleNode.BaseInstrumentToken, strangleNode.BaseInstrumentPrice, strangleNode.Expiry, strangleNode.StrikePriceIncrement);
                }
                else
                {
                    nodeData = GetNewStrikes(strangleNode.BaseInstrumentToken, includeStrike, strangleNode.Expiry, strangleNode.StrikePriceIncrement);
                }

                if (strangleNode.CallUniverse.Count > 0)
                {
                    foreach (KeyValuePair<decimal, Instrument> keyValuePair in nodeData[0])
                    {
                        if (!strangleNode.CallUniverse.ContainsKey(keyValuePair.Key))
                            strangleNode.CallUniverse.Add(keyValuePair.Key, keyValuePair.Value);
                    }
                }
                else
                {
                    strangleNode.CallUniverse = nodeData[0];

                }
                if (strangleNode.PutUniverse.Count > 0)
                {
                    foreach (KeyValuePair<decimal, Instrument> keyValuePair in nodeData[1])
                    {
                        if (!strangleNode.PutUniverse.ContainsKey(keyValuePair.Key))
                            strangleNode.PutUniverse.Add(keyValuePair.Key, keyValuePair.Value);
                    }
                }
                else
                {
                    strangleNode.PutUniverse = nodeData[1];
                }
            }

            for (int i = 0; i < strangleNode.CallUniverse.Count; i++)
            {
                Instrument instrument = strangleNode.CallUniverse.ElementAt(i).Value;
                decimal strike = strangleNode.CallUniverse.ElementAt(i).Key;

                Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == instrument.InstrumentToken);
                if (optionTick.LastPrice != 0)
                {
                    instrument.LastPrice = optionTick.LastPrice;
                    instrument.InstrumentType = "CE";
                    instrument.Bids = optionTick.Bids;
                    instrument.Offers = optionTick.Offers;
                    instrument.OI = optionTick.OI;
                    instrument.BeginingPeriodOI = instrument.BeginingPeriodOI == 0 ? instrument.OI : instrument.BeginingPeriodOI;
                    instrument.OIDayHigh = optionTick.OIDayHigh;
                    instrument.OIDayLow = optionTick.OIDayLow;
                    strangleNode.CallUniverse[strike] = instrument;
                }
            }

            for (int i = 0; i < strangleNode.PutUniverse.Count; i++)
            {
                Instrument instrument = strangleNode.PutUniverse.ElementAt(i).Value;
                decimal strike = strangleNode.PutUniverse.ElementAt(i).Key;

                Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == instrument.InstrumentToken);
                if (optionTick.LastPrice != 0)
                {
                    instrument.LastPrice = optionTick.LastPrice;
                    instrument.InstrumentType = "PE";
                    instrument.Bids = optionTick.Bids;
                    instrument.Offers = optionTick.Offers;
                    instrument.OI = optionTick.OI;
                    instrument.BeginingPeriodOI = instrument.BeginingPeriodOI == 0 ? instrument.OI : instrument.BeginingPeriodOI;
                    instrument.OIDayHigh = optionTick.OIDayHigh;
                    instrument.OIDayLow = optionTick.OIDayLow;
                    strangleNode.PutUniverse[strike] = instrument;
                }
            }

            ////for (int i = 0; i < strangleNode.TradedStrangles.Count; i++)
            ////{
            ////    List<Instrument> instruments = strangleNode.TradedStrangles.ElementAt(i).Options;
            //List<Instrument> instruments = strangleNode.TradedStrangle.Options;
            //for (int j = 0; j < instruments.Count; j++)
            //    {
            //        Instrument instrument = instruments.ElementAt(j);

            //        Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == instrument.InstrumentToken);
            //        if (optionTick.LastPrice != 0)
            //        {
            //            instrument.LastPrice = optionTick.LastPrice;
            //            //instrument.InstrumentType = "CE";
            //            instrument.Bids = optionTick.Bids;
            //            instrument.Offers = optionTick.Offers;
            //            instrument.OI = optionTick.OI;
            //            instrument.OIDayHigh = optionTick.OIDayHigh;
            //            instrument.OIDayLow = optionTick.OIDayLow;
            //            //strangleNode.TradedCalls.ElementAt(i).Option = instrument;
            //            instruments[j] = instrument;
            //        }
            //    }
            ////strangleNode.TradedStrangles.ElementAt(i).Options = instruments;
            //strangleNode.TradedStrangle.Options = instruments;
            //}
            //for (int i = 0; i < strangleNode.TradedStrangles.Count; i++)
            //{
            //instruments = strangleNode.TradedStrangles.ElementAt(i).Put;
            //for (int j = 0; j < instruments.Count; j++)
            //{
            //    Instrument instrument = instruments.ElementAt(j);

            //    Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == instrument.InstrumentToken);
            //    if (optionTick.LastPrice != 0)
            //    {
            //        instrument.LastPrice = optionTick.LastPrice;
            //        instrument.InstrumentType = "CE";
            //        instrument.Bids = optionTick.Bids;
            //        instrument.Offers = optionTick.Offers;
            //        instrument.OI = optionTick.OI;
            //        instrument.OIDayHigh = optionTick.OIDayHigh;
            //        instrument.OIDayLow = optionTick.OIDayLow;
            //        //strangleNode.TradedCalls.ElementAt(i).Option = instrument;
            //        instruments[j] = instrument;
            //    }
            //}
            //strangleNode.TradedStrangles.ElementAt(i).Put = instruments;
            //}


            //for (int i = 0; i < strangleNode.TradedCalls.Count; i++)
            //{
            //    Instrument instrument = strangleNode.TradedCalls.ElementAt(i).Option;

            //    Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == instrument.InstrumentToken);
            //    if (optionTick.LastPrice != 0)
            //    {
            //        instrument.LastPrice = optionTick.LastPrice;
            //        instrument.InstrumentType = "CE";
            //        instrument.Bids = optionTick.Bids;
            //        instrument.Offers = optionTick.Offers;
            //        instrument.OI = optionTick.OI;
            //        instrument.OIDayHigh = optionTick.OIDayHigh;
            //        instrument.OIDayLow = optionTick.OIDayLow;
            //        strangleNode.TradedCalls.ElementAt(i).Option = instrument;
            //    }
            //}
            //for (int i = 0; i < strangleNode.TradedPuts.Count; i++)
            //{
            //    Instrument instrument = strangleNode.TradedPuts.ElementAt(i).Option;

            //    Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == instrument.InstrumentToken);
            //    if (optionTick.LastPrice != 0)
            //    {
            //        instrument.LastPrice = optionTick.LastPrice;
            //        instrument.InstrumentType = "PE";
            //        instrument.Bids = optionTick.Bids;
            //        instrument.Offers = optionTick.Offers;
            //        instrument.OI = optionTick.OI;
            //        instrument.OIDayHigh = optionTick.OIDayHigh;
            //        instrument.OIDayLow = optionTick.OIDayLow;
            //        strangleNode.TradedPuts.ElementAt(i).Option = instrument;
            //    }
            //}

            //return !Convert.ToBoolean(strangleNode.Calls.Values.Count(x => x.OI == 0) + strangleNode.Puts.Values.Count(x => x.OI == 0));//

            return !Convert.ToBoolean(strangleNode.CallUniverse.Values.Count(x => x.LastPrice == 0) + strangleNode.PutUniverse.Values.Count(x => x.LastPrice == 0));
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

        public virtual async Task<bool> OnNext(Tick[] ticks)
        {
            lock (ActiveStrangles)
            {
                for (int i = 0; i < ActiveStrangles.Count; i++)
                {
                    //ReviewStrangle(ActiveStrangles.ElementAt(i).Value, ticks);
                    ManageStrangle(ActiveStrangles.ElementAt(i).Value, ticks);

                    //   foreach (KeyValuePair<int, StrangleDataList> keyValuePair in ActiveStrangles)
                    // {
                    //  ReviewStrangle(keyValuePair.Value, ticks);
                    // }
                }
            }
            return true;
        }
        private ShortTrade PlaceOrder(int strangleID, string instrument_tradingsymbol, decimal instrument_currentPrice, uint instrument_Token, 
            bool buyOrder, int quantity, DateTime? tickTime = null, uint token = 0, decimal triggerID = 0)
        {
            string tradingSymbol = instrument_tradingsymbol;
            decimal currentPrice = instrument_currentPrice;
            //Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
            //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, quantity, Product: Constants.PRODUCT_MIS,
            //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            ///TEMP, REMOVE Later
            if (currentPrice == 0)
            {
                DataLogic dl = new DataLogic();
                currentPrice = dl.RetrieveLastPrice(instrument_Token, tickTime, buyOrder);
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
            trade.TriggerID = Convert.ToInt32(triggerID);
            trade.TradingStatus = buyOrder ? TradeStatus.Closed : TradeStatus.Open;
            UpdateTradeDetails(strangleID, instrument_Token, quantity, trade, Convert.ToInt32(triggerID));

            return trade;
        }
        private ShortTrade PlaceOrder(int strangleID, Instrument instrument, bool buyOrder, int quantity, DateTime? tickTime = null, uint token = 0, decimal triggerID = 0)
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
            trade.TriggerID = Convert.ToInt32(triggerID);
            trade.TradingStatus = buyOrder ? TradeStatus.Closed : TradeStatus.Open;
            UpdateTradeDetails(strangleID, instrument.InstrumentToken, quantity, trade, Convert.ToInt32(triggerID));

            return trade;
        }
        private void UpdateTradeDetails(int strategyID, uint instrumentToken, int tradedLot, ShortTrade trade, int triggerID)
        {
            DataLogic dl = new DataLogic();
            dl.UpdateTrade(strategyID, instrumentToken, trade, AlgoIndex.ExpiryTrade, tradedLot, triggerID);
        }
        private void LoadActiveData()
        {
            AlgoIndex algoIndex = AlgoIndex.ExpiryTrade;
            DataLogic dl = new DataLogic();
            DataSet activeStrangles = dl.RetrieveActiveStrangleData(algoIndex);
            DataRelation strategy_Token_Relation = activeStrangles.Relations.Add("Strangle_Token", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"] },
                new DataColumn[] { activeStrangles.Tables[1].Columns["StrategyId"] });

            DataRelation strategy_Trades_Relation = activeStrangles.Relations.Add("Strangle_Trades", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"] },
                new DataColumn[] { activeStrangles.Tables[2].Columns["StrategyId"] });

            Instrument call, put;

            foreach (DataRow strangleRow in activeStrangles.Tables[0].Rows)
            {
                StrangleDataStructure strangleNode = new StrangleDataStructure();
                strangleNode.BaseInstrumentToken = Convert.ToUInt32(strangleRow["BToken"]);
                strangleNode.Expiry = Convert.ToDateTime(strangleRow["Expiry"]);
                strangleNode.MaxLossThreshold = Convert.ToDecimal(strangleRow["MaxLossPoints"]);
                strangleNode.ProfitTarget = Convert.ToDecimal(strangleRow["MaxProfitPoints"]);
                strangleNode.MaxQty = Convert.ToInt32(strangleRow["MaxQty"]);
                strangleNode.InitialQty = Convert.ToInt32(strangleRow["InitialQty"]);
                strangleNode.StepQty = Convert.ToInt32(strangleRow["StepQty"]);
                strangleNode.StrikePriceIncrement = Convert.ToInt32(strangleRow["StrikePriceIncrement"]);
                strangleNode.ID = Convert.ToInt32(strangleRow["ID"]);

                DataRow[] strangleTokenRows = strangleRow.GetChildRows(strategy_Token_Relation);
                if (strangleTokenRows.Count() == 0)
                {
                    ActiveStrangles.Add(strangleNode.ID, strangleNode);
                    continue;
                }

                //DataRow strangleTokenRow = strangleTokenRows[0];
                //uint baseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]);

                //call = new Instrument()
                //{
                //    BaseInstrumentToken = baseInstrumentToken,
                //    InstrumentToken = Convert.ToUInt32(strangleTokenRow["CallToken"]),
                //    InstrumentType = "CE",
                //    Strike = (Decimal)strangleTokenRow["CallStrike"],
                //    TradingSymbol = (string)strangleTokenRow["CallSymbol"]
                //};
                //put = new Instrument()
                //{
                //    BaseInstrumentToken = baseInstrumentToken,
                //    InstrumentToken = Convert.ToUInt32(strangleTokenRow["PutToken"]),
                //    InstrumentType = "PE",
                //    Strike = (Decimal)strangleTokenRow["PutStrike"],
                //    TradingSymbol = (string)strangleTokenRow["PutSymbol"]
                //};

                //if (strangleTokenRow["Expiry"] != DBNull.Value)
                //{
                //    call.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);
                //    put.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);
                //}
                //strangleNode.CurrentPut.Option = put;
                //strangleNode.CurrentCall.Option = call;

                //ShortTrade trade;
                //decimal netPnL = 0;
                //foreach (DataRow strangleTradeRow in strangleRow.GetChildRows(strategy_Trades_Relation))
                //{
                //    trade = new ShortTrade();
                //    trade.AveragePrice = (Decimal)strangleTradeRow["Price"];
                //    trade.ExchangeTimestamp = (DateTime?)strangleTradeRow["TimeStamp"];
                //    trade.OrderId = (string)strangleTradeRow["OrderId"];
                //    trade.TransactionType = (string)strangleTradeRow["TransactionType"];
                //    trade.Quantity = (int)strangleTradeRow["Quantity"];
                //    trade.TriggerID = (int)strangleTradeRow["TriggerID"];
                //    if (Convert.ToUInt32(strangleTradeRow["InstrumentToken"]) == call.InstrumentToken)
                //    {
                //        strangleNode.CurrentCall.SellTrade = trade;
                //    }
                //    else if (Convert.ToUInt32(strangleTradeRow["InstrumentToken"]) == put.InstrumentToken)
                //    {
                //        strangleNode.CurrentPut.SellTrade = trade;
                //    }
                //    netPnL += trade.AveragePrice * Math.Abs(trade.Quantity);
                //}
                //strangleNode.BaseInstrumentToken = call.BaseInstrumentToken;
                //strangleNode.TradingQuantity = strangleNode.CurrentPut.SellTrade.Quantity; //PutTrades.Sum(x => x.Quantity);
                //strangleNode.TradingQuantity = strangleNode.CurrentCall.SellTrade.Quantity;
                //strangleNode.CurrentCall.TradingStatus = PositionStatus.Open;
                //strangleNode.CurrentPut.TradingStatus = PositionStatus.Open;
                ////strangleNode.MaxQty = (int)strangleRow["MaxQty"];
                ////strangleNode.StepQty = Convert.ToInt32(strangleRow["MaxProfitPoints"]);
                //strangleNode.NetPnL = netPnL;
                //strangleNode.ID = (int)strangleRow["Id"];
                ActiveStrangles.Add(strangleNode.ID, strangleNode);
            }
        }

        public void StoreIndexForExpiryTrade(uint bToken, DateTime expiry, int tradingQty, int strikePriceIncrement, decimal maxLossThreshold, decimal profitTarget, DateTime timeOfOrder = default(DateTime))
        {
            timeOfOrder = DateTime.Now;

            //Update Database
            DataLogic dl = new DataLogic();
            dl.StoreIndexForMainPainStrangle(bToken: bToken, expiry, strikePriceIncrement: strikePriceIncrement, algoIndex: AlgoIndex.ExpiryTrade, tradingQty: tradingQty, maxLossThreshold, profitTarget, timeOfOrder);
        }

        public virtual void OnError(Exception ex)
        {
            ///TODO: Log the error. Also handle the error.
        }

        //public virtual void Subscribe(Publisher publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}
        //public virtual void Subscribe(Ticker publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}

        //public virtual void Subscribe(TickDataStreamer publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}


        public virtual void Unsubscribe()
        {
            UnsubscriptionToken.Dispose();
        }

        public virtual void OnCompleted()
        {
        }
    }
}
