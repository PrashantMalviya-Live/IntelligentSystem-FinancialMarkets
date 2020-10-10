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
    public class ManageStrangleBuyBack : IObserver<Tick[]>
    { 
        public ManageStrangleBuyBack()
        {
            LoadActiveData();
        }
        Dictionary<int, InstrumentLinkedList[]> ActiveStrangles = new Dictionary<int, InstrumentLinkedList[]>();

        List<UInt32> assignedTokens;

        public IDisposable UnsubscriptionToken;


        Instrument _bInst;
        ///TODO:This needs to be input from the front end
        decimal SafetyBand = 20;

        //public virtual void Subscribe(Publisher publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}
        //public virtual void Subscribe(Ticker publisher)
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

        private void ReviewStrangle(InstrumentLinkedList optionList, Tick[] ticks)
        {
            InstrumentListNode optionNode = optionList.Current;
            Instrument currentOption = optionNode.Instrument;


            /// ToDO: Remove this on the live environment as this will come with the tick data
            //if (currentOption.LastPrice == 0)
            //{
            //    DataLogic dl = new DataLogic();
            //    currentOption.LastPrice = dl.RetrieveLastPrice(currentOption.InstrumentToken);

            //    optionList.Current.Instrument = currentOption;
            //}

            _bInst.InstrumentToken = currentOption.BaseInstrumentToken;

            Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == _bInst.InstrumentToken);
            if (baseInstrumentTick.LastPrice != 0)
            {
                _bInst.LastPrice = baseInstrumentTick.LastPrice;
            }



            Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == currentOption.InstrumentToken);
            if (optionTick.LastPrice != 0)
            {
                currentOption.LastPrice = optionTick.LastPrice;
                optionList.Current.Instrument = currentOption;
            }


            //Pulling next nodes from the begining and keeping it up to date from the ticks. This way next nodes will be available when needed, as Tick table takes time.
            //Doing it on Async way
            if (optionNode.NextNode == null || optionNode.PrevNode == null)
                AssignNextNodes(optionNode, _bInst.InstrumentToken, currentOption.InstrumentType, currentOption.Strike, currentOption.Expiry.Value);


            /////Relatively slower task kept after asnyc method call
            /////TODO: REMOVE DATE TIME PARAMETER FROM THE UPDATE DELTA METHOD IN CASE OF LIVE RUNNING. THIS WAS DONE TO BACKTEST
            //double optionDelta = currentOption.UpdateDelta(Convert.ToDouble(_bInst.LastPrice), 0.1, ticks[0].Timestamp);

            //if (nodelistformed && nodelistformed.IsCompleted)
            //{
            // optionNode = nextNodes.Result;
            UpdateLastPriceOnAllNodes(ref optionNode, ref ticks);
            //optionList.Current = optionNode;
            //}

            //SafetyBand = optionList.MaxLossPoints * 0.5m;



            //Check all the previously held loss options for their selling price
            InstrumentListNode subsequentNode = null;//CheckForBuyBack(optionNode, SafetyBand);
            if ((subsequentNode != null) ||
             ((optionNode.Prices.Count > 0) && (optionNode.Prices.Last() > optionList.MaxProfitPoints) &&
               (currentOption.LastPrice < optionNode.Prices.Last() - optionList.MaxProfitPoints)
               || currentOption.LastPrice > optionNode.Prices.Last() + optionList.MaxLossPoints))
            
            { 
                //put P&L impact on node level on the linked list
                //decimal previousNodeAvgPrice = PlaceOrder(currentOption.TradingSymbol, buyOrder: true);
                optionNode.Prices.Add(PlaceOrder(currentOption.TradingSymbol, buyOrder: true, currentOption.LastPrice)); //buy prices are negative and sell are positive. as sign denotes cashflow direction

                decimal previousNodeBuyPrice = optionNode.Prices.Last();  //optionNode.Prices.Skip(optionNode.Prices.Count - 2).Sum();

                uint previousInstrumentToken = currentOption.InstrumentToken;
                int previousInstrumentIndex = optionNode.Index;
                //Logic to determine the next node and move there
                if (subsequentNode != null)
                {
                    optionNode = subsequentNode;
                }
                else
                {
                    optionNode = MoveToSubsequentNode(optionNode, currentOption.LastPrice, 
                        optionList.MaxProfitPoints, 
                        optionList.MaxLossPoints, 
                        ticks);
                }

                currentOption = optionNode.Instrument;
                optionList.Current = optionNode;

                if (optionNode.Prices.Count > 0 && optionNode.Prices.First() == 0)
                {
                    optionNode.Prices.RemoveAt(0);
                }
                optionNode.Prices.Add(PlaceOrder(currentOption.TradingSymbol, buyOrder: false, currentOption.LastPrice));
                decimal currentNodeSellPrice = optionNode.Prices.Last(); //buy prices are negative and sell are positive. as sign denotes cashflow direction

                //Do we need current instrument index , when current instrument is already present in the linked list?
                //It can see the index of current instrument using list.current.index
                optionList.CurrentInstrumentIndex = optionNode.Index;


                optionList.MaxProfitPoints = Math.Max(optionList.MaxProfitPoints, Math.Abs(optionNode.Prices.Last()) * 0.3m);
                optionList.MaxLossPoints = Math.Max(optionList.MaxLossPoints, Math.Abs(optionNode.Prices.Last()) * 0.15m);


                //make next symbol as next to current node. Also update PL data in linklist main
                //also update currnetindex in the linklist main
                DataLogic dl = new DataLogic();
                dl.UpdateListData(optionList.listID, currentOption.InstrumentToken, optionNode.Index, previousInstrumentIndex, currentOption.InstrumentType,
                   previousInstrumentToken, currentNodeSellPrice, previousNodeBuyPrice, AlgoIndex.BBStrangle);
            }
            //}
            //catch (Exception exp)
            //{
            //    //throw exp;
            //}
        }
        

        //call this method only if current index is not equal to 0
        private InstrumentListNode CheckForBuyBack(InstrumentListNode optionNode, decimal safetyBand)
        {
            Instrument currentOption = optionNode.Instrument;
            decimal lastSellingPrice  = 0;
            bool nodeFound = false;
            InstrumentListNode subsequentNode = optionNode;

            ///TODO: Change this to Case statement.
            if (currentOption.InstrumentType.Trim(' ') == "CE" && optionNode.Index > 0)
            {
                //go to index 0
                while (subsequentNode.PrevNode != null)
                {
                    subsequentNode = subsequentNode.PrevNode;
                    if (subsequentNode.Index == 0)
                    {
                        break;
                    }
                }
            
                while (subsequentNode.NextNode != null)
                {
                    if (subsequentNode.Prices.Count > 1)
                    {
                        lastSellingPrice = Math.Abs(subsequentNode.Prices.Last());

                        if ((lastSellingPrice - safetyBand >= subsequentNode.Instrument.LastPrice) && (optionNode.Index != subsequentNode.Index))
                        {
                            nodeFound = true;
                            break;
                        }
                    }
                    subsequentNode = subsequentNode.NextNode;
                }
            }
            if (currentOption.InstrumentType.Trim(' ') == "PE" && optionNode.Index < 0)
            {
                //go to index 0
                while (subsequentNode.NextNode != null)
                {
                    subsequentNode = subsequentNode.NextNode;
                    if (subsequentNode.Index == 0)
                    {
                        break;
                    }
                }

                while (subsequentNode.PrevNode != null)
                {
                    if (subsequentNode.Prices.Count > 1)
                    {
                        lastSellingPrice = Math.Abs(subsequentNode.Prices.Last());

                        if ((lastSellingPrice - safetyBand >= subsequentNode.Instrument.LastPrice) && (optionNode.Index != subsequentNode.Index))
                        {
                            nodeFound = true;
                            break;
                        }
                    }
                    subsequentNode = subsequentNode.PrevNode;
                }
            }
            return nodeFound?subsequentNode:null;   
        }
        private InstrumentListNode MoveToSubsequentNode(InstrumentListNode optionNode, decimal optionPrice, decimal maxProfitPoints, decimal maxLossPoints, Tick[] ticks)
        {
            bool nodeFound = false;
            Instrument currentOption = optionNode.Instrument;
            decimal initialSellingPrice = optionNode.Prices.ElementAtOrDefault(optionNode.Prices.Count - 2);
            ///TODO: Change this to Case statement.
            if (currentOption.InstrumentType.Trim(' ') == "CE")
            {
                if (optionPrice > initialSellingPrice + maxLossPoints)
                {
                    while (optionNode.NextNode != null)
                    {
                        optionNode = optionNode.NextNode;

                        //No condition modelled to determine the next node
                        //if (optionNode.Instrument.LastPrice <= upperPrice * 0.8m) //90% of upper delta
                        //{
                        nodeFound = true;
                        break;
                        //}
                    }

                }
                else
                {
                    while (optionNode.PrevNode != null)
                    {
                        optionNode = optionNode.PrevNode;
                        //No condition modelled to determine the next node
                        //if (optionNode.Instrument.LastPrice >= lowerPrice * 1.2m) //110% of lower delta
                        //{
                        nodeFound = true;
                        break;
                        //}
                    }

                }
            }
            else
            {
                if (optionPrice > initialSellingPrice + maxLossPoints)
                {
                    while (optionNode.PrevNode != null)
                    {
                        optionNode = optionNode.PrevNode;
                        //No condition modelled to determine the next node
                        //if (optionNode.Instrument.LastPrice <= upperPrice * 0.8m) //90% of upper delta
                        //{
                        nodeFound = true;
                        break;
                        //}
                    }
                }
                else
                {
                    while (optionNode.NextNode != null)
                    {
                        optionNode = optionNode.NextNode;
                        //No condition modelled to determine the next node
                        //if (optionNode.Instrument.LastPrice >= lowerPrice * 1.2m) //110% of lower delta
                        //{
                        nodeFound = true;
                        break;
                        //}
                    }

                }
            }

            if (!nodeFound)
            {
                currentOption = optionNode.Instrument;
                AssignNextNodes(optionNode, currentOption.BaseInstrumentToken, currentOption.InstrumentType, currentOption.Strike, currentOption.Expiry.Value);
                optionNode = MoveToSubsequentNode(optionNode, optionPrice, maxProfitPoints, maxLossPoints, ticks);
            }

            return optionNode;

        }


        //make sure ref is working with struct . else make it class
        public virtual void OnNext(Tick[] ticks)
        {
            //for (int i = 0; i < ActiveStrangles.Count; i++)
            //{
            //    await ReviewStrangle(ActiveStrangles[i][0], ticks);
            //    await ReviewStrangle(ActiveStrangles[i][1], ticks);
            //}
            lock (ActiveStrangles)
            {
                foreach (KeyValuePair<int, InstrumentLinkedList[]> keyValuePair in ActiveStrangles)
                {
                    ReviewStrangle(keyValuePair.Value[0], ticks);
                    ReviewStrangle(keyValuePair.Value[1], ticks);
                    //await ReviewStrangle(ActiveStrangles[i][1], ticks);
                }
            }
        }

        private void LoadActiveData()
        {
            //InstrumentLinkedList[] strangleList = new InstrumentLinkedList[2] {
            //    new InstrumentLinkedList(),
            //    new InstrumentLinkedList()
            //};
            AlgoIndex algoIndex = AlgoIndex.BBStrangle;
            DataLogic dl = new DataLogic();
            DataSet activeStrangles = dl.RetrieveActiveData(algoIndex);
            DataRelation strangle_Token_Relation = activeStrangles.Relations.Add("Strangle_Token", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"], activeStrangles.Tables[0].Columns["OptionType"] },
                new DataColumn[] { activeStrangles.Tables[1].Columns["StrategyId"], activeStrangles.Tables[1].Columns["Type"] });

            foreach (DataRow strangleRow in activeStrangles.Tables[0].Rows)
            {
                InstrumentListNode strangleTokenNode = null;
                foreach (DataRow strangleTokenRow in strangleRow.GetChildRows(strangle_Token_Relation))
                {
                    Instrument option = new Instrument()
                    {
                        BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
                        InstrumentToken = Convert.ToUInt32(strangleTokenRow["InstrumentToken"]),
                        InstrumentType = (string)strangleTokenRow["Type"],
                        Strike = (Decimal)strangleTokenRow["StrikePrice"],
                        TradingSymbol = (string)strangleTokenRow["TradingSymbol"]
                    };
                    if (strangleTokenRow["Expiry"] != DBNull.Value)
                        option.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);

                    ///TODO: Each trade should be stored seperately so that prices can be stored sepertely. 
                    ///Other wise it will create a problem with below logic, as new average gets calculated using
                    ///last 2 prices, and retrival below is the average price.
                    List<Decimal> prices = new List<decimal>();
                    if ((decimal)strangleTokenRow["LastSellingPrice"] != 0)
                        prices.Add((decimal)strangleTokenRow["LastSellingPrice"]);

                    if (strangleTokenNode == null)
                    {
                        strangleTokenNode = new InstrumentListNode(option)
                        {
                            Index = (int)strangleTokenRow["InstrumentIndex"]
                        };
                        strangleTokenNode.Prices = prices;
                    }
                    else
                    {
                        InstrumentListNode newNode = new InstrumentListNode(option)
                        {
                            Index = (int)strangleTokenRow["InstrumentIndex"]
                        };
                        newNode.Prices = prices;
                        strangleTokenNode.AttachNode(newNode);
                    }
                }
                int strategyId = (int)strangleRow["Id"];
                InstrumentType instrumentType = (string)strangleRow["OptionType"] == "CE" ? InstrumentType.CE : InstrumentType.PE;


                InstrumentListNode currentNode = strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]);

                InstrumentLinkedList instrumentLinkedList = new InstrumentLinkedList(currentNode)
                {
                    CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
                    MaxLossPoints = Math.Max( (decimal)strangleRow["MaxLossPoints"], Math.Abs(currentNode.Prices.Last())* 0.15m),
                    MaxProfitPoints = Math.Max((decimal)strangleRow["MaxProfitPoints"], Math.Abs(currentNode.Prices.Last()) * 0.3m),
                    StopLossPoints = (double)strangleRow["StopLossPoints"],
                    NetPrice = (decimal)strangleRow["NetPrice"],
                    listID = strategyId
                };

                if (ActiveStrangles.ContainsKey(strategyId))
                {
                    ActiveStrangles[strategyId][(int)instrumentType] = instrumentLinkedList;
                }
                else
                {
                    InstrumentLinkedList[] strangle = new InstrumentLinkedList[2];
                    strangle[(int)instrumentType] = instrumentLinkedList;

                    ActiveStrangles.Add(strategyId, strangle);
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

        }

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

        public virtual void OnError(Exception ex)
        {
            ///TODO: Log the error. Also handle the error.
        }

        /// <summary>
        /// Manages the value of put and call in the strangle
        /// </summary>
        /// <param name="bInst"></param>
        /// <param name="currentPE"></param>
        /// <param name="currentCE"></param>
        /// <param name="peLowerValue"></param>
        /// <param name="peUpperValue"></param>
        /// <param name="ceLowerValue"></param>
        /// <param name="ceUpperValue"></param>
        /// <param name="stopLossPoints"></param>
        /// <param name="strangleId"></param>
        public void ManageStrangleBB(Instrument bInst, Instrument currentPE, Instrument currentCE,
            decimal peMaxProfitPoints = 0, decimal peMaxLossPoints = 0, decimal ceMaxProfitPoints = 0,
            decimal ceMaxLossPoints = 0, double stopLossPoints = 0,
            decimal peMaxProfitPercent = 0, decimal peMaxLossPercent = 0, decimal ceMaxProfitPercent = 0,
            decimal ceMaxLossPercent = 0, int strangleId = 0)
        {
            if (currentPE.LastPrice * currentCE.LastPrice != 0)
            {
                ///TODO: placeOrder lowerPutValue and upperCallValue
                /// Get Executed values on to lowerPutValue and upperCallValue

                //Two seperate linked list are maintained with their incides on the linked list.
                InstrumentListNode put = new InstrumentListNode(currentPE);
                InstrumentListNode call = new InstrumentListNode(currentCE);

                //If new strangle, place the order and update the data base. If old strangle monitor it.
                if (strangleId == 0)
                {
                  

                    Dictionary<string, Quote> keyValuePairs = ZObjects.kite.GetQuote(new string[] { Convert.ToString(currentCE.InstrumentToken),
                                                                Convert.ToString(currentPE.InstrumentToken) });

                    decimal cePrice = keyValuePairs[Convert.ToString(currentCE.InstrumentToken)].Bids[0].Price;
                    decimal pePrice = keyValuePairs[Convert.ToString(currentPE.InstrumentToken)].Bids[0].Price;

                    ////TEMP -> First price
                    //put.Prices.Add(currentPE.LastPrice); //put.SellPrice = 100;
                    //call.Prices.Add(currentCE.LastPrice);  // call.SellPrice = 100;

                    ///Uncomment below for real time orders
                    put.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false, pePrice));
                    call.Prices.Add(PlaceOrder(currentCE.TradingSymbol, false, cePrice));

                    //Update Database
                    DataLogic dl = new DataLogic();
                    strangleId = dl.StoreStrangleData(currentCE.InstrumentToken, currentPE.InstrumentToken, call.Prices.Last(),
                       put.Prices.Last(), AlgoIndex.BBStrangleWithoutMovement, ceMaxProfitPoints, ceMaxLossPoints, peMaxProfitPoints, peMaxLossPoints,
                        ceMaxProfitPercent, ceMaxLossPercent, peMaxProfitPercent, peMaxLossPercent,
                       stopLossPoints = 0);
                }

                //Calls = new InstrumentLinkedList(call);
                //Puts = new InstrumentLinkedList(put);

                //Calls.MaxProfitPoints = ceMaxProfitPoints;
                //Calls.MaxLossPoints = ceMaxLossPoints;

                //Puts.MaxProfitPoints = peMaxProfitPoints;
                //Puts.MaxLossPoints = peMaxLossPoints;

                //Calls.listID = Puts.listID = strangleId;

                //_bInst = bInst;
            }
        }

        bool AssignNextNodesWithDelta(InstrumentListNode currentNode, UInt32 baseInstrumentToken, string instrumentType,
            decimal currentStrikePrice, DateTime expiry, DateTime? tickTimeStamp)
        {
            DataLogic dl = new DataLogic();
            SortedList<Decimal, Instrument> NodeData = dl.RetrieveNextNodes(baseInstrumentToken, instrumentType,
                currentStrikePrice, expiry, currentNode.Index);

            Instrument currentInstrument = currentNode.Instrument;

            NodeData.Add(currentInstrument.Strike, currentInstrument);

            //InstrumentListNode tempNode = currentNode;

            int currentIndex = currentNode.Index;
            int currentNodeIndex = NodeData.IndexOfKey(currentInstrument.Strike);



            InstrumentListNode baseNode, firstOption = new InstrumentListNode(NodeData.Values[0]);
            baseNode = firstOption;
            //byte index = baseNode.Index = Convert.ToByte(Math.Floor(Convert.ToDouble(NodeData.Values.Count / 2)) * -1);

            int index = currentIndex - currentNodeIndex;


            for (byte i = 1; i < NodeData.Values.Count; i++)
            {
                InstrumentListNode option = new InstrumentListNode(NodeData.Values[i]);

                baseNode.NextNode = option;
                baseNode.Index = index;
                option.PrevNode = baseNode;
                baseNode = option;
                index++;
            }
            baseNode.Index = index; //to assign index to the last node

            if (currentNodeIndex == 0)
            {
                firstOption.NextNode.PrevNode = currentNode;
                currentNode.NextNode = firstOption.NextNode;
            }
            else if (currentNodeIndex == NodeData.Values.Count - 1)
            {
                baseNode.PrevNode.NextNode = currentNode;
                currentNode.PrevNode = baseNode.PrevNode;
            }
            else
            {
                while (baseNode.PrevNode != null)
                {
                    if (baseNode.Index == currentIndex)
                    {
                        currentNode.PrevNode = baseNode.PrevNode;
                        currentNode.NextNode = baseNode.NextNode;
                        break;
                    }
                    baseNode = baseNode.PrevNode;
                }
            }

            return true;
        }
        /// <summary>
        /// Pulls nodes data from database on both sides
        /// </summary>
        /// <param name="currentNode"></param>
        /// <param name="baseInstrumentToken"></param>
        /// <param name="instrumentType"></param>
        /// <param name="currentStrikePrice"></param>
        /// <param name="expiry"></param>
        /// <param name="updownboth"></param>
        /// <returns></returns>
        bool AssignNextNodes(InstrumentListNode currentNode, UInt32 baseInstrumentToken, string instrumentType,
        decimal currentStrikePrice, DateTime expiry)
        {
            DataLogic dl = new DataLogic();
            SortedList<Decimal, Instrument> NodeData = dl.RetrieveNextNodes(baseInstrumentToken, instrumentType,
                currentStrikePrice, expiry, currentNode.Index);

            Instrument currentInstrument = currentNode.Instrument;

            NodeData.Add(currentInstrument.Strike, currentInstrument);

            //InstrumentListNode tempNode = currentNode;

            int currentIndex = currentNode.Index;
            int currentNodeIndex = NodeData.IndexOfKey(currentInstrument.Strike);



            InstrumentListNode baseNode, firstOption = new InstrumentListNode(NodeData.Values[0]);
            baseNode = firstOption;
            //byte index = baseNode.Index = Convert.ToByte(Math.Floor(Convert.ToDouble(NodeData.Values.Count / 2)) * -1);

            int index = currentIndex - currentNodeIndex;


            for (byte i = 1; i < NodeData.Values.Count; i++)
            {
                InstrumentListNode option = new InstrumentListNode(NodeData.Values[i]);

                baseNode.NextNode = option;
                baseNode.Index = index;
                option.PrevNode = baseNode;
                baseNode = option;
                index++;
            }
            baseNode.Index = index; //to assign index to the last node


            if (currentNodeIndex == 0)
            {
                firstOption.NextNode.PrevNode = currentNode;
                currentNode.NextNode = firstOption.NextNode;
            }
            else if (currentNodeIndex == NodeData.Values.Count - 1)
            {
                baseNode.PrevNode.NextNode = currentNode;
                currentNode.PrevNode = baseNode.PrevNode;
            }
            else
            {
                while (baseNode.PrevNode != null)
                {
                    if (baseNode.Index == currentIndex)
                    {
                        baseNode.Prices = currentNode.Prices;
                        currentNode.PrevNode = baseNode.PrevNode;
                        currentNode.NextNode = baseNode.NextNode;
                        break;
                    }
                    baseNode = baseNode.PrevNode;
                }
            }

            return true;

        }
        
        private decimal PlaceOrder(string tradingSymbol, bool buyOrder, decimal currentPrice)
        {
            Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
                                      buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, 150, Product: Constants.PRODUCT_MIS,
                                      OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            string orderId = "0";
            decimal averagePrice = 0;
            if (orderStatus["data"]["order_id"] != null)
            {
                orderId = orderStatus["data"]["order_id"];
            }
            if (orderId != "0")
            {
                System.Threading.Thread.Sleep(200);
                List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
                averagePrice = orderInfo[orderInfo.Count - 1].AveragePrice;
            }
            if (averagePrice == 0)
                averagePrice = buyOrder ? currentPrice + 1 : currentPrice - 1;
            return buyOrder ? averagePrice * -1 : averagePrice;
        }

        //private decimal PlaceOrder(string Symbol, bool buyOrder, InstrumentLinkedList optionList)
        //{
        //    //temp
        //    if (optionList.Current.Instrument.LastPrice == 0)
        //    {
        //        return optionList.Current.Prices.Last();
        //    }
        //    else
        //    {
        //        return optionList.Current.Instrument.LastPrice;
        //    }

        //    //Dictionary<string, dynamic> orderStatus;

        //    //orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, Symbol,
        //    //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_BUY, 75, Product: Constants.PRODUCT_MIS,
        //    //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

        //    //string orderId = orderStatus["data"]["order_id"];
        //    //List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
        //    //return orderInfo[orderInfo.Count - 1].AveragePrice;
        //}
        //private decimal PlaceOrder(string Symbol, bool buyOrder, decimal price)
        //{
        //    return price;

        //    //Dictionary<string, dynamic> orderStatus;

        //    //orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, Symbol,
        //    //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_BUY, 75, Product: Constants.PRODUCT_MIS,
        //    //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

        //    //string orderId = orderStatus["data"]["order_id"];
        //    //List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
        //    //return orderInfo[orderInfo.Count - 1].AveragePrice;
        //}
    }
}
