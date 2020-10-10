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
    public class StrangleShiftToOneSide : IObserver<Tick[]>
    {
        public IDisposable UnsubscriptionToken;

        Dictionary<int, StrangleNode> ActiveStrangles = new Dictionary<int, StrangleNode>();

        public StrangleShiftToOneSide()
        {
            LoadActiveData();
        }

        private void ReviewStrangle(StrangleNode strangleNode, Tick[] ticks)
        {
            Instrument callOption = strangleNode.Call;
            Instrument putOption = strangleNode.Put;

            Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == callOption.InstrumentToken);
            if (optionTick.LastPrice != 0)
            {
                callOption.LastPrice = optionTick.LastPrice;
                callOption.Bids = optionTick.Bids;
                callOption.Offers = optionTick.Offers;
                strangleNode.Call = callOption;
            }
            optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == putOption.InstrumentToken);
            if (optionTick.LastPrice != 0)
            {
                putOption.LastPrice = optionTick.LastPrice;
                putOption.Bids = optionTick.Bids;
                putOption.Offers = optionTick.Offers;
                strangleNode.Put = putOption;
            }

            Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == strangleNode.BaseInstrumentToken);
            if (baseInstrumentTick.LastPrice != 0)
            {
                strangleNode.BaseInstrumentPrice = baseInstrumentTick.LastPrice;
            }
            if (strangleNode.BaseInstrumentPrice * callOption.LastPrice * putOption.LastPrice == 0)
            {
                return;
            }


            //Continue trading if position is open
            if (strangleNode.CurrentPosition == PositionStatus.Open)
            {
                List<ShortTrade> callTrades = strangleNode.CallTrades;
                List<ShortTrade> putTrades = strangleNode.PutTrades;

                //check for shift
                decimal initialCallPrice = Math.Abs(callTrades[0].AveragePrice);
                decimal initialPutPrice = Math.Abs(putTrades[0].AveragePrice);

                int callQty = strangleNode.CallTradedQty;
                int putQty = strangleNode.PutTradedQty;

                decimal threshold = strangleNode.Threshold = 0.3m; //0.2 for 20%



                //Check net P&L and determine if trading should be closed.
                decimal currentPL = strangleNode.NetPnL - (callQty * callOption.LastPrice + putQty * putOption.LastPrice);
                int PROFIT_POINTS = 25;
                if(currentPL > strangleNode.InitialQty * PROFIT_POINTS)
                {
                    ShortTrade trade = PlaceOrder(callOption.TradingSymbol, true, callOption.LastPrice, callQty, tickTime: ticks[0].Timestamp);
                    //increase trade record and quantity
                    callTrades.Add(trade);
                    strangleNode.CallTradedQty = callTrades.Sum(x => x.Quantity);
                    strangleNode.CallTrades = callTrades; //This may not be required. Just check
                    DataLogic dl = new DataLogic();
                    strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, callOption.InstrumentToken, trade, AlgoIndex.StrangleShiftToOneSide);

                    trade = PlaceOrder(putOption.TradingSymbol, true, putOption.LastPrice, putQty, tickTime: ticks[0].Timestamp);
                    //increase trade record and quantity
                    putTrades.Add(trade);
                    strangleNode.PutTradedQty = putTrades.Sum(x => x.Quantity);
                    strangleNode.PutTrades = putTrades; //This may not be required. Just check
                    strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, putOption.InstrumentToken, trade, AlgoIndex.StrangleShiftToOneSide);

                    strangleNode.CurrentPosition = PositionStatus.Closed;

                    //Take Fresh Trades
                    Instrument[] instruments = GetNewStrikes(strangleNode.BaseInstrumentToken, strangleNode.BaseInstrumentPrice, strangleNode.Call.Expiry);
                    callOption = instruments[0];
                    putOption = instruments[1];

                    /// Start a new trade
                    optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == callOption.InstrumentToken);
                    if (optionTick.LastPrice != 0)
                    {
                        callOption.LastPrice = optionTick.LastPrice;
                        callOption.Bids = optionTick.Bids;
                        callOption.Offers = optionTick.Offers;
                    }
                    optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == putOption.InstrumentToken);
                    if (optionTick.LastPrice != 0)
                    {
                        putOption.LastPrice = optionTick.LastPrice;
                        putOption.Bids = optionTick.Bids;
                        putOption.Offers = optionTick.Offers;
                    }

                    decimal pePrice = putOption.LastPrice;
                    decimal cePrice = callOption.LastPrice;
                    int initialQty = strangleNode.InitialQty;

                    ShortTrade callTrade;
                    ShortTrade putTrade;
                    ///Uncomment below for real time orders
                    if (pePrice * cePrice == 0)
                    {
                        callTrade = PlaceOrder(callOption.TradingSymbol.TrimEnd(), false, cePrice, initialQty, ticks[0].Timestamp, callOption.InstrumentToken);
                        putTrade = PlaceOrder(putOption.TradingSymbol.TrimEnd(), false, pePrice, initialQty, ticks[0].Timestamp, putOption.InstrumentToken);
                    }
                    else
                    {
                        callTrade = PlaceOrder(callOption.TradingSymbol.TrimEnd(), false, cePrice, initialQty);
                        putTrade = PlaceOrder(putOption.TradingSymbol.TrimEnd(), false, pePrice, initialQty);
                    }

                    //Update Database
                    int strangleId = dl.StoreStrangleData(ceToken: callOption.InstrumentToken, peToken: putOption.InstrumentToken, cePrice: callTrade.AveragePrice,
                       pePrice: putTrade.AveragePrice, bInstPrice: 0, algoIndex: AlgoIndex.StrangleShiftToOneSide, initialQty: initialQty,
                       maxQty: strangleNode.MaxQty, stepQty: strangleNode.StepQty, ceOrderId: callTrade.OrderId, peOrderId: putTrade.OrderId, transactionType: "Sell");

                    StrangleNode tempStrangleNode = new StrangleNode(callOption, putOption);
                    tempStrangleNode.BaseInstrumentToken = strangleNode.BaseInstrumentToken;
                    tempStrangleNode.BaseInstrumentPrice = strangleNode.BaseInstrumentPrice;
                    tempStrangleNode.InitialQty = tempStrangleNode.PutTradedQty = tempStrangleNode.CallTradedQty = strangleNode.InitialQty;
                    tempStrangleNode.NetPnL = callTrade.AveragePrice * Math.Abs(callTrade.Quantity) + putTrade.AveragePrice * Math.Abs(putTrade.Quantity);
                    tempStrangleNode.StepQty = strangleNode.StepQty;
                    tempStrangleNode.MaxQty = strangleNode.MaxQty;
                    tempStrangleNode.ID = strangleId;

                    tempStrangleNode.CallTrades.Add(callTrade);
                    tempStrangleNode.PutTrades.Add(putTrade);

                    ActiveStrangles.Add(strangleId, tempStrangleNode);



                    //        public void StoreStrangleShiftToOneSideTrade(Instrument bInst, Instrument currentPE, Instrument currentCE,
                    //int initialQty, int maxQty, int stepQty, decimal safetyWidth = 10, int strangleId = 0,
                    //DateTime timeOfOrder = default(DateTime))
                }


                if ((strangleNode.InitialQty >= strangleNode.PutTradedQty) && (Math.Floor((putOption.LastPrice - initialPutPrice) / (initialPutPrice * threshold)) >
                    Math.Floor(Convert.ToDecimal((strangleNode.InitialQty - strangleNode.PutTradedQty) / strangleNode.StepQty))))
                {
                    strangleNode.PutTradedQty -= strangleNode.StepQty;
                    strangleNode.CallTradedQty += strangleNode.StepQty;


                    //SET TRIGGER ID. This trigger id will be used to determine whether it has been closed or not
                    int triggerID = Math.Max(callTrades.Select(x => x.TriggerID).Max() + 1, putTrades.Select(x => x.TriggerID).Max() + 1);
                    ShortTrade trade = PlaceOrder(callOption.TradingSymbol, false, callOption.LastPrice, strangleNode.StepQty, triggerID: triggerID, tickTime: ticks[0].Timestamp);
                    //increase trade record and quantity
                    callTrades.Add(trade);
                    strangleNode.CallTradedQty = callTrades.Sum(x => x.Quantity);
                    strangleNode.CallTrades = callTrades; //This may not be required. Just check
                    DataLogic dl = new DataLogic();
                    strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, callOption.InstrumentToken, trade, AlgoIndex.StrangleShiftToOneSide, triggerID: triggerID);

                    trade = PlaceOrder(putOption.TradingSymbol, true, putOption.LastPrice, strangleNode.StepQty, triggerID:triggerID, tickTime: ticks[0].Timestamp);
                    //increase trade record and quantity
                    putTrades.Add(trade);
                    strangleNode.PutTradedQty = putTrades.Sum(x => x.Quantity);
                    strangleNode.PutTrades = putTrades; //This may not be required. Just check
                    strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, putOption.InstrumentToken, trade, AlgoIndex.StrangleShiftToOneSide, triggerID: triggerID);

                }
                if((strangleNode.InitialQty >= strangleNode.CallTradedQty) && (Math.Floor((callOption.LastPrice - initialCallPrice) / (initialCallPrice * threshold)) >
                   Math.Floor(Convert.ToDecimal((strangleNode.InitialQty - strangleNode.CallTradedQty) / strangleNode.StepQty))))
                {
                    strangleNode.PutTradedQty += strangleNode.StepQty;
                    strangleNode.CallTradedQty -= strangleNode.StepQty;

                    //SET TRIGGER ID. This trigger id will be used to determine whether it has been closed or not
                    int triggerID = Math.Max(callTrades.Select(x => x.TriggerID).Max() + 1, putTrades.Select(x => x.TriggerID).Max() + 1);

                    ShortTrade trade = PlaceOrder(callOption.TradingSymbol, true, callOption.LastPrice, strangleNode.StepQty, triggerID: triggerID, tickTime: ticks[0].Timestamp);
                    //increase trade record and quantity
                    callTrades.Add(trade);
                    strangleNode.CallTradedQty = callTrades.Sum(x => x.Quantity);
                    strangleNode.CallTrades = callTrades; //This may not be required. Just check
                    DataLogic dl = new DataLogic();
                    strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, callOption.InstrumentToken, trade, AlgoIndex.StrangleShiftToOneSide, triggerID: triggerID);

                    trade = PlaceOrder(putOption.TradingSymbol, false, putOption.LastPrice, strangleNode.StepQty, triggerID: triggerID, tickTime: ticks[0].Timestamp);
                    //increase trade record and quantity
                    putTrades.Add(trade);
                    strangleNode.PutTradedQty = putTrades.Sum(x => x.Quantity);
                    strangleNode.PutTrades = putTrades; //This may not be required. Just check
                    strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, putOption.InstrumentToken, trade, AlgoIndex.StrangleShiftToOneSide, triggerID: triggerID);
                }

                //stoploss trades in case trend reverses

                decimal buyBackCEThreshold = initialCallPrice * threshold;
                decimal buyBackPEThreshold = initialPutPrice * threshold;
                List<ShortTrade> openPutTrades = strangleNode.PutTrades.GroupBy(t => t.TriggerID).Where(g => g.Count() == 1).Select(e => e.First()).Where(x => x.TransactionType == "Buy").ToList<ShortTrade>();
                for (int i = 0; i < openPutTrades.Count; i++)
                {
                    if (putOption.LastPrice < openPutTrades.ElementAt(i).AveragePrice - buyBackPEThreshold)  //initialPutPrice * (1 + Math.Floor(((Math.Abs(initialPutPrice - openPutTrades.ElementAt(i).AveragePrice) / (initialPutPrice * threshold))) - 1) * threshold))
                    {
                        strangleNode.PutTradedQty += strangleNode.StepQty;
                        strangleNode.CallTradedQty -= strangleNode.StepQty;
                        //place order with same triggerid

                        int triggerID = openPutTrades.ElementAt(i).TriggerID;

                        ShortTrade trade = PlaceOrder(callOption.TradingSymbol, true, callOption.LastPrice, strangleNode.StepQty, triggerID: triggerID, tickTime: ticks[0].Timestamp);
                        //increase trade record and quantity
                        callTrades.Add(trade);
                        strangleNode.CallTradedQty = callTrades.Sum(x => x.Quantity);
                        strangleNode.CallTrades = callTrades; //This may not be required. Just check
                        DataLogic dl = new DataLogic();
                        strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, callOption.InstrumentToken, trade, AlgoIndex.StrangleShiftToOneSide, triggerID: triggerID);

                        trade = PlaceOrder(putOption.TradingSymbol, false, putOption.LastPrice, strangleNode.StepQty, triggerID: triggerID, tickTime: ticks[0].Timestamp);
                        //increase trade record and quantity
                        putTrades.Add(trade);
                        strangleNode.PutTradedQty = putTrades.Sum(x => x.Quantity);
                        strangleNode.PutTrades = putTrades; //This may not be required. Just check
                        strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, putOption.InstrumentToken, trade, AlgoIndex.StrangleShiftToOneSide, triggerID: triggerID);
                    }
                }

                List<ShortTrade> openCallTrades = strangleNode.CallTrades.GroupBy(t => t.TriggerID).Where(g => g.Count() == 1).Select(e => e.First()).Where(x => x.TransactionType == "Buy").ToList<ShortTrade>();
                for (int i = 0; i < openCallTrades.Count; i++)
                {
                    
                    if (callOption.LastPrice < initialCallPrice - buyBackCEThreshold) //initialCallPrice * (1 + Math.Floor(((Math.Abs(initialCallPrice - openCallTrades.ElementAt(i).AveragePrice) / (initialCallPrice * threshold))) - 1) * threshold))
                    {
                        strangleNode.CallTradedQty += strangleNode.StepQty;
                        strangleNode.PutTradedQty -= strangleNode.StepQty;
                        //place order with same triggerid

                        int triggerID = openCallTrades.ElementAt(i).TriggerID;

                        ShortTrade trade = PlaceOrder(callOption.TradingSymbol, true, callOption.LastPrice, strangleNode.StepQty, triggerID: triggerID, tickTime: ticks[0].Timestamp);
                        //increase trade record and quantity
                        callTrades.Add(trade);
                        strangleNode.CallTradedQty = callTrades.Sum(x => x.Quantity);
                        strangleNode.CallTrades = callTrades; //This may not be required. Just check
                        DataLogic dl = new DataLogic();
                        strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, callOption.InstrumentToken, trade, AlgoIndex.StrangleShiftToOneSide, triggerID: triggerID);

                        trade = PlaceOrder(putOption.TradingSymbol, false, putOption.LastPrice, strangleNode.StepQty, triggerID: triggerID, tickTime: ticks[0].Timestamp);
                        //increase trade record and quantity
                        putTrades.Add(trade);
                        strangleNode.PutTradedQty = putTrades.Sum(x => x.Quantity);
                        strangleNode.PutTrades = putTrades; //This may not be required. Just check
                        strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, putOption.InstrumentToken, trade, AlgoIndex.StrangleShiftToOneSide, triggerID: triggerID);

                    }
                }
            }

            #region OLD CODE 
            ////decimal pnl = strangleNode.NetPnL + strangleNode.Put.LastPrice*strangleNode.PutTradedQty + strangleNode.Call.LastPrice * strangleNode.CallTradedQty;
            ////sell traded qty
            //decimal pnl = strangleNode.NetPnL + strangleNode.Put.Bids[2].Price * strangleNode.PutTradedQty + strangleNode.Call.Bids[2].Price * strangleNode.CallTradedQty;

            //DataLogic dl = new DataLogic();

            //decimal profitPoints = Math.Abs((callTrades[0].AveragePrice + putTrades[0].AveragePrice) * 0.01m);

            //Console.WriteLine(pnl);
            //if (pnl > strangleNode.InitialQty * (profitPoints))
            //{
            //    //place order to buy calls
            //    ShortTrade trade = PlaceOrder(callOption.TradingSymbol, false, strangleNode.Call.Bids[2].Price, strangleNode.CallTradedQty);
            //    strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, callOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);

            //    trade = PlaceOrder(putOption.TradingSymbol, false, strangleNode.Put.Bids[2].Price, strangleNode.PutTradedQty);
            //    strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, putOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);

            //    strangleNode.CurrentPosition = PositionStatus.Closed;


            //    Instrument[] instruments = GetNewStrikes(strangleNode.BaseInstrumentToken, strangleNode.BaseInstrumentPrice, strangleNode.Call.Expiry);
            //    callOption = instruments[0];
            //    putOption = instruments[1];

            //    /// Start a new trade
            //    optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == callOption.InstrumentToken);
            //    if (optionTick.LastPrice != 0)
            //    {
            //        callOption.LastPrice = optionTick.LastPrice;
            //        callOption.Bids = optionTick.Bids;
            //        callOption.Offers = optionTick.Offers;
            //    }
            //    optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == putOption.InstrumentToken);
            //    if (optionTick.LastPrice != 0)
            //    {
            //        putOption.LastPrice = optionTick.LastPrice;
            //        putOption.Bids = optionTick.Bids;
            //        putOption.Offers = optionTick.Offers;
            //    }

            //    decimal pePrice = putOption.LastPrice;
            //    decimal cePrice = callOption.LastPrice;
            //    int initialQty = strangleNode.InitialQty;

            //    ShortTrade callTrade;
            //    ShortTrade putTrade;
            //    ///Uncomment below for real time orders
            //    if (pePrice * cePrice == 0)
            //    {
            //        callTrade = PlaceOrder(callOption.TradingSymbol.TrimEnd(), true, cePrice, initialQty, ticks[0].Timestamp, callOption.InstrumentToken);
            //        putTrade = PlaceOrder(putOption.TradingSymbol.TrimEnd(), true, pePrice, initialQty, ticks[0].Timestamp, putOption.InstrumentToken);
            //    }
            //    else
            //    {
            //        callTrade = PlaceOrder(callOption.TradingSymbol.TrimEnd(), true, cePrice, initialQty);
            //        putTrade = PlaceOrder(putOption.TradingSymbol.TrimEnd(), true, pePrice, initialQty);
            //    }

            //    //Update Database
            //    int strangleId = dl.StoreStrangleData(ceToken: callOption.InstrumentToken, peToken: putOption.InstrumentToken, cePrice: callTrade.AveragePrice,
            //       pePrice: putTrade.AveragePrice, bInstPrice: 0, algoIndex: AlgoIndex.ActiveTradeWithVariableQty, initialQty: initialQty,
            //       maxQty: strangleNode.MaxQty, stepQty: strangleNode.StepQty, ceOrderId: callTrade.OrderId, peOrderId: putTrade.OrderId, transactionType: "Buy");

            //    StrangleNode tempStrangleNode = new StrangleNode(callOption, putOption);
            //    tempStrangleNode.BaseInstrumentToken = strangleNode.BaseInstrumentToken;
            //    tempStrangleNode.BaseInstrumentPrice = strangleNode.BaseInstrumentPrice;
            //    tempStrangleNode.InitialQty = tempStrangleNode.PutTradedQty = tempStrangleNode.CallTradedQty = strangleNode.InitialQty;
            //    tempStrangleNode.NetPnL = callTrade.AveragePrice * Math.Abs(callTrade.Quantity) + putTrade.AveragePrice * Math.Abs(putTrade.Quantity);
            //    tempStrangleNode.StepQty = strangleNode.StepQty;
            //    tempStrangleNode.MaxQty = strangleNode.MaxQty;
            //    tempStrangleNode.ID = strangleId;

            //    tempStrangleNode.CallTrades.Add(callTrade);
            //    tempStrangleNode.PutTrades.Add(putTrade);

            //    ActiveStrangles.Add(strangleId, tempStrangleNode);
            //}
            //else
            //{

            //    decimal putBid = putOption.Bids.Count() >= 3 && putOption.Bids[2].Price != 0 ? putOption.Bids[2].Price : putOption.Bids.Count() >= 2 && putOption.Bids[1].Price != 0 ? putOption.Bids[1].Price : putOption.LastPrice;
            //    decimal putOffer = putOption.Offers.Count() >= 3 && putOption.Offers[2].Price != 0 ? putOption.Offers[2].Price : putOption.Offers.Count() >= 2 && putOption.Offers[1].Price != 0 ? putOption.Offers[1].Price : putOption.LastPrice;
            //    decimal callBid = callOption.Bids.Count() >= 3 && callOption.Bids[2].Price != 0 ? callOption.Bids[2].Price : callOption.Bids.Count() >= 2 && callOption.Bids[1].Price != 0 ? callOption.Bids[1].Price : callOption.LastPrice;
            //    decimal callOffer = callOption.Offers.Count() >= 3 && callOption.Offers[2].Price != 0 ? callOption.Offers[2].Price : callOption.Offers.Count() >= 2 && callOption.Offers[1].Price != 0 ? callOption.Offers[1].Price : callOption.LastPrice;

            //    //int PutSellCallBuyQty = Convert.ToInt32((callOption.Offers[2].Price / putOption.Bids[2].Price) * strangleNode.CallTradedQty - strangleNode.PutTradedQty);
            //    //int PutBuyCallSellQty = Convert.ToInt32((callOption.Bids[2].Price / putOption.Offers[2].Price) * strangleNode.CallTradedQty - strangleNode.PutTradedQty);

            //    int PutSellCallBuyQty = Convert.ToInt32((callOffer / putBid) * strangleNode.CallTradedQty - strangleNode.PutTradedQty);
            //    int PutBuyCallSellQty = Convert.ToInt32((callBid / putOffer) * strangleNode.CallTradedQty - strangleNode.PutTradedQty);

            //    if (Math.Abs(PutBuyCallSellQty) >= strangleNode.StepQty) //need more puts or less calls
            //    {
            //        if (PutBuyCallSellQty > 0)
            //        {
            //            if (strangleNode.CallTradedQty > strangleNode.InitialQty)
            //            {
            //                //place order to sell calls
            //                ShortTrade trade = PlaceOrder(callOption.TradingSymbol, false, callOption.Bids[2].Price, strangleNode.StepQty);

            //                //increase trade record and quantity
            //                callTrades.Add(trade);
            //                strangleNode.CallTradedQty = callTrades.Sum(x => x.Quantity);

            //                strangleNode.CallTrades = callTrades; //This may not be required. Just check

            //                strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, callOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);

            //            }
            //            else if (strangleNode.PutTradedQty < strangleNode.MaxQty)
            //            {
            //                //place order to buy Puts

            //                ShortTrade trade = PlaceOrder(putOption.TradingSymbol, true, putOption.Offers[2].Price, strangleNode.StepQty);

            //                //increase trade record and quantity
            //                putTrades.Add(trade);
            //                strangleNode.PutTradedQty = putTrades.Sum(x => x.Quantity);

            //                strangleNode.PutTrades = putTrades; //This may not be required. Just check

            //                strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, putOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);
            //            }
            //            else
            //            {

            //            }
            //        }
            //    }
            //    if (Math.Abs(PutSellCallBuyQty) >= strangleNode.StepQty) //need more calls or less puts
            //    {
            //        if (PutSellCallBuyQty < 0)
            //        {
            //            if (strangleNode.PutTradedQty > strangleNode.InitialQty)
            //            {
            //                //place order to sell puts
            //                ShortTrade trade = PlaceOrder(putOption.TradingSymbol, false, putOption.Bids[2].Price, strangleNode.StepQty);

            //                //increase trade record and quantity
            //                putTrades.Add(trade);
            //                strangleNode.PutTradedQty = putTrades.Sum(x => x.Quantity);

            //                strangleNode.PutTrades = putTrades; //This may not be required. Just check

            //                strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, putOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);
            //            }
            //            else if (strangleNode.CallTradedQty < strangleNode.MaxQty)
            //            {
            //                //place order to buy Call
            //                ShortTrade trade = PlaceOrder(callOption.TradingSymbol, true, callOption.Offers[2].Price, strangleNode.StepQty);

            //                //increase trade record and quantity
            //                callTrades.Add(trade);
            //                strangleNode.CallTradedQty = callTrades.Sum(x => x.Quantity);

            //                strangleNode.CallTrades = callTrades; //This may not be required. Just check

            //                strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, callOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);
            //            }
            //            else
            //            {

            //            }
            //        }
            //    }


            //if (Math.Abs(putBuyQtyNeeded) >= strangleNode.StepQty)
            // {
            //     if (putQtyNeeded > 0) //need more puts or less calls
            //     {
            //         if (strangleNode.CallTradedQty > strangleNode.InitialQty)
            //         {
            //             //place order to sell calls
            //             ShortTrade trade = PlaceOrder(callOption.TradingSymbol, false, callOption.LastPrice, strangleNode.StepQty);

            //             //increase trade record and quantity
            //             callTrades.Add(trade);
            //             strangleNode.CallTradedQty = callTrades.Sum(x => x.Quantity);

            //             strangleNode.CallTrades = callTrades; //This may not be required. Just check

            //             strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, callOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);

            //         }
            //         else if (strangleNode.PutTradedQty < strangleNode.MaxQty)
            //         {
            //             //place order to buy Puts
            //             ShortTrade trade = PlaceOrder(putOption.TradingSymbol, true, putOption.LastPrice, strangleNode.StepQty);

            //             //increase trade record and quantity
            //             putTrades.Add(trade);
            //             strangleNode.PutTradedQty = putTrades.Sum(x => x.Quantity);

            //             strangleNode.PutTrades = putTrades; //This may not be required. Just check

            //             strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, putOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);
            //         }
            //         else
            //         {

            //         }
            //     }
            //     else //need more calls or less puts
            //     {
            //         if (strangleNode.PutTradedQty > strangleNode.InitialQty)
            //         {
            //             //place order to sell puts
            //             ShortTrade trade = PlaceOrder(putOption.TradingSymbol, false, putOption.LastPrice, strangleNode.StepQty);

            //             //increase trade record and quantity
            //             putTrades.Add(trade);
            //             strangleNode.PutTradedQty = putTrades.Sum(x => x.Quantity);

            //             strangleNode.PutTrades = putTrades; //This may not be required. Just check

            //             strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, putOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);
            //         }
            //         else if (strangleNode.CallTradedQty < strangleNode.MaxQty)
            //         {
            //             //place order to buy Puts
            //             ShortTrade trade = PlaceOrder(callOption.TradingSymbol, true, callOption.LastPrice, strangleNode.StepQty);

            //             //increase trade record and quantity
            //             callTrades.Add(trade);
            //             strangleNode.CallTradedQty = callTrades.Sum(x => x.Quantity);

            //             strangleNode.CallTrades = callTrades; //This may not be required. Just check

            //             strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, callOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);
            //         }
            //         else
            //         {

            //         }
            //     }
            // }
            // }
            //}
            //}
            #endregion
        }


        public Instrument[] GetNewStrikes(uint baseInstrumentToken, decimal baseInstrumentPrice, DateTime? expiry)
        {
            DataLogic dl = new DataLogic();
            SortedList<Decimal, Instrument>[] nodeData = dl.RetrieveNextStrangleNodes(baseInstrumentToken, expiry.GetValueOrDefault(DateTime.Now), baseInstrumentPrice, baseInstrumentPrice, 0);

            SortedList<Decimal, Instrument> calls = nodeData[0];
            SortedList<Decimal, Instrument> puts = nodeData[1];

            IEnumerable<KeyValuePair<decimal, Instrument>> callkeyvalue = calls.Where(x => x.Key > baseInstrumentPrice);
            IEnumerable<KeyValuePair<decimal, Instrument>> putkeyvalue = puts.Where(x => x.Key < baseInstrumentPrice);

            var call = callkeyvalue.ElementAt(0).Value;
            var put = putkeyvalue.ElementAt(callkeyvalue.Count() - 1).Value;
            return new Instrument[] { call, put };
        }

        public virtual void OnNext(Tick[] ticks)
        {
            lock (ActiveStrangles)
            {
                for (int i = 0; i < ActiveStrangles.Count; i++)
                {
                    ReviewStrangle(ActiveStrangles.ElementAt(i).Value, ticks);

                    //   foreach (KeyValuePair<int, StrangleNode> keyValuePair in ActiveStrangles)
                    // {
                    //  ReviewStrangle(keyValuePair.Value, ticks);
                    // }
                }
            }
        }
        private ShortTrade PlaceOrder(string tradingSymbol, bool buyOrder, decimal currentPrice, int quantity, DateTime? tickTime = null, uint token = 0, int triggerID = 0)
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
            trade.Quantity = buyOrder ? quantity*-1 : quantity * 1;
            trade.OrderId = orderId;
            trade.TransactionType = buyOrder ? "Buy" : "Sell";
            trade.TriggerID = triggerID;
            return trade;
        }

        private void LoadActiveData()
        {
            AlgoIndex algoIndex = AlgoIndex.StrangleShiftToOneSide;
            DataLogic dl = new DataLogic();
            DataSet activeStrangles = dl.RetrieveActiveStrangleData(algoIndex);
            DataRelation strategy_Token_Relation = activeStrangles.Relations.Add("Strangle_Token", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"] },
                new DataColumn[] { activeStrangles.Tables[1].Columns["StrategyId"] });

            DataRelation strategy_Trades_Relation = activeStrangles.Relations.Add("Strangle_Trades", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"] },
                new DataColumn[] { activeStrangles.Tables[2].Columns["StrategyId"] });

            Instrument call, put;

            foreach (DataRow strangleRow in activeStrangles.Tables[0].Rows)
            {
                DataRow strangleTokenRow = strangleRow.GetChildRows(strategy_Token_Relation)[0];

                call = new Instrument()
                {
                    BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
                    InstrumentToken = Convert.ToUInt32(strangleTokenRow["CallToken"]),
                    InstrumentType = "CE",
                    Strike = (Decimal)strangleTokenRow["CallStrike"],
                    TradingSymbol = (string)strangleTokenRow["CallSymbol"]
                };
                put = new Instrument()
                {
                    BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
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

                StrangleNode strangleNode = new StrangleNode(call, put);

                strangleNode.InitialQty = (int)strangleTokenRow["CallInitialQty"];

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
                        strangleNode.CallTrades.Add(trade);
                    }
                    else if (Convert.ToUInt32(strangleTradeRow["InstrumentToken"]) == put.InstrumentToken)
                    {
                        strangleNode.PutTrades.Add(trade);
                    }
                    netPnL += trade.AveragePrice * Math.Abs(trade.Quantity);
                }
                strangleNode.BaseInstrumentToken = call.BaseInstrumentToken;
                strangleNode.PutTradedQty = strangleNode.PutTrades.Sum(x => x.Quantity);
                strangleNode.CallTradedQty = strangleNode.CallTrades.Sum(x => x.Quantity);
                strangleNode.CurrentPosition = PositionStatus.Open;
                strangleNode.MaxQty = (int)strangleRow["MaxQty"];
                strangleNode.StepQty = Convert.ToInt32(strangleRow["MaxProfitPoints"]);
                strangleNode.NetPnL = netPnL;
                strangleNode.ID = (int)strangleRow["Id"];

                ActiveStrangles.Add(strangleNode.ID, strangleNode);
            }
        }

        public void StoreStrangleShiftToOneSideTrade(Instrument bInst, Instrument currentPE, Instrument currentCE,
            int initialQty, int maxQty, int stepQty, decimal safetyWidth = 10, int strangleId = 0,
            DateTime timeOfOrder = default(DateTime))
        {
            if (currentPE.LastPrice * currentCE.LastPrice != 0)
            {
                //If new strangle, place the order and update the data base. If old strangle monitor it.
                if (strangleId == 0)
                {
                    //Dictionary<string, Quote> keyValuePairs = ZObjects.kite.GetQuote(new string[] { Convert.ToString(currentCE.InstrumentToken),
                    //                                            Convert.ToString(currentPE.InstrumentToken) });

                    //decimal cePrice = keyValuePairs[Convert.ToString(currentCE.InstrumentToken)].Bids[0].Price;
                    //decimal pePrice = keyValuePairs[Convert.ToString(currentPE.InstrumentToken)].Bids[0].Price;
                    //decimal bPrice = keyValuePairs[Convert.ToString(bInst.InstrumentToken)].Bids[0].Price;
                    timeOfOrder = DateTime.Now;

                    //TEMP -> First price
                    decimal pePrice = currentPE.LastPrice;
                    decimal cePrice = currentCE.LastPrice;
                    decimal bPrice = bInst.LastPrice;
                    //////put.Prices.Add(pePrice); //put.SellPrice = 100;
                    //call.Prices.Add(cePrice);  // call.SellPrice = 100;

                    ///Uncomment below for real time orders
                    ShortTrade callTrade = PlaceOrder(currentCE.TradingSymbol, false, cePrice, initialQty);
                    ShortTrade putTrade = PlaceOrder(currentPE.TradingSymbol, false, pePrice, initialQty);

                    //Update Database
                    DataLogic dl = new DataLogic();
                    strangleId = dl.StoreStrangleData(ceToken: currentCE.InstrumentToken, peToken: currentPE.InstrumentToken, cePrice: callTrade.AveragePrice,
                       pePrice: putTrade.AveragePrice, bInstPrice: 0, algoIndex: AlgoIndex.StrangleShiftToOneSide, initialQty: initialQty,
                       maxQty: maxQty, stepQty: stepQty, ceOrderId: callTrade.OrderId, peOrderId: putTrade.OrderId, transactionType: "Sell");
                }
            }
        }
        public virtual void OnError(Exception ex)
        {
            ///TODO: Log the error. Also handle the error.
        }

        public virtual void OnCompleted()
        {
        }
    }
}
