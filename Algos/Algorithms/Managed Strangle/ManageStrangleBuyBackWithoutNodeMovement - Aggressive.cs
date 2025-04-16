using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Algorithms.Utilities;
using GlobalLayer;
using KiteConnect;
using BrokerConnectWrapper;
using System.Data;

namespace Algos.TLogics
{
    public class ManageStrangleBuyBackWithoutNodeMovement_Aggressive : IObserver<Tick[]>
    { 
        public ManageStrangleBuyBackWithoutNodeMovement_Aggressive()
        {
            LoadActiveData();
        }
        Dictionary<int, InstrumentLinkedList[]> ActiveStrangles = new Dictionary<int, InstrumentLinkedList[]>();

        ///TODO:This needs to be input from the front end
        //decimal SafetyBand = 5;


        //todo: check if position is open first time for the current node. Functionality of load active data is working fine.
        private void ReviewStrangle(InstrumentLinkedList optionList, Tick[] ticks, InstrumentLinkedList backupOptionList)
        {
            InstrumentListNode strangleNode = optionList.Current;
            Instrument option = strangleNode.Instrument;
            //Instrument putOption = optionNodes[(int)InstrumentType.PE].Instrument;

            Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == option.InstrumentToken);
            if (optionTick.LastPrice != 0)
            {
                option.LastPrice = optionTick.LastPrice;
                strangleNode.Instrument = option;
            }

            if (strangleNode.Prices.Count > 0)
            {
                if (strangleNode.Prices.First() == 0)
                {
                    strangleNode.Prices.RemoveAt(0);
                }
                
                if (strangleNode.CurrentPosition == PositionStatus.Closed &&
                    CheckForReSale(strangleNode, Math.Max(Math.Abs(strangleNode.Prices.First()) * 0.20m, optionList.MaxLossPoints) * 0.5m))
                {
                    strangleNode.CurrentPosition = PositionStatus.Open;
                    strangleNode.Prices.Add(PlaceOrder(option.TradingSymbol, buyOrder: false, option.LastPrice));

                    decimal lastOptionPrice = strangleNode.Prices.Last();

                    uint token = option.InstrumentToken;
                    option = strangleNode.Instrument;

                        DataLogic dl = new DataLogic();
                    dl.UpdateListData(optionList.listID, option.InstrumentToken, strangleNode.Index, 0, option.InstrumentType,
                       0, lastOptionPrice, 0, AlgoIndex.BBStrangleWithoutMovement_Aggressive);

                    //Increase quantity back to full amount
                    InstrumentListNode backupStrangleNode = backupOptionList.Current;
                    Instrument backUpOption = backupStrangleNode.Instrument;
                    PlaceBackUpOrder(backUpOption.TradingSymbol, buyOrder: false, backUpOption.LastPrice);

                }
                else if (strangleNode.CurrentPosition == PositionStatus.Open && (
                    (option.LastPrice > strangleNode.Prices.First() + Math.Max(strangleNode.Prices.First() * 0.20m, optionList.MaxLossPoints) )))
                {
                    strangleNode.CurrentPosition = PositionStatus.Closed;
                    //put P&L impact on node level on the linked list
                    strangleNode.Prices.Add(PlaceOrder(option.TradingSymbol, buyOrder: true, option.LastPrice)); //buy prices are negative and sell are positive. as sign denotes cashflow direction
                    

                    decimal lastOptionPrice = strangleNode.Prices.Last();

                    uint token = option.InstrumentToken;
                    option = strangleNode.Instrument;

                    DataLogic dl = new DataLogic();
                    dl.UpdateListData(optionList.listID, option.InstrumentToken, strangleNode.Index, 0, option.InstrumentType,
                       0, lastOptionPrice, 0, AlgoIndex.BBStrangleWithoutMovement_Aggressive);


                    //Reduce quantity and book some profit for risk mitigation if market comes back 
                    InstrumentListNode backupStrangleNode = backupOptionList.Current;
                    Instrument backUpOption = backupStrangleNode.Instrument;
                    PlaceBackUpOrder(backUpOption.TradingSymbol, buyOrder: true, backUpOption.LastPrice);
                }
                optionList.Current = strangleNode;
            }
        }
        

        //call this method only if current index is not equal to 0
        private bool CheckForReSale(InstrumentListNode optionNode, decimal safetyBand)
        {
            Instrument currentOption = optionNode.Instrument;
            decimal firstSellingPrice = Math.Abs(optionNode.Prices.First());
            bool buyback = false;
            if (currentOption.LastPrice <= firstSellingPrice + safetyBand)
            {
                buyback = true;
            }
            return buyback;
        }
        //private bool CheckForReSale(InstrumentListNode optionNode, decimal safetyBand)
        //{
        //    Instrument currentOption = optionNode.Instrument;
        //    decimal lastSellingPrice = Math.Abs(optionNode.Prices.Last());
        //    bool buyback = false;
        //    if (currentOption.LastPrice <= lastSellingPrice - safetyBand)
        //    {
        //        buyback = true;
        //    }
        //    return buyback;
        //}



        //make sure ref is working with struct . else make it class
        public virtual void OnNext(Tick[] ticks)
        {
            lock (ActiveStrangles)
            {
                foreach (KeyValuePair<int, InstrumentLinkedList[]> keyValuePair in ActiveStrangles)
                {
                    ReviewStrangle(keyValuePair.Value[0], ticks, keyValuePair.Value[1]);
                    ReviewStrangle(keyValuePair.Value[1], ticks, keyValuePair.Value[0]);
                }
            }
        }

        private void LoadActiveData()
        {
            //InstrumentLinkedList[] strangleList = new InstrumentLinkedList[2] {
            //    new InstrumentLinkedList(),
            //    new InstrumentLinkedList()
            //};
            AlgoIndex algoIndex = AlgoIndex.BBStrangleWithoutMovement_Aggressive;
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
                       // strangleTokenNode.CurrentPosition = PositionStatus.Open;
                    }
                    else
                    {
                        InstrumentListNode newNode = new InstrumentListNode(option)
                        {
                            Index = (int)strangleTokenRow["InstrumentIndex"]
                        };
                        newNode.Prices = prices;
                       // newNode.CurrentPosition = PositionStatus.Open;
                        strangleTokenNode.AttachNode(newNode);
                    }

                }

                int strategyId = (int)strangleRow["Id"];
                InstrumentType instrumentType = (string)strangleRow["OptionType"] == "CE" ? InstrumentType.CE : InstrumentType.PE;

                InstrumentLinkedList instrumentLinkedList = new InstrumentLinkedList(
                        strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]))
                {
                    CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
                    MaxLossPoints = (decimal)strangleRow["MaxLossPoints"],
                    MaxProfitPoints = (decimal)strangleRow["MaxProfitPoints"],
                    StopLossPoints = (double)strangleRow["StopLossPoints"],
                    NetPrice = (decimal)strangleRow["NetPrice"],
                    listID = strategyId
                };
                instrumentLinkedList.Current.CurrentPosition = PositionStatus.Open;
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
                       put.Prices.Last(), AlgoIndex.BBStrangleWithoutMovement_Aggressive, ceMaxProfitPoints, ceMaxLossPoints, peMaxProfitPoints, peMaxLossPoints,
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
                                      buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, 300, Product: Constants.PRODUCT_MIS,
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
        public virtual void OnCompleted()
        {
        }

        private void PlaceBackUpOrder(string tradingSymbol, bool buyOrder, decimal currentPrice)
        {
            //Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
            //                         buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, 120, Product: Constants.PRODUCT_MIS,
            //                         OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            //Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
            //                        Constants.TRANSACTION_TYPE_SELL, 200, Product: Constants.PRODUCT_MIS,
            //                        OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);



            //string orderId = "0";
            //decimal averagePrice = 0;
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
            //if (averagePrice == 0)
            //    averagePrice = currentPrice;

           
            //ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
            //                        Constants.TRANSACTION_TYPE_BUY, 200, Product: Constants.PRODUCT_MIS,
            //                        OrderType: Constants.ORDER_TYPE_LIMIT, Validity: Constants.VALIDITY_DAY, TriggerPrice: averagePrice + 10);
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
