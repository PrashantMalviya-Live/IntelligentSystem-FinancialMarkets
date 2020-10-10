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
using Pub_Sub;
using MarketDataTest;
using System.Data;

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
    public class ExpiryTrade : IObserver<Tick[]>
    {
        public IDisposable UnsubscriptionToken;

        Dictionary<int, StrangleDataStructure> ActiveStrangles = new Dictionary<int, StrangleDataStructure>();

        public ExpiryTrade()
        {
            LoadActiveData();
        }

        private void ReviewStrangle(StrangleDataStructure strangleNode, Tick[] ticks)
        {
            //Instrument callOption = strangleNode.Call;
            //Instrument putOption = strangleNode.Put;

            //Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == callOption.InstrumentToken);
            //if (optionTick.LastPrice != 0)
            //{
            //    callOption.LastPrice = optionTick.LastPrice;
            //    callOption.Bids = optionTick.Bids;
            //    callOption.Offers = optionTick.Offers;
            //    strangleNode.Call = callOption;
            //}
            //optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == putOption.InstrumentToken);
            //if (optionTick.LastPrice != 0)
            //{
            //    putOption.LastPrice = optionTick.LastPrice;
            //    putOption.Bids = optionTick.Bids;
            //    putOption.Offers = optionTick.Offers;
            //    strangleNode.Put = putOption;
            //}

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
            decimal maxPainStrike = UpdateMaxPainStrike(strangleNode, ticks);
            if (maxPainStrike == 0)
            {
                return;
            }

            //Take initial Trades
            if (strangleNode.TradedStrangles.Count == 0 || strangleNode.TradedStrangles.Any(x => x.SellTrades.Values.Any(t=>t.TradingStatus == TradeStatus.Open)))
            {
                TakeInitialPositions(strangleNode, ticks);
            }
            else
            {
                ///TODO: There should be one tradedstrangle with as many combination of trades
                //List all open traded strangles to watch out for
                List<TradedStrangle> openStrangles = strangleNode.TradedStrangles.Where(x => x.TradingStatus.Any(t=>t.Value != PositionStatus.Closed)).ToList();

                //strangle check is based on total unclosed strange positions
                //strangle check is based on each unclosed strangle trades. The trades should be tagged together based on trade id 
                //and individual call or put position taken to balance the strangle should be tagged to the same strangle trade id. So that over all strangle position always gets closed in a good way

                for (int i = 0; i < openStrangles.Count; i++)
                {
                    TradedStrangle tradedStrangle = openStrangles.ElementAt(i);
                    TradeStrangle(strangleNode, tradedStrangle, ticks[0].Timestamp.Value);
                    //int totalTradedQty = openTradedStrangles.Sum(x => x.Quantity);
                }
                
                //Here the order will be based on their execution
                List <TradedInstrument> openCalls = strangleNode.TradedCalls.Where(x => x.TradingStatus != PositionStatus.Closed).ToList(); // OrderBy(x=>x.SellTrades[0].ExchangeTimestamp).ToList();
                List<TradedInstrument> openPuts = strangleNode.TradedPuts.Where(x => x.TradingStatus != PositionStatus.Closed).ToList(); //OrderBy(x => x.SellTrades[0].ExchangeTimestamp).ToList();

                if(CheckStrangleValue(strangleNode, openCalls, openPuts, ticks[0].Timestamp.Value))
                {
                    CheckAndTrade(strangleNode, openCalls, ticks[0].Timestamp.Value, ticks);
                    CheckAndTrade(strangleNode, openPuts, ticks[0].Timestamp.Value, ticks);
                }
            }
        }

        private bool TradeStrangle(StrangleDataStructure strangleNode, TradedStrangle tradedStrangle, DateTime timeOfOrder, Tick[] ticks)
        {
            IEnumerable<ShortTrade> openTradedStrangles = tradedStrangle.SellTrades.Where(x => x.TradingStatus == TradeStatus.Open);
            var openStrangleTrades = openTradedStrangles.GroupBy(x => x.TriggerID).ToList();
            
            foreach (var strangleTrade in openStrangleTrades)
            {
                int triggerId = strangleTrade.Key;
                List<ShortTrade> optionTrades = strangleTrade.ToList();
                decimal tradedCallValue = optionTrades.Where(x=>x.InstrumentType == "CE"). Sum(x => x.Quantity * x.AveragePrice);
                decimal tradedPutValue = optionTrades.Where(x => x.InstrumentType == "PE").Sum(x => x.Quantity * x.AveragePrice);
                decimal tradedValue = tradedCallValue + tradedPutValue;
                
                decimal currentCallValue = optionTrades.Where(x => x.InstrumentType == "CE").Sum(x => x.Quantity * tradedStrangle.Options.First(c => c.InstrumentToken == x.InstrumentToken).LastPrice);
                decimal currentPutValue = optionTrades.Where(x => x.InstrumentType == "PE").Sum(x => x.Quantity * tradedStrangle.Options.First(c => c.InstrumentToken == x.InstrumentToken).LastPrice);
                decimal currentValue = currentCallValue + currentPutValue;

                // Currently traded Quantities
                int quantityTraded = strangleNode.NetCallQtyInTrade;
                int triggerid = tradedStrangle.SellTrades.Max(x=>x.TriggerID);
                //Increase lot if value increased by 20%
                if (currentValue > tradedValue * 1.2m)
                {
                    if (quantityTraded < strangleNode.MaxQty)
                    {
                        foreach (ShortTrade trade in optionTrades)
                        {
                            Instrument option = tradedStrangle.Options.First(c => c.InstrumentToken == trade.InstrumentToken);
                            ShortTrade addlTrade = PlaceOrder(strangleNode.ID, option, false, strangleNode.StepQty, timeOfOrder, triggerID: triggerid + 1);

                            tradedStrangle.SellTrades.Add(addlTrade);
                            tradedStrangle.TradingStatus = PositionStatus.Open;
                            tradedStrangle.UnbookedPnl += addlTrade.AveragePrice * addlTrade.Quantity;
                            strangleNode.UnBookedPnL += addlTrade.AveragePrice * addlTrade.Quantity;
                        }
                    }

                    //check loss making option for next option movement. If not possible then add one node of the loosing side
                    decimal nextCallOptionValue = 0;
                    //move to next option when value is equal
                    if (currentCallValue >= * tradedCallValue)
                    {
                        CheckAndTrade(strangleNode, strangleTrade, timeOfOrder, ticks)
                    }
                }







                if (currentValue < tradedValue * 0.8m)
                {
                    foreach (ShortTrade trade in optionTrades)
                    {
                        Instrument option = tradedStrangle.Options.First(c => c.InstrumentToken == trade.InstrumentToken);
                        ShortTrade addlTrade = PlaceOrder(strangleNode.ID, option, true, strangleNode.StepQty, timeOfOrder, triggerID: triggerid);

                        tradedStrangle.SellTrades.Add(addlTrade);
                        tradedStrangle.TradingStatus = PositionStatus.PercentClosed50;
                        tradedStrangle.BookedPnL += (tradedValue/2 - addlTrade.AveragePrice * addlTrade.Quantity);
                        tradedStrangle.UnbookedPnl -= addlTrade.AveragePrice * addlTrade.Quantity;
                        strangleNode.UnBookedPnL -= addlTrade.AveragePrice * addlTrade.Quantity;
                    }
                }
                if (currentValue < tradedValue * 0.5m)
                {
                    foreach (ShortTrade trade in optionTrades)
                    {
                        Instrument option = tradedStrangle.Options.First(c => c.InstrumentToken == trade.InstrumentToken);
                        ShortTrade addlTrade = PlaceOrder(strangleNode.ID, option, true, strangleNode.StepQty, timeOfOrder, triggerID: triggerid);

                        tradedStrangle.SellTrades.Add(addlTrade);
                        tradedStrangle.TradingStatus = PositionStatus.Closed;
                        tradedStrangle.BookedPnL += (tradedValue - addlTrade.AveragePrice * addlTrade.Quantity);
                        tradedStrangle.UnbookedPnl -= addlTrade.AveragePrice * addlTrade.Quantity;
                        strangleNode.UnBookedPnL -= addlTrade.AveragePrice * addlTrade.Quantity;
                    }
                }



            }



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
                if(totalCallQty > strangleNode.InitialQty && totalPutQty > strangleNode.InitialQty)
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
        
        private void ManageStrangle(StrangleDataList strangleNode, TradedStrangle tradedStrangle, IGrouping<int, ShortTrade> strangleTrade, DateTime timeOfOrder, Tick[] ticks)
        {

            //




            ///Columns: Token, InitialPrice, CurrentPrice, Quantity, TradeID,TradingStatus(bool), PnL
            Decimal[,] tradedCallMatix = new Decimal[3, 3];

           
            
        }

        /// <summary>
        /// move to next node when the premium equals to next node
        /// </summary>
        /// <param name="strangleNode"></param>
        /// <param name="tradedStrangle"></param>
        /// <param name="timeOfOrder"></param>
        /// <param name="ticks"></param>
        private void CheckAndTrade(StrangleDataList strangleNode, TradedStrangle tradedStrangle, IGrouping<int, ShortTrade> strangleTrade, DateTime timeOfOrder, Tick[] ticks)
        {

            int triggerId = strangleTrade.Key;
            List<ShortTrade> optionTrades = strangleTrade.ToList();
            List<Instrument> options = tradedStrangle.Options;

            //List<Instrument> tradedCalls = tradedStrangle.Options.Where(c=>c.InstrumentToken = optionTrades.Where(x => x.InstrumentType == "CE").Select(it=>it.InstrumentToken)).ToList();

            //var innerJoin = tradedOptions.Join(optionTrades, ot => ot.InstrumentToken, c => c.InstrumentToken, (ot, c) => ot);

            var tradedOptions = from option in options
                                join trade in optionTrades on option.InstrumentToken equals trade.InstrumentToken
                                 select option;

            List<Instrument> tradedCalls = tradedOptions.Where(x => x.InstrumentType == "CE").ToList();
            List<Instrument> tradedPuts = tradedOptions.Where(x => x.InstrumentType == "PE").ToList();

            decimal tradedCallValue = optionTrades.Where(x => x.InstrumentType == "CE").Sum(x => x.Quantity * x.AveragePrice);
            decimal tradedPutValue = optionTrades.Where(x => x.InstrumentType == "PE").Sum(x => x.Quantity * x.AveragePrice);
            decimal tradedValue = tradedCallValue + tradedPutValue;

            decimal currentCallValue = optionTrades.Where(x => x.InstrumentType == "CE").Sum(x => x.Quantity * tradedStrangle.Options.First(c => c.InstrumentToken == x.InstrumentToken).LastPrice);
            decimal currentPutValue = optionTrades.Where(x => x.InstrumentType == "PE").Sum(x => x.Quantity * tradedStrangle.Options.First(c => c.InstrumentToken == x.InstrumentToken).LastPrice);
            decimal currentValue = currentCallValue + currentPutValue;


            if(currentCallValue > currentPutValue)
            {
                var sortedPuts = from tp in tradedPuts
                                 orderby tp.Strike
                                 select new { tp.Strike, tp.LastPrice };



                //check if currentcallvalue is equal to next put node current value

                //Move 50% to next node
                decimal nextCallStrike = bookProfit ? openOption.Option.Strike - strangleNode.StrikePriceIncrement : openOption.Option.Strike + strangleNode.StrikePriceIncrement;
                nextOption = strangleNode.TradedCalls.FirstOrDefault(x => x.Option.Strike == nextCallStrike);

                if (nextOption == null)
                {
                    //Instrument nextOptionFromUniverse = strangleNode.CallUniverse[nextCallStrike];

                    if (strangleNode.CallUniverse.TryGetValue(nextCallStrike, out nextOptionFromUniverse))
                    {
                        nextOption = new TradedInstrument() { Option = nextOptionFromUniverse };
                    }
                    else
                    {
                        PopulateReferenceStrangleData(strangleNode, ticks, nextCallStrike);
                        return;
                    }
                }
                strangleNode.TradedCalls.Add(nextOption);



            }

            List<Instrument> tradedOptions = strangleTrade.Options;
            for (int i = 0; i < tradedOptions.Count; i++)
            {
                Instrument openOption = tradedOptions[i];

                ShortTrade lastSellTrade = openOption.SellTrades.Last();

                int lastSellQty = lastSellTrade.Quantity > strangleNode.StepQty ? lastSellTrade.Quantity / 2 : strangleNode.StepQty;
                decimal lastSellPrice = lastSellTrade.AveragePrice;
                decimal currentPrice = openOption.Option.LastPrice;

                //TODO: Do we need to move one node based on opposite type of node, so move call based on put or vice-versa
                if (openOption.TradingStatus == PositionStatus.Open)
                {
                    if (currentPrice > lastSellPrice * 1.2m)
                    {
                        MoveNodes(openOption, PositionStatus.PercentClosed50, lastSellQty, lastSellPrice, timeOfOrder, strangleNode, false, ticks);
                    }
                    if (currentPrice < lastSellPrice * 0.8m)
                    {
                        MoveNodes(openOption, PositionStatus.PercentClosed50, lastSellQty, lastSellPrice, timeOfOrder, strangleNode, true, ticks);
                    }
                }
                else if (openOption.TradingStatus == PositionStatus.PercentClosed50)
                {
                    if (currentPrice > lastSellPrice * 1.4m)
                    {
                        MoveNodes(openOption, PositionStatus.Closed, lastSellQty, lastSellPrice, timeOfOrder, strangleNode, false, ticks);
                    }
                    if (currentPrice < lastSellPrice * 0.6m)
                    {
                        MoveNodes(openOption, PositionStatus.Closed, lastSellQty, lastSellPrice, timeOfOrder, strangleNode, true, ticks);
                    }
                }

                #region Commented Code
                ////if option lost more than 20% from last trade, close last trade
                //if (openOption.TradingStatus == PositionStatus.Open &&
                //    (openOption.Option.LastPrice > lastSellTrade.AveragePrice * 1.2m || openOption.Option.LastPrice < lastSellTrade.AveragePrice * 0.8m))
                //{
                //    openOption.TradingStatus = PositionStatus.PercentClosed50; //TODO: Check qty is half of initial qty to assign percentclosed50 weightage. Else assign closed.
                //    ShortTrade closeTrade = PlaceOrder(strangleNode.ID, openOption.Option, true, lastSellQty, timeOfOrder);
                //    openOption.BuyTrades.Add(closeTrade);
                //    openOption.BookedPnL += lastSellTrade.AveragePrice * lastSellQty - closeTrade.AveragePrice * closeTrade.Quantity;
                //    strangleNode.BookedPnL += openOption.BookedPnL;

                //    TradedInstrument nextOption= null;
                //    if (openOption.Option.InstrumentType.Trim() == "CE")
                //    {
                //        //Move 50% to next node
                //        decimal nextCallStrike = openOption.Option.Strike + strangleNode.StrikePriceIncrement;
                //        nextOption = strangleNode.TradedCalls.FirstOrDefault(x => x.Option.Strike == nextCallStrike);

                //        if (nextOption == null)
                //        {
                //            Instrument nextOptionFromUniverse = strangleNode.CallUniverse[nextCallStrike];
                //            nextOption = new TradedInstrument() { Option = nextOptionFromUniverse };
                //        }
                //        strangleNode.TradedCalls.Add(nextOption);
                //    }
                //    else if (openOption.Option.InstrumentType.Trim() == "PE")
                //    {
                //        //Move 50% to next node
                //        decimal nextPutStrike = openOption.Option.Strike - strangleNode.StrikePriceIncrement;
                //        nextOption = strangleNode.TradedPuts.FirstOrDefault(x => x.Option.Strike == nextPutStrike);
                //        if (nextOption == null)
                //        {
                //            Instrument nextOptionFromUniverse = strangleNode.PutUniverse[nextPutStrike];
                //            nextOption = new TradedInstrument() { Option = nextOptionFromUniverse };
                //        }
                //            strangleNode.TradedPuts.Add(nextOption);
                //    }

                //    nextOption.TradingStatus = PositionStatus.Open;
                //    ShortTrade openTrade = PlaceOrder(strangleNode.ID, nextOption.Option, false, lastSellQty, timeOfOrder);
                //    nextOption.SellTrades.Add(openTrade);
                //    nextOption.UnbookedPnl += openTrade.AveragePrice * lastSellQty;
                //    strangleNode.UnBookedPnL += nextOption.UnbookedPnl;

                //}
                //else if (openOption.TradingStatus == PositionStatus.PercentClosed50 &&
                //    (openOption.Option.LastPrice > lastSellTrade.AveragePrice * 1.4m || openOption.Option.LastPrice < lastSellTrade.AveragePrice * 0.6m))
                //{
                //    openOption.TradingStatus = PositionStatus.Closed;
                //    ShortTrade closeTrade = PlaceOrder(strangleNode.ID, openOption.Option, true, lastSellQty, timeOfOrder);
                //    openOption.BuyTrades.Add(closeTrade);
                //    openOption.BookedPnL += lastSellTrade.AveragePrice * lastSellQty - closeTrade.AveragePrice * closeTrade.Quantity;
                //    strangleNode.BookedPnL += openOption.BookedPnL;

                //    TradedInstrument nextOption = null;
                //    if (openOption.Option.InstrumentType.Trim() == "CE")
                //    {
                //        //Move 50% to next node
                //        decimal nextCallStrike = openOption.Option.Strike + strangleNode.StrikePriceIncrement;
                //        nextOption = strangleNode.TradedCalls.FirstOrDefault(x => x.Option.Strike == nextCallStrike);

                //        if (nextOption == null)
                //        {
                //            Instrument nextOptionFromUniverse = strangleNode.CallUniverse[nextCallStrike];
                //            nextOption = new TradedInstrument() { Option = nextOptionFromUniverse };
                //        }
                //        strangleNode.TradedCalls.Add(nextOption);
                //    }
                //    else if (openOption.Option.InstrumentType.Trim() == "PE")
                //    {
                //        //Move 50% to next node
                //        decimal nextPutStrike = openOption.Option.Strike - strangleNode.StrikePriceIncrement;
                //        nextOption = strangleNode.TradedPuts.FirstOrDefault(x => x.Option.Strike == nextPutStrike);
                //        if (nextOption == null)
                //        {
                //            Instrument nextOptionFromUniverse = strangleNode.PutUniverse[nextPutStrike];
                //            nextOption = new TradedInstrument() { Option = nextOptionFromUniverse };
                //        }
                //        strangleNode.TradedPuts.Add(nextOption);
                //    }

                //    nextOption.TradingStatus = PositionStatus.Open;
                //    ShortTrade openTrade = PlaceOrder(strangleNode.ID, nextOption.Option, false, lastSellQty, timeOfOrder);
                //    nextOption.SellTrades.Add(openTrade);
                //    nextOption.UnbookedPnl += openTrade.AveragePrice * lastSellQty;
                //    strangleNode.UnBookedPnL += nextOption.UnbookedPnl;
                //}
                #endregion
            }
        }
        private void BookProfitFromLastTrade(TradedInstrument openOption, PositionStatus positionStatus, int tradeQty, decimal lastTradePrice, DateTime? tradeTime, StrangleDataList strangleNode, bool bookProfit, Tick[] ticks)
        {
            openOption.TradingStatus = (PositionStatus)((int)positionStatus - 1); //PositionStatus.Open; //TODO: Check qty is half of initial qty to assign percentclosed50 weightage. Else assign closed.
            ShortTrade closeTrade = PlaceOrder(strangleNode.ID, openOption.Option, true, tradeQty, tradeTime);
            openOption.BuyTrades.Add(closeTrade);
            openOption.BookedPnL += lastTradePrice * tradeQty - closeTrade.AveragePrice * closeTrade.Quantity;
            strangleNode.BookedPnL += openOption.BookedPnL;
            strangleNode.BookedPnL += openOption.BookedPnL;
        }
        private void MoveNodes(TradedInstrument openOption, PositionStatus positionStatus, int tradeQty, decimal lastTradePrice, DateTime? tradeTime, StrangleDataList strangleNode, bool bookProfit, Tick[] ticks)
        {
            TradedInstrument nextOption = null;
            Instrument nextOptionFromUniverse;
            if (openOption.Option.InstrumentType.Trim() == "CE")
            {
                //Move 50% to next node
                decimal nextCallStrike =  bookProfit? openOption.Option.Strike - strangleNode.StrikePriceIncrement : openOption.Option.Strike + strangleNode.StrikePriceIncrement;
                nextOption = strangleNode.TradedCalls.FirstOrDefault(x => x.Option.Strike == nextCallStrike);

                if (nextOption == null)
                {
                    //Instrument nextOptionFromUniverse = strangleNode.CallUniverse[nextCallStrike];
                    
                    if (strangleNode.CallUniverse.TryGetValue(nextCallStrike, out nextOptionFromUniverse))
                    {
                        nextOption = new TradedInstrument() { Option = nextOptionFromUniverse };
                    }
                    else
                    {
                        PopulateReferenceStrangleData(strangleNode, ticks, nextCallStrike);
                        return;
                    }
                }
                strangleNode.TradedCalls.Add(nextOption);
            }
            else if (openOption.Option.InstrumentType.Trim() == "PE")
            {
                //Move 50% to next node
                decimal nextPutStrike = bookProfit ? openOption.Option.Strike + strangleNode.StrikePriceIncrement : openOption.Option.Strike - strangleNode.StrikePriceIncrement;
                nextOption = strangleNode.TradedPuts.FirstOrDefault(x => x.Option.Strike == nextPutStrike);
                if (nextOption == null)
                {
                    if (strangleNode.PutUniverse.TryGetValue(nextPutStrike, out nextOptionFromUniverse))
                    {
                        nextOption = new TradedInstrument() { Option = nextOptionFromUniverse };
                    }
                    else
                    {
                        PopulateReferenceStrangleData(strangleNode, ticks, nextPutStrike);
                    }
                    //nextOptionFromUniverse = strangleNode.PutUniverse[nextPutStrike];
                    //nextOption = new TradedInstrument() { Option = nextOptionFromUniverse };
                }
                strangleNode.TradedPuts.Add(nextOption);
            }

            openOption.TradingStatus = PositionStatus.PercentClosed50; //TODO: Check qty is half of initial qty to assign percentclosed50 weightage. Else assign closed.
            ShortTrade closeTrade = PlaceOrder(strangleNode.ID, openOption.Option, true, tradeQty, tradeTime);
            openOption.BuyTrades.Add(closeTrade);
            openOption.BookedPnL += lastTradePrice * tradeQty - closeTrade.AveragePrice * closeTrade.Quantity;
            strangleNode.BookedPnL += openOption.BookedPnL;
            strangleNode.BookedPnL += openOption.BookedPnL;

            nextOption.TradingStatus = PositionStatus.Open;
            ShortTrade openTrade = PlaceOrder(strangleNode.ID, nextOption.Option, false, tradeQty, tradeTime);
            nextOption.SellTrades.Add(openTrade);
            nextOption.UnbookedPnl += openTrade.AveragePrice * tradeQty;
            strangleNode.UnBookedPnL += nextOption.UnbookedPnl;
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
            tradedStrangle.Call.Add(callOption);
            tradedStrangle.Put.Add(putOption);

            int tradeId = tradedStrangle.SellTrades.Count;
            
            ShortTrade sellTrade = PlaceOrder(strangleNode.ID, callOption, buyOrder: false, strangleNode.InitialQty, tickTime: ticks[0].Timestamp);
            tradedStrangle.SellTrades.Add(tradeId, sellTrade);


            //TradedInstrument cInstrument = new TradedInstrument() { Option = callOption };
            //cInstrument.SellTrades = new List<ShortTrade>();
            //cInstrument.SellTrades.Add(sellTrade);
            //cInstrument.TradingStatus = PositionStatus.Open;
            strangleNode.NetCallQtyInTrade += sellTrade.Quantity;
            strangleNode.UnBookedPnL += sellTrade.Quantity * sellTrade.AveragePrice;

            sellTrade = PlaceOrder(strangleNode.ID, putOption, buyOrder: false, strangleNode.InitialQty, tickTime: ticks[0].Timestamp);
            tradedStrangle.SellTrades.Add(tradeId, sellTrade);
            //TradedInstrument pInstrument = new TradedInstrument() { Option = putOption };
            //pInstrument.SellTrades = new List<ShortTrade>();
            //pInstrument.SellTrades.Add(sellTrade);
            //pInstrument.TradingStatus = PositionStatus.Open;
            strangleNode.NetPutQtyInTrade += sellTrade.Quantity;
            strangleNode.UnBookedPnL += sellTrade.Quantity * sellTrade.AveragePrice;

            tradedStrangle.UnbookedPnl = strangleNode.UnBookedPnL;
            tradedStrangle.TradingStatus.Add(1, PositionStatus.Open);

            //strangleNode.TradedCalls = new List<TradedInstrument>();
            //strangleNode.TradedCalls.Add(cInstrument);
            //strangleNode.TradedPuts = new List<TradedInstrument>();
            //strangleNode.TradedPuts.Add(pInstrument);
            //ActiveStrangles.Add(strangleNode.ID, strangleNode);

        }
        private decimal UpdateMaxPainStrike(StrangleDataStructure strangleNode, Tick[] ticks)
        {
            //Check and Fill Reference Strangle Data
            //if (strikePrice == 0 || strikePrice != strangleNode.MaxPainStrike)
            //{
            bool allpopulated = PopulateReferenceStrangleData(strangleNode, ticks);
            //}
            if (!allpopulated) return 0;

            SortedList<decimal, Instrument> calls = strangleNode.CallUniverse;
            SortedList<decimal, Instrument> puts = strangleNode.PutUniverse;

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
                    instrument.OIDayHigh = optionTick.OIDayHigh;
                    instrument.OIDayLow = optionTick.OIDayLow;
                    strangleNode.PutUniverse[strike] = instrument;
                }
            }

            for (int i = 0; i < strangleNode.TradedStrangles.Count; i++)
            {
                List<Instrument> instruments = strangleNode.TradedStrangles.ElementAt(i).Call;
                for (int j = 0; j < instruments.Count; j++)
                {
                    Instrument instrument = instruments.ElementAt(j);

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
                        //strangleNode.TradedCalls.ElementAt(i).Option = instrument;
                        instruments[j] = instrument;
                    }
                }
                strangleNode.TradedStrangles.ElementAt(i).Call = instruments;
                //}
                //for (int i = 0; i < strangleNode.TradedStrangles.Count; i++)
                //{
                instruments = strangleNode.TradedStrangles.ElementAt(i).Put;
                for (int j = 0; j < instruments.Count; j++)
                {
                    Instrument instrument = instruments.ElementAt(j);

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
                        //strangleNode.TradedCalls.ElementAt(i).Option = instrument;
                        instruments[j] = instrument;
                    }
                }
                strangleNode.TradedStrangles.ElementAt(i).Put = instruments;
            }


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

        public virtual void OnNext(Tick[] ticks)
        {
            lock (ActiveStrangles)
            {
                for (int i = 0; i < ActiveStrangles.Count; i++)
                {
                    ReviewStrangle(ActiveStrangles.ElementAt(i).Value, ticks);

                    //   foreach (KeyValuePair<int, StrangleDataList> keyValuePair in ActiveStrangles)
                    // {
                    //  ReviewStrangle(keyValuePair.Value, ticks);
                    // }
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
            trade.TradingStatus = buyOrder ? TradeStatus.Closed : TradeStatus.Open;
            UpdateTradeDetails(strangleID, instrument.InstrumentToken, quantity, trade, triggerID);

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
                StrangleDataList strangleNode = new StrangleDataList();
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
    }
}
