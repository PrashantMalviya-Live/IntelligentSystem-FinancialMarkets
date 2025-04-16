using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Algorithms.Utilities;
using GlobalLayer;
using KiteConnect;
using BrokerConnectWrapper;
//using Pub_Sub;
//using MarketDataTest;
using System.Data;

namespace Algos.TLogics
{
    public class ManageStrangleConstantWidth : IObserver<Tick[]>
    {
        public ManageStrangleConstantWidth()
        {
            LoadActiveData();
        }

        Dictionary<int, StrangleLinkedList> ActiveStrangles = new Dictionary<int, StrangleLinkedList>();
        //Instrument _bInst;
        decimal safetyBand = 10;
        const double SAFETY_MARGIN = 0.1;
        const double DELTA_NEUTRAL_BAND = 0.03;
        const double DELTA_UPPER_CAP = 0.37;
        //decimal baseInstrumentPrice = 29360; //TODO: Remove this

        //make sure ref is working with struct . else make it class
        public virtual void OnNext(Tick[] ticks)
        {
            lock (ActiveStrangles)
            {
                foreach (KeyValuePair<int, StrangleLinkedList> keyValuePair in ActiveStrangles)
                {
                    ReviewStrangle(keyValuePair.Value, ticks);
                }
            }
        }

        private void ReviewStrangle(StrangleLinkedList strangleList, Tick[] ticks)
        {
            StrangleListNode strangleNode = strangleList.Current;
            Instrument callOption = strangleNode.Call;
            Instrument putOption = strangleNode.Put;

            //Instrument baseInstrument = strangleList.BaseInstrument;

            Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == strangleList.BaseInstrumentToken);
            if (baseInstrumentTick.LastPrice != 0)
            {
                strangleList.BaseInstrumentPrice = baseInstrumentTick.LastPrice;
            }

            Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == callOption.InstrumentToken);
            if (optionTick.LastPrice != 0)
            {
                callOption.LastPrice = optionTick.LastPrice;
                strangleList.Current.Call = callOption;
            }

            optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == putOption.InstrumentToken);
            if (optionTick.LastPrice != 0)
            {
                putOption.LastPrice = optionTick.LastPrice;
                strangleList.Current.Put = putOption;
            }

            //Pulling next nodes from the begining and keeping it up to date from the ticks. This way next nodes will be available when needed, as Tick table takes time.
            //if (strangleNode.NextNode == null || strangleNode.PrevNode == null)
                AssignNeighbouringNodes(strangleNode, callOption.BaseInstrumentToken, callOption, putOption);

            UpdateLastPriceOnAllNodes(ref strangleNode, ref ticks);

            int currentIndex = strangleNode.Index;
            //StrangleListNode minimumPremiumNode = GetSubsequentNodes(strangleNode, strangleList.BaseInstrumentPrice, ticks);
            StrangleListNode minimumPremiumNode = GetSubsequentNodes_v2(strangleNode, strangleList.BaseInstrumentPrice, ticks);
            

            decimal[] prevNodeBuyPrice = new decimal[3];
            decimal[] currentNodeSellPrice = new decimal[3];
            if (currentIndex != minimumPremiumNode.Index)
            {
                //buy current strangle and close this position
                //buy prices are negative and sell are positive. as sign denotes cashflow direction
                prevNodeBuyPrice[0] = PlaceOrder(callOption.TradingSymbol, buyOrder: true, callOption.LastPrice);
                prevNodeBuyPrice[1] = PlaceOrder(putOption.TradingSymbol, buyOrder: true, putOption.LastPrice);
                prevNodeBuyPrice[2] = strangleList.BaseInstrumentPrice;
                strangleNode.Prices.Add(prevNodeBuyPrice);

                uint prevCallToken = callOption.InstrumentToken;
                uint prevPutToken = putOption.InstrumentToken;

                int prevInstrumentIndex = strangleNode.Index;

                //Logic to determine the next node and move there
                
                callOption = minimumPremiumNode.Call;
                putOption = minimumPremiumNode.Put;

                if (minimumPremiumNode.Prices.Count > 0 && minimumPremiumNode.Prices[0].First() == 0)
                {
                    minimumPremiumNode.Prices.RemoveAt(0);
                }

                currentNodeSellPrice[0] = PlaceOrder(callOption.TradingSymbol, buyOrder: false, callOption.LastPrice);
                currentNodeSellPrice[1] = PlaceOrder(putOption.TradingSymbol, buyOrder: false, putOption.LastPrice);
                currentNodeSellPrice[2] = strangleList.BaseInstrumentPrice;
                minimumPremiumNode.Prices.Add(currentNodeSellPrice);

                //buy prices are negative and sell are positive. as sign denotes cashflow direction

                //Do we need current instrument index , when current instrument is already present in the linked list?
                //It can see the index of current instrument using list.current.index
                strangleList.CurrentInstrumentIndex = minimumPremiumNode.Index;

                // double callDelta = currentNode.Call.UpdateDelta(Convert.ToDouble(currentNode.Call.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
                double callDelta = minimumPremiumNode.Call.UpdateDelta(Convert.ToDouble(minimumPremiumNode.Call.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(strangleList.BaseInstrumentPrice));
                callDelta = Math.Abs(callDelta);

                double putDelta = minimumPremiumNode.Put.UpdateDelta(Convert.ToDouble(minimumPremiumNode.Put.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(strangleList.BaseInstrumentPrice));
                putDelta = Math.Abs(putDelta);

                minimumPremiumNode.DeltaThreshold = Math.Max(callDelta, putDelta) + SAFETY_MARGIN;


                //make next symbol as next to current node. Also update PL data in linklist main
                //also update currnetindex in the linklist main
                DataLogic dl = new DataLogic();
                dl.UpdateListData(strangleList.listID, callOption.InstrumentToken, putOption.InstrumentToken, prevCallToken, prevPutToken,
                    minimumPremiumNode.Index, prevInstrumentIndex, currentNodeSellPrice, prevNodeBuyPrice, AlgoIndex.StrangleWithConstantWidth, strangleList.BaseInstrumentPrice);

                //Update list to set the node as current
                strangleList.Current = minimumPremiumNode;
            }
        }
        private decimal PlaceOrder(string tradingSymbol, bool buyOrder, decimal currentPrice)
        {
            //Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
            //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, 100, Product: Constants.PRODUCT_MIS,
            //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            //string orderId = "0";
            decimal averagePrice = 0;
            //if (orderStatus["data"]["order_id"] != null)
            //{
            //    orderId = orderStatus["data"]["order_id"];
            //}
            //if (orderId != "0")
            //{
            //    System.Threading.Thread.Sleep(200);
            //    List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
            //    averagePrice = orderInfo[orderInfo.Count - 1].AveragePrice;
            //}
            if (averagePrice == 0)
                averagePrice = buyOrder ? currentPrice + 1 : currentPrice - 1;
            return buyOrder ? averagePrice * -1 : averagePrice;
        }

        private StrangleListNode GetSubsequentNodes_v2(StrangleListNode strangleNode, decimal baseInstrumentPrice, Tick[] ticks)
        {
            StrangleListNode currentNode = strangleNode;
            StrangleListNode subsequentNode = currentNode;

            //Store base instrument price at the begining as reference and then with every trade store the reference.
            //check with last reference for say 100 point movements, or check for node crossover of base instrument.

            if (strangleNode.Prices.Count > 0)
            {
                //decimal stranglePremium = strangleNode.Prices[0].Last() + strangleNode.Prices[1].Last();
                decimal stranglePremium = currentNode.Call.LastPrice + currentNode.Put.LastPrice - safetyBand;
                decimal premiumAvailable;

                // double callDelta = currentNode.Call.UpdateDelta(Convert.ToDouble(currentNode.Call.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
                double callDelta = currentNode.Call.UpdateDelta(Convert.ToDouble(currentNode.Call.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(baseInstrumentPrice));
                callDelta = Math.Abs(callDelta);

                double putDelta = currentNode.Put.UpdateDelta(Convert.ToDouble(currentNode.Put.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(baseInstrumentPrice));
                putDelta = Math.Abs(putDelta);

                if (putDelta < strangleNode.DeltaThreshold - SAFETY_MARGIN && callDelta < strangleNode.DeltaThreshold - SAFETY_MARGIN)
                {
                    strangleNode.DeltaThreshold = Math.Max(putDelta, callDelta) + SAFETY_MARGIN;
                    Console.WriteLine(strangleNode.DeltaThreshold);
                }

                if (callDelta > strangleNode.DeltaThreshold && currentNode.Call.LastPrice != 0)
                {
                    subsequentNode = currentNode;

                    while (true)
                    {
                        if (subsequentNode.NextNode == null)
                        {
                            AssignNeighbouringNodes(subsequentNode, subsequentNode.Call.BaseInstrumentToken, subsequentNode.Call, subsequentNode.Put);
                            if (subsequentNode.NextNode == null)
                            {
                                return subsequentNode;
                            }
                        }
                        subsequentNode = subsequentNode.NextNode;
                        if (subsequentNode.Call.LastPrice == 0 || subsequentNode.Put.LastPrice == 0)
                        { return subsequentNode.PrevNode; }
                        // callDelta = subsequentNode.Call.UpdateDelta(Convert.ToDouble(subsequentNode.Call.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
                        callDelta = subsequentNode.Call.UpdateDelta(Convert.ToDouble(subsequentNode.Call.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(baseInstrumentPrice));
                        callDelta = Math.Abs(callDelta);

                        // putDelta = subsequentNode.Call.UpdateDelta(Convert.ToDouble(subsequentNode.Put.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
                        putDelta = subsequentNode.Put.UpdateDelta(Convert.ToDouble(subsequentNode.Put.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(baseInstrumentPrice));
                        putDelta = Math.Abs(putDelta);

                        if (double.IsNaN(callDelta) || double.IsNaN(putDelta))
                        {
                            return subsequentNode.PrevNode;
                        }
                        if (Math.Abs(callDelta - putDelta) < DELTA_NEUTRAL_BAND) //90% of upper delta
                        {
                            break;
                        }
                    }
                }
                else
                {
                    //double putDelta = currentNode.Put.UpdateDelta(Convert.ToDouble(currentNode.Put.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
                   
                    if (putDelta > strangleNode.DeltaThreshold && currentNode.Put.LastPrice != 0)
                    {
                        subsequentNode = currentNode;

                        while (true)
                        {
                            if (subsequentNode.PrevNode == null)
                            {
                                AssignNeighbouringNodes(subsequentNode, subsequentNode.Call.BaseInstrumentToken, subsequentNode.Call, subsequentNode.Put);
                                if (subsequentNode.PrevNode == null)
                                {
                                    return subsequentNode;
                                }
                            }

                            subsequentNode = subsequentNode.PrevNode;

                            if (subsequentNode.Call.LastPrice == 0 || subsequentNode.Put.LastPrice == 0)
                            { return subsequentNode.NextNode; }

                            // callDelta = subsequentNode.Call.UpdateDelta(Convert.ToDouble(subsequentNode.Call.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
                            callDelta = subsequentNode.Call.UpdateDelta(Convert.ToDouble(subsequentNode.Call.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(baseInstrumentPrice));
                            callDelta = Math.Abs(callDelta);

                            //putDelta = subsequentNode.Put.UpdateDelta(Convert.ToDouble(subsequentNode.Put.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
                            putDelta = subsequentNode.Put.UpdateDelta(Convert.ToDouble(subsequentNode.Put.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(baseInstrumentPrice));
                            putDelta = Math.Abs(putDelta);

                            if (double.IsNaN(callDelta) || double.IsNaN(putDelta))
                            {
                                return subsequentNode.NextNode;
                            }
                            if (Math.Abs(callDelta - putDelta) < DELTA_NEUTRAL_BAND) //90% of upper delta
                            {
                                break;
                            }
                        }
                    }
                }

            }
            return subsequentNode;
        }
       
        private StrangleListNode GetSubsequentNodes(StrangleListNode strangleNode, decimal baseInstrumentPrice, Tick[] ticks)
        {
            StrangleListNode currentNode = strangleNode;
            StrangleListNode subsequentNode = currentNode;

            //Store base instrument price at the begining as reference and then with every trade store the reference.
            //check with last reference for say 100 point movements, or check for node crossover of base instrument.

            if (strangleNode.Prices.Count > 0)
            {
                //decimal stranglePremium = strangleNode.Prices[0].Last() + strangleNode.Prices[1].Last();
                decimal stranglePremium = currentNode.Call.LastPrice + currentNode.Put.LastPrice - safetyBand;
                decimal premiumAvailable;

               // double callDelta = currentNode.Call.UpdateDelta(Convert.ToDouble(currentNode.Call.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
                double callDelta = currentNode.Call.UpdateDelta(Convert.ToDouble(currentNode.Call.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(baseInstrumentPrice));
                callDelta = Math.Abs(callDelta);
                if (callDelta > 0.4 && currentNode.Call.LastPrice != 0)
                {
                    subsequentNode = currentNode;

                    while (true)
                    {
                        if (subsequentNode.NextNode == null)
                        {
                            AssignNeighbouringNodes(subsequentNode, subsequentNode.Call.BaseInstrumentToken, subsequentNode.Call, subsequentNode.Put);
                            if (subsequentNode.NextNode == null)
                            {
                                return subsequentNode;
                            }
                        }
                        subsequentNode = subsequentNode.NextNode;
                        if(subsequentNode.Call.LastPrice == 0 || subsequentNode.Put.LastPrice == 0)
                        { return subsequentNode.PrevNode; }
                       // callDelta = subsequentNode.Call.UpdateDelta(Convert.ToDouble(subsequentNode.Call.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
                        callDelta = subsequentNode.Call.UpdateDelta(Convert.ToDouble(subsequentNode.Call.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(baseInstrumentPrice));

                        callDelta = Math.Abs(callDelta);

                        if (double.IsNaN(callDelta))
                        {
                            return subsequentNode.PrevNode; 
                        }
                        if (callDelta <= 0.32) //90% of upper delta
                        {
                            break;
                        }
                    }
                }
                else
                {
                    //double putDelta = currentNode.Put.UpdateDelta(Convert.ToDouble(currentNode.Put.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
                    double putDelta = currentNode.Put.UpdateDelta(Convert.ToDouble(currentNode.Put.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(baseInstrumentPrice));
                    putDelta = Math.Abs(putDelta);
                    if (putDelta > 0.4 && currentNode.Put.LastPrice != 0)
                    {
                        subsequentNode = currentNode;

                        while (true)
                        {
                            if (subsequentNode.PrevNode == null)
                            {
                                AssignNeighbouringNodes(subsequentNode, subsequentNode.Call.BaseInstrumentToken, subsequentNode.Call, subsequentNode.Put);
                                if (subsequentNode.PrevNode == null)
                                {
                                    return subsequentNode;
                                }
                            }

                            subsequentNode = subsequentNode.PrevNode;

                            if (subsequentNode.Call.LastPrice == 0 || subsequentNode.Put.LastPrice == 0)
                            { return subsequentNode.NextNode; }

                            //putDelta = subsequentNode.Put.UpdateDelta(Convert.ToDouble(subsequentNode.Put.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
                            putDelta = subsequentNode.Put.UpdateDelta(Convert.ToDouble(subsequentNode.Put.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(baseInstrumentPrice));
                            putDelta = Math.Abs(putDelta);

                            if (double.IsNaN(putDelta))
                            {
                                return subsequentNode.NextNode;
                            }
                            if (putDelta <= 0.32) //90% of upper delta
                            {
                                break;
                            }
                        }
                    }
                }

            }
            return subsequentNode;
        }
        private void UpdateLastPriceOnAllNodes(ref StrangleListNode currentNode, ref Tick[] ticks)
        {
            Instrument option;
            Tick optionTick;

            int currentNodeIndex = currentNode.Index;

            //go to the first node:
            while (currentNode.PrevNode != null)
            {
                currentNode = currentNode.PrevNode;
            }

            //Update all the price all the way till the end
            while (currentNode.NextNode != null)
            {
                option = currentNode.Call;
                optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == option.InstrumentToken);
                if (optionTick.LastPrice != 0)
                {
                    option.LastPrice = optionTick.LastPrice;
                    currentNode.Call = option;
                }

                option = currentNode.Put;
                optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == option.InstrumentToken);
                if (optionTick.LastPrice != 0)
                {
                    option.LastPrice = optionTick.LastPrice;
                    currentNode.Put = option;
                }

                currentNode = currentNode.NextNode;
            }

            //Come back to the current node
            while (currentNode.PrevNode != null)
            {
                if (currentNode.Index == currentNodeIndex)
                {
                    break;
                }
                currentNode = currentNode.PrevNode;
            }
        }

        private void AssignNeighbouringNodes(StrangleListNode strangleNode, UInt32 baseInstrumentToken, Instrument callOption, Instrument putOption)
        {
            int? retrievalIndex = GetRetrievalIndex(strangleNode);
            if (retrievalIndex == null)
                return;

            int currentIndex = strangleNode.Index;

            DataLogic dl = new DataLogic();
            SortedList<Decimal, Instrument>[] NodeData = dl.RetrieveNextStrangleNodes(baseInstrumentToken, callOption.Expiry.Value,
                callOption.Strike, putOption.Strike, retrievalIndex.Value);

            //Instrument currentInstrument = currentNode.Instrument;

            SortedList<Decimal, Instrument> callNodeData = NodeData[0];
            SortedList<Decimal, Instrument> putNodeData = NodeData[1];

            int lowerCallNodeCount = callNodeData.Count(x => x.Key < strangleNode.Call.Strike);
            int lowerPutNodeCount = putNodeData.Count(x => x.Key < strangleNode.Put.Strike);
            int lowerNodeCount = Math.Min(lowerCallNodeCount, lowerPutNodeCount);


            var lowerCallNodeData = callNodeData.Where(x => x.Key < strangleNode.Call.Strike).ToList();
            var lowerPutNodeData = putNodeData.Where(x => x.Key < strangleNode.Put.Strike).ToList();

            for (int i = lowerNodeCount-1; i >=0; i--)
            {
                StrangleListNode strangle = new StrangleListNode(lowerCallNodeData[--lowerCallNodeCount].Value, lowerPutNodeData[--lowerPutNodeCount].Value);

                strangle.Index = strangleNode.Index - 1;
                strangleNode.PrevNode = strangle;
                strangle.NextNode = strangleNode;

                strangleNode = strangleNode.PrevNode;
            }

            strangleNode = strangleNode.GetNodebyIndex((Int16) currentIndex);

            int higherCallNodeCount = callNodeData.Count(x => x.Key > strangleNode.Call.Strike);
            int higherPutNodeCount = callNodeData.Count(x => x.Key > strangleNode.Put.Strike);
            int higherNodeCount = Math.Min(higherCallNodeCount, higherPutNodeCount);


            var higherCallNodeData = callNodeData.Where(x => x.Key > strangleNode.Call.Strike).ToList();
            var higherPutNodeData = putNodeData.Where(x => x.Key > strangleNode.Put.Strike).ToList();

            for (int i = 0; i < higherNodeCount; i++)
            {
                StrangleListNode strangle = new StrangleListNode(higherCallNodeData[i].Value, higherPutNodeData[i].Value);

                strangle.Index = strangleNode.Index + 1;
                strangleNode.NextNode = strangle;
                strangle.PrevNode = strangleNode;

                strangleNode = strangleNode.NextNode;
            }

            strangleNode = strangleNode.GetNodebyIndex((Int16)currentIndex);

        }

        //private void AssignNeighbouringNodes(StrangleListNode strangleNode, UInt32 baseInstrumentToken, Instrument callOption, Instrument putOption)
        //{
        //    int? retrievalIndex = GetRetrievalIndex(strangleNode);
        //    if (retrievalIndex == null)
        //        return;

        //    DataLogic dl = new DataLogic();
        //    SortedList<Decimal, Instrument>[] NodeData = dl.RetrieveNextStrangleNodes(baseInstrumentToken, callOption.Expiry.Value, 
        //        callOption.Strike, putOption.Strike, retrievalIndex.Value);

        //    //Instrument currentInstrument = currentNode.Instrument;

        //    SortedList<Decimal, Instrument> callNodeData = NodeData[0];
        //    SortedList<Decimal, Instrument> putNodeData = NodeData[1];

        //    callNodeData.Add(callOption.Strike, callOption);
        //    putNodeData.Add(putOption.Strike, putOption);

        //    //InstrumentListNode tempNode = currentNode;

        //    int currentIndex = strangleNode.Index;
        //    int currentNodeIndex = Math.Min(callNodeData.IndexOfKey(callOption.Strike), putNodeData.IndexOfKey(putOption.Strike));



        //    StrangleListNode baseNode, firstOption = new StrangleListNode(callNodeData.Values[0], putNodeData.Values[0]);
        //    baseNode = firstOption;
        //    //byte index = baseNode.Index = Convert.ToByte(Math.Floor(Convert.ToDouble(NodeData.Values.Count / 2)) * -1);

        //    int index = currentIndex - currentNodeIndex;

        //    int nodeCount = Math.Min(callNodeData.Values.Count, putNodeData.Values.Count);

        //    for (byte i = 1; i < nodeCount; i++)
        //    {
        //        StrangleListNode strangle = new StrangleListNode(callNodeData.Values[i], putNodeData.Values[i]);

        //        baseNode.NextNode = strangle;
        //        baseNode.Index = index;
        //        strangle.PrevNode = baseNode;
        //        baseNode = strangle;
        //        index++;
        //    }
        //    baseNode.Index = index; //to assign index to the last node


        //    if (currentNodeIndex == 0)
        //    {
        //        firstOption.NextNode.PrevNode = strangleNode;
        //        strangleNode.NextNode = firstOption.NextNode;
        //    }
        //    else if (currentNodeIndex == callNodeData.Values.Count - 1)
        //    {
        //        baseNode.PrevNode.NextNode = strangleNode;
        //        strangleNode.PrevNode = baseNode.PrevNode;
        //    }
        //    else
        //    {
        //        while (baseNode.PrevNode != null)
        //        {
        //            if (baseNode.Index == currentIndex)
        //            {
        //                baseNode.Prices = strangleNode.Prices;
        //                strangleNode.PrevNode = baseNode.PrevNode;
        //                strangleNode.NextNode = baseNode.NextNode;
        //                break;
        //            }
        //            baseNode = baseNode.PrevNode;
        //        }
        //    }
        //}

        private void UpdateLastPriceOnAllNodes(ref InstrumentListNode currentNode, ref Tick[] ticks)
        {
            Instrument option;
            Tick optionTick;

            int currentNodeIndex = currentNode.Index;

            //go to the first node:
            while (currentNode.PrevNode != null)
            {
                currentNode = currentNode.PrevNode;
            }

            //Update all the price all the way till the end
            while (currentNode.NextNode != null)
            {
                option = currentNode.Instrument;

                optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == option.InstrumentToken);
                if (optionTick.LastPrice != 0)
                {
                    option.LastPrice = optionTick.LastPrice;
                    currentNode.Instrument = option;

                }
                currentNode = currentNode.NextNode;
            }

            //Come back to the current node
            while (currentNode.PrevNode != null)
            {
                if (currentNode.Index == currentNodeIndex)
                {
                    break;
                }
                currentNode = currentNode.PrevNode;
            }
        }


        private void LoadActiveData()
        {
            AlgoIndex algoIndex = AlgoIndex.StrangleWithConstantWidth;
            DataLogic dl = new DataLogic();
            DataSet activeStrangles = dl.RetrieveActiveStrangleData(algoIndex);
            DataRelation strangle_Token_Relation = activeStrangles.Relations.Add("Strangle_Token", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"] },
                new DataColumn[] { activeStrangles.Tables[1].Columns["StrategyId"] });

            foreach (DataRow strangleRow in activeStrangles.Tables[0].Rows)
            {
                StrangleListNode strangleTokenNode = null;
                foreach (DataRow strangleTokenRow in strangleRow.GetChildRows(strangle_Token_Relation))
                {
                    // string optionType = (string)strangleTokenRow["Type"];
                    Instrument call = new Instrument()
                    {
                        BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
                        InstrumentToken = Convert.ToUInt32(strangleTokenRow["CallToken"]),
                        InstrumentType = "CE",
                        Strike = (Decimal)strangleTokenRow["CallStrike"],
                        TradingSymbol = (string)strangleTokenRow["CallSymbol"]
                    };
                    Instrument put = new Instrument()
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

                    ///TODO: Each trade should be stored seperately so that prices can be stored sepertely. 
                    ///Other wise it will create a problem with below logic, as new average gets calculated using
                    ///last 2 prices, and retrival below is the average price.



                    List<Decimal[]> prices = new List<decimal[]>();

                    if ((decimal)strangleTokenRow["CallPrice"] != 0 && (decimal)strangleTokenRow["PutPrice"] != 0)
                        prices.Add(new Decimal[3] { (decimal)strangleTokenRow["CallPrice"], (decimal)strangleTokenRow["PutPrice"],
                            (decimal)strangleTokenRow["BaseInstrumentPrice"] });

                    if (strangleTokenNode == null)
                    {
                        strangleTokenNode = new StrangleListNode(call, put)
                        {
                            Index = (int)strangleTokenRow["InstrumentIndex"]
                        };
                        strangleTokenNode.DeltaThreshold = (double)strangleRow["StopLossPoints"];
                        strangleTokenNode.Prices = prices;
                    }
                    else
                    {
                        StrangleListNode newNode = new StrangleListNode(call, put)
                        {
                            Index = (int)strangleTokenRow["InstrumentIndex"]
                        };
                        newNode.Prices = prices;
                        newNode.DeltaThreshold = (double)strangleRow["StopLossPoints"];

                        strangleTokenNode.AttachNode(newNode);
                    }
                }
                int strategyId = (int)strangleRow["Id"];
                //InstrumentType instrumentType = (string)strangleRow["OptionType"] == "CE" ? InstrumentType.CE : InstrumentType.PE;


                StrangleListNode currentNode = strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]);


                StrangleLinkedList strangleLinkedList = new StrangleLinkedList(currentNode)
                {
                    CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
                    //MaxLossPoints = Math.Max((decimal)strangleRow["MaxLossPoints"], Math.Abs(currentNode.Prices.Last()) * 0.15m),
                    //MaxProfitPoints = Math.Max((decimal)strangleRow["MaxProfitPoints"], Math.Abs(currentNode.Prices.Last()) * 0.3m),
                    StopLossPoints = (double)strangleRow["StopLossPoints"],
                    NetPrice = (decimal)strangleRow["NetPrice"],
                    listID = strategyId,
                    BaseInstrumentToken = strangleTokenNode.Call.BaseInstrumentToken
                };

                ActiveStrangles.Add(strategyId, strangleLinkedList);
            }

                //strangle[strangleCount++] = new InstrumentLinkedList(
                //        strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]))
                //    {
                //        CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
                //        MaxLossPoints = (decimal)strangleRow["MaxLossPoints"],
                //        MaxProfitPoints = (decimal)strangleRow["MaxProfitPoints"],
                //        StopLossPoints = (decimal)strangleRow["StopLossPoints"]
                //    };
                //// int strangleID = (int)strangleTokenRow["StrangleId"];
                ////strangleNodeList.Add(strangleID, new SortedList<decimal, InstrumentListNode>(strangleTokenNode.Index, strangleTokenNode));

                //ActiveStrangles.Add(strangle);

        }

        /// <summary>
        /// Retrieval Index 
        ///     1) 0 -> Both previous and next nodes
        ///     2) 1 -> Only next nodes
        ///     3) -1 -> Only previous nodes
        /// </summary>
        /// <param name="strangleNode"></param>
        /// <returns></returns>
        private int? GetRetrievalIndex(StrangleListNode strangleNode)
        {
            int? retrievalIndex = 0; // retrieve both side - previous and next
            if (strangleNode.NextNode != null && strangleNode.PrevNode != null)
            {
                return null;
            }
            if (strangleNode.PrevNode == null)
            {
                retrievalIndex--;
            }
            if (strangleNode.NextNode == null)
            {
                retrievalIndex++;
            }
            return retrievalIndex;
        }
        /// <summary>
        /// Manages the value of put and call in the strangle
        /// </summary>
        /// <param name="bInst"></param>
        /// <param name="currentPE"></param>
        /// <param name="currentCE"></param>
        /// <param name="safetyWidth"></param>
        /// <param name="strangleId"></param>
        public void ManageStrangleWithConstantWidth(Instrument bInst, Instrument currentPE, Instrument currentCE,
            decimal safetyWidth = 10, int strangleId = 0, DateTime timeOfOrder = default(DateTime))
        {
            if (currentPE.LastPrice * currentCE.LastPrice != 0)
            {
                //If new strangle, place the order and update the data base. If old strangle monitor it.
                if (strangleId == 0)
                {
                    //Dictionary<string, Quote> keyValuePairs = ZObjects.kite.GetQuote(new string[] { Convert.ToString(currentCE.InstrumentToken),
                    //                                            Convert.ToString(currentPE.InstrumentToken), Convert.ToString(bInst.InstrumentToken) });

                    //decimal cePrice = keyValuePairs[Convert.ToString(currentCE.InstrumentToken)].Bids[0].Price;
                    //decimal pePrice = keyValuePairs[Convert.ToString(currentPE.InstrumentToken)].Bids[0].Price;
                    //decimal bPrice = keyValuePairs[Convert.ToString(bInst.InstrumentToken)].Bids[0].Price;
                    //decimal timeOfOrder = DateTime.Now;

                    ////TEMP -> First price
                    decimal pePrice = currentPE.LastPrice;
                    decimal cePrice = currentCE.LastPrice;
                    decimal bPrice = bInst.LastPrice;
                    ////put.Prices.Add(pePrice); //put.SellPrice = 100;
                    //call.Prices.Add(cePrice);  // call.SellPrice = 100;

                    ///Uncomment below for real time orders
                    cePrice = PlaceOrder(currentCE.TradingSymbol, false, cePrice);
                    pePrice = PlaceOrder(currentPE.TradingSymbol, false, pePrice);

                    // double callDelta = currentNode.Call.UpdateDelta(Convert.ToDouble(currentNode.Call.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
                    double callDelta = currentCE.UpdateDelta(Convert.ToDouble(currentCE.LastPrice), 0.1, timeOfOrder, Convert.ToDouble(bPrice));
                    callDelta = Math.Abs(callDelta);

                    double putDelta = currentPE.UpdateDelta(Convert.ToDouble(currentPE.LastPrice), 0.1, timeOfOrder, Convert.ToDouble(bPrice));
                    putDelta = Math.Abs(putDelta);

                    double stopLossPoints = Math.Max(callDelta, putDelta) + SAFETY_MARGIN;


                    //Update Database
                    DataLogic dl = new DataLogic();
                    strangleId = dl.StoreStrangleData(ceToken: currentCE.InstrumentToken, peToken: currentPE.InstrumentToken, cePrice: cePrice,
                       pePrice: pePrice, bInstPrice:bPrice, algoIndex: AlgoIndex.StrangleWithConstantWidth, safetyMargin:10, stopLossPoints: stopLossPoints);
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
