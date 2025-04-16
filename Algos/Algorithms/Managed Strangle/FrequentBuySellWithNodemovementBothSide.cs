using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Algorithms.Utilities;
using GlobalLayer;
using KiteConnect;
using BrokerConnectWrapper;
////using Pub_Sub;
//using MarketDataTest;
using System.Data;

namespace Algos.TLogics
{
    public class FrequentBuySellWithNodemovementBothSide : IObserver<Tick[]>
    {
        //SortedList<Decimal, InstrumentListNode> ceOptions;
        //SortedList<Decimal, InstrumentListNode> peOptions;
        //Dictionary<int, InstrumentLinkedList> ActiveStrangles = new Dictionary<int, InstrumentLinkedList>();

        InstrumentListNode[] Strangle = new InstrumentListNode[2];

        List<UInt32> assignedTokens;

        public IDisposable UnsubscriptionToken;

        public decimal MaxProfitPoints;
        public decimal MaxLossPoints;
        UInt32 _bInstToken;
        decimal SafetyBand = 10;
        int listID = 0;
        bool profitBooked = false;
        decimal totalProfit = 0.0m;
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
        //public virtual void Subscribe(Ticker publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);

        //    LoadActiveData();
        //}

        public virtual void Unsubscribe()
        {
            UnsubscriptionToken.Dispose();
        }

        public virtual void OnCompleted()
        {
        }

        private void ReviewStrangle(Tick[] ticks)
        {
            InstrumentListNode ceNode = Strangle[(int)InstrumentType.CE];
            InstrumentListNode peNode = Strangle[(int)InstrumentType.PE];

            Instrument ce = ceNode.Instrument;
            Instrument pe = peNode.Instrument;

            /// ToDO: Remove this on the live environment as this will come with the tick data
            //-----------Start--------------
            //DataLogic dl = new DataLogic();
            //if (ce.LastPrice == 0 || pe.LastPrice == 0)
            //{
            //    ce.LastPrice = dl.RetrieveLastPrice(ce.InstrumentToken, ticks[0].Timestamp);
            //    pe.LastPrice = dl.RetrieveLastPrice(pe.InstrumentToken, ticks[0].Timestamp);
            //}
            //-----------END -------------

            //_bInst.InstrumentToken = ce.BaseInstrumentToken;

            Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == _bInstToken);
            //if (baseInstrumentTick.LastPrice != 0)
            //{
            //    _bInst.LastPrice = baseInstrumentTick.LastPrice;
            //}


            Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == ce.InstrumentToken);
            if (optionTick.LastPrice != 0)
            {
                ce.LastPrice = optionTick.LastPrice;
                Strangle[(int)InstrumentType.CE].Instrument = ce;
            }
            optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == pe.InstrumentToken);
            if (optionTick.LastPrice != 0)
            {
                pe.LastPrice = optionTick.LastPrice;
                Strangle[(int)InstrumentType.PE].Instrument = pe;
            }

            if (ceNode.NextNode == null || ceNode.PrevNode == null)
                AssignNextNodes(ceNode, _bInstToken, ce.InstrumentType, ce.Strike, ce.Expiry.Value);
            if (peNode.NextNode == null || peNode.PrevNode == null)
                AssignNextNodes(peNode, _bInstToken, pe.InstrumentType, pe.Strike, pe.Expiry.Value);

            UpdateLastPriceOnAllNodes(ref ceNode, ref ticks);
            UpdateLastPriceOnAllNodes(ref peNode, ref ticks);

            decimal orderAvgPrice = 0;

            if (ce.LastPrice != 0 && pe.LastPrice != 0)
            {
                if (ceNode.CurrentPosition == PositionStatus.Open && peNode.CurrentPosition == PositionStatus.Open)
                {
                    if (profitBooked)
                    {
                        decimal ceDelta = ceNode.Prices.Last() - ce.LastPrice;
                        decimal peDelta = peNode.Prices.Last() - pe.LastPrice;

                        if (totalProfit + ceDelta + peDelta > MaxProfitPoints)
                        {
                            profitBooked = false;

                            orderAvgPrice = PlaceOrder(ceNode, true, ce.LastPrice);
                            ce.LastPrice = orderAvgPrice;
                            ceNode.Instrument = ce;

                            orderAvgPrice = PlaceOrder(peNode, true, pe.LastPrice);
                            pe.LastPrice = orderAvgPrice;
                            peNode.Instrument = pe;

                            Strangle[(int)InstrumentType.CE] = ceNode;
                            Strangle[(int)InstrumentType.PE] = peNode;

                            ceNode.CurrentPosition = PositionStatus.Closed;
                            peNode.CurrentPosition = PositionStatus.Closed;
                        }
                        else if ((ce.LastPrice > Math.Abs(ceNode.Prices.Last()) + MaxLossPoints) && ceNode.Index < 0)
                        {
                            orderAvgPrice = PlaceOrder(ceNode, true, ce.LastPrice);
                            if (orderAvgPrice > 0)
                            {
                                ceNode.CurrentPosition = PositionStatus.Closed;
                                totalProfit += ceNode.Prices.Skip(ceNode.Prices.Count - 2).Sum();

                                if (ceNode.NextNode == null || ceNode.PrevNode == null)
                                    AssignNextNodes(ceNode, _bInstToken, ce.InstrumentType, ce.Strike, ce.Expiry.Value);

                                //move 1 node ahead
                                ceNode = ceNode.NextNode;
                                ce = ceNode.Instrument;

                                ///LIVE
                                optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == ce.InstrumentToken);
                                if (optionTick.LastPrice != 0)
                                {
                                    ce.LastPrice = optionTick.LastPrice;
                                }

                                /////TEMP
                                //if (ce.LastPrice == 0)
                                //{
                                //    ce.LastPrice = dl.RetrieveLastPrice(ce.InstrumentToken, ticks[0].Timestamp);
                                //}
                                ////END
                                orderAvgPrice = PlaceOrder(ceNode, false, ce.LastPrice);


                                if (orderAvgPrice > 0)
                                {
                                    ceNode.CurrentPosition = PositionStatus.Open;

                                    ce.LastPrice = orderAvgPrice;
                                    ceNode.Instrument = ce;

                                    Strangle[(int)InstrumentType.CE] = ceNode;
                                }
                            }
                        }
                        else if ((pe.LastPrice > Math.Abs(peNode.Prices.Last()) + MaxLossPoints) && peNode.Index > 0 )
                        {
                            orderAvgPrice = PlaceOrder(peNode, true, pe.LastPrice);

                            if (orderAvgPrice > 0)
                            {
                                //profitBooked = true;
                                peNode.CurrentPosition = PositionStatus.Closed;
                                totalProfit += peNode.Prices.Skip(peNode.Prices.Count - 2).Sum();

                                

                                if (peNode.NextNode == null || peNode.PrevNode == null)
                                    AssignNextNodes(peNode, _bInstToken, pe.InstrumentType, pe.Strike, pe.Expiry.Value);

                                //move 1 node ahead
                                peNode = peNode.PrevNode;
                                pe = peNode.Instrument;


                                ///LIVE
                                optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == pe.InstrumentToken);
                                if (optionTick.LastPrice != 0)
                                {
                                    pe.LastPrice = optionTick.LastPrice;
                                }

                                ///TEMP
                                //if (pe.LastPrice == 0)
                                //{
                                //    pe.LastPrice = dl.RetrieveLastPrice(pe.InstrumentToken, ticks[0].Timestamp);


                                //}
                                //END

                                orderAvgPrice = PlaceOrder(peNode, false, pe.LastPrice);

                                if (orderAvgPrice > 0)
                                {
                                    peNode.CurrentPosition = PositionStatus.Open;

                                    pe.LastPrice = orderAvgPrice;
                                    peNode.Instrument = pe;

                                    Strangle[(int)InstrumentType.PE] = peNode;
                                }
                            }
                        }

                    }
                    if (ce.LastPrice < Math.Abs(ceNode.Prices.Last()) - MaxProfitPoints)
                    {
                        orderAvgPrice = PlaceOrder(ceNode, true, ce.LastPrice);
                        if (orderAvgPrice > 0)
                        {
                            totalProfit += ceNode.Prices.Skip(ceNode.Prices.Count - 2).Sum();
                                //optionNode.Prices.ElementAtOrDefault(optionNode.Prices.Count - 2);

                            profitBooked = true;

                            ceNode.CurrentPosition = PositionStatus.Closed;

                            if (ceNode.NextNode == null || ceNode.PrevNode == null)
                                AssignNextNodes(ceNode, _bInstToken, ce.InstrumentType, ce.Strike, ce.Expiry.Value);

                            //move 1 node ahead
                            ceNode = ceNode.PrevNode;
                            ce = ceNode.Instrument;

                            ///LIVE
                            optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == ce.InstrumentToken);
                            if (optionTick.LastPrice != 0)
                            {
                                ce.LastPrice = optionTick.LastPrice;
                            }

                            /////TEMP
                            //if (ce.LastPrice == 0)
                            //{
                            //    ce.LastPrice = dl.RetrieveLastPrice(ce.InstrumentToken, ticks[0].Timestamp);
                            //}
                            ////END
                            orderAvgPrice = PlaceOrder(ceNode, false, ce.LastPrice);


                            if (orderAvgPrice > 0)
                            {
                                ceNode.CurrentPosition = PositionStatus.Open;

                                ce.LastPrice = orderAvgPrice;
                                ceNode.Instrument = ce;

                                Strangle[(int)InstrumentType.CE] = ceNode;
                            }
                        }
                    }
                    else if (pe.LastPrice < Math.Abs(peNode.Prices.Last()) - MaxProfitPoints)
                    {
                        orderAvgPrice = PlaceOrder(peNode, true, pe.LastPrice);

                        if (orderAvgPrice > 0)
                        {
                            totalProfit += peNode.Prices.Skip(peNode.Prices.Count - 2).Sum();

                            profitBooked = true;
                            peNode.CurrentPosition = PositionStatus.Closed;

                            if (peNode.NextNode == null || peNode.PrevNode == null)
                                AssignNextNodes(peNode, _bInstToken, pe.InstrumentType, pe.Strike, pe.Expiry.Value);

                            //move 1 node ahead
                            peNode = peNode.NextNode;
                            pe = peNode.Instrument;


                            ///LIVE
                            optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == pe.InstrumentToken);
                            if (optionTick.LastPrice != 0)
                            {
                                pe.LastPrice = optionTick.LastPrice;
                            }

                            ///TEMP
                            //if (pe.LastPrice == 0)
                            //{
                            //    pe.LastPrice = dl.RetrieveLastPrice(pe.InstrumentToken, ticks[0].Timestamp);


                            //}
                            //END

                            orderAvgPrice = PlaceOrder(peNode, false, pe.LastPrice);

                            if (orderAvgPrice > 0)
                            {
                                peNode.CurrentPosition = PositionStatus.Open;

                                pe.LastPrice = orderAvgPrice;
                                peNode.Instrument = pe;

                                Strangle[(int)InstrumentType.PE] = peNode;
                            }
                        }
                    }
                }
            }
            #region
            //else if (ceNode.CurrentPosition == PositionStatus.Closed)
            //{
            //    //Move to next CE Node to neutralize PE impact
            //    ceNode = ceNode.NextNode;

            //    if (pe.LastPrice > Math.Abs(peNode.Prices.Last()) + MaxLossPoints)
            //    {
            //        orderAvgPrice = PlaceOrder(ceNode, false, ce.LastPrice);

            //        if (orderAvgPrice > 0)
            //        {
            //            ceNode.CurrentPosition = PositionStatus.Open;

            //            //do not place order in the system, but add the price for new reference
            //            peNode.Prices.Add(pe.LastPrice * -1);
            //            peNode.Prices.Add(pe.LastPrice);

            //            //peNode.CurrentPosition = PositionStatus.Closed;
            //        }
            //    }
            //    else if (ce.LastPrice < Math.Abs(ceNode.Prices.ElementAt(ceNode.Prices.Count - 2)))
            //    {
            //        orderAvgPrice = PlaceOrder(ceNode, false, ce.LastPrice);

            //        if (orderAvgPrice > 0)
            //        {
            //            ceNode.CurrentPosition = PositionStatus.Open;
            //        }
            //    }
            //}

            //else if (peNode.CurrentPosition == PositionStatus.Closed)
            //{
            //    if (ce.LastPrice < Math.Abs(ceNode.Prices.Last()) - MaxProfitPoints)
            //    {
            //        orderAvgPrice = PlaceOrder(peNode, false, pe.LastPrice);


            //        if (orderAvgPrice > 0)
            //        {
            //            //ceNode.CurrentPosition = PositionStatus.Closed;
            //            peNode.CurrentPosition = PositionStatus.Open;

            //            //do not place order in the system, but add the price for new reference
            //            ceNode.Prices.Add(ce.LastPrice * -1);
            //            ceNode.Prices.Add(ce.LastPrice);
            //        }
            //    }
            //    else if (pe.LastPrice < Math.Abs(peNode.Prices.ElementAt(peNode.Prices.Count - 2)))
            //    {
            //        orderAvgPrice = PlaceOrder(peNode, false, pe.LastPrice);

            //        if (orderAvgPrice > 0)
            //        {
            //            peNode.CurrentPosition = PositionStatus.Open;
            //        }
            //    }
            //}
            #endregion
        }
        private InstrumentListNode MoveToSubsequentNode(InstrumentListNode optionNode, 
            decimal optionPrice, decimal maxProfitPoints, decimal maxLossPoints, Tick[] ticks)
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
                        baseNode.CurrentPosition = currentNode.CurrentPosition;
                        currentNode.PrevNode = baseNode.PrevNode;
                        currentNode.NextNode = baseNode.NextNode;
                        break;
                    }
                    baseNode = baseNode.PrevNode;
                }
            }

            return true;

        }

        ////call this method only if current index is not equal to 0
        //private InstrumentListNode CheckForSquareOff(InstrumentListNode optionNode, decimal safetyBand)
        //{
        //    Instrument currentOption = optionNode.Instrument;
        //    decimal lastSellingPrice = 0;
        //    bool nodeFound = false;
        //    InstrumentListNode subsequentNode = optionNode;

        //    ///TODO: Change this to Case statement.
        //    if (currentOption.InstrumentType.Trim(' ') == "CE" && optionNode.Index > 0)
        //    {
        //        //go to index 0
        //        while (subsequentNode.PrevNode != null)
        //        {
        //            subsequentNode = subsequentNode.PrevNode;
        //            if (subsequentNode.Index == 0)
        //            {
        //                break;
        //            }
        //        }


        //        while (subsequentNode.NextNode != null)
        //        {
        //            if (subsequentNode.Prices.Count > 1)
        //            {
        //                lastSellingPrice = Math.Abs(subsequentNode.Prices.Last());

        //                if ((lastSellingPrice - safetyBand >= subsequentNode.Instrument.LastPrice) && (optionNode.Index != subsequentNode.Index))
        //                {
        //                    nodeFound = true;
        //                    break;
        //                }
        //            }
        //            subsequentNode = subsequentNode.NextNode;
        //        }
        //    }
        //    if (currentOption.InstrumentType.Trim(' ') == "PE" && optionNode.Index < 0)
        //    {
        //        //go to index 0
        //        while (subsequentNode.NextNode != null)
        //        {
        //            subsequentNode = subsequentNode.NextNode;
        //            if (subsequentNode.Index == 0)
        //            {
        //                break;
        //            }
        //        }

        //        while (subsequentNode.PrevNode != null)
        //        {
        //            if (subsequentNode.Prices.Count > 1)
        //            {
        //                lastSellingPrice = Math.Abs(subsequentNode.Prices.Last());

        //                if ((lastSellingPrice - safetyBand >= subsequentNode.Instrument.LastPrice) && (optionNode.Index != subsequentNode.Index))
        //                {
        //                    nodeFound = true;
        //                    break;
        //                }
        //            }
        //            subsequentNode = subsequentNode.PrevNode;
        //        }
        //    }
        //    return nodeFound ? subsequentNode : null;
        //}

        //make sure ref is working with struct . else make it class
        public virtual void OnNext(Tick[] ticks)
        {
            lock (Strangle)
            {
                try
                {

                    ReviewStrangle(ticks);
                }
                catch (Exception exp)
                {
                    //System.Net.Mail.SmtpClient()
                    throw exp;
                }
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

        public void ManageFrequentStrangle(Instrument bInst, Instrument currentPE, Instrument currentCE,
            decimal maxProfitPoints = 0, decimal maxLossPoints = 0, decimal stopLossPoints = 0,
             int strangleId = 0)
        {
            if (currentPE.LastPrice * currentCE.LastPrice != 0)
            {
                ///TODO: placeOrder lowerPutValue and upperCallValue
                /// Get Executed values on to lowerPutValue and upperCallValue

                //Two seperate linked list are maintained with their incides on the linked list.
                Strangle[(int)InstrumentType.PE] = new InstrumentListNode(currentPE);
                Strangle[(int)InstrumentType.CE] = new InstrumentListNode(currentCE);

                //If new strangle, place the order and update the data base. If old strangle monitor it.
                if (strangleId == 0)
                {
                    //TEMP -> First price
                    //Strangle[(int)InstrumentType.PE].Prices.Add(currentPE.LastPrice); //put.SellPrice = 100;
                    //Strangle[(int)InstrumentType.CE].Prices.Add(currentCE.LastPrice);  // call.SellPrice = 100;

                    ///LIVE Uncomment below for real time orders
                    ////Below code is not required when real orders will be placed as traded price can be taken from Place Order method
                    Dictionary<string, Quote> keyValuePairs = ZObjects.kite.GetQuote(new string[] { Convert.ToString(currentCE.InstrumentToken),
                                                                Convert.ToString(currentPE.InstrumentToken) });

                    decimal cePrice = keyValuePairs[Convert.ToString(currentCE.InstrumentToken)].Bids[0].Price;
                    decimal pePrice = keyValuePairs[Convert.ToString(currentPE.InstrumentToken)].Bids[0].Price;


                    pePrice = PlaceOrder(currentPE.TradingSymbol, false, pePrice);
                    cePrice = PlaceOrder(currentCE.TradingSymbol, false, cePrice);

                    //ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, currentCE.TradingSymbol,
                    //                          Constants.TRANSACTION_TYPE_SELL, 300, Product: Constants.PRODUCT_MIS,
                    //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

                    //ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, currentPE.TradingSymbol,
                    //                          Constants.TRANSACTION_TYPE_SELL, 300, Product: Constants.PRODUCT_MIS,
                    //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);



                    //put.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));
                    //call.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));

                    Strangle[(int)InstrumentType.PE].Prices.Add(pePrice); //put.SellPrice = 100;
                    Strangle[(int)InstrumentType.CE].Prices.Add(cePrice);  // call.SellPrice = 100

                    Strangle[(int)InstrumentType.CE].CurrentPosition = PositionStatus.Open;
                    Strangle[(int)InstrumentType.PE].CurrentPosition = PositionStatus.Open;

                    MaxProfitPoints = maxProfitPoints;
                    MaxLossPoints = maxLossPoints;
                    _bInstToken = bInst.InstrumentToken;

                    //Update Database
                    DataLogic dl = new DataLogic();
                    listID = strangleId = dl.StoreStrangleData(currentCE.InstrumentToken, currentPE.InstrumentToken, Strangle[(int)InstrumentType.CE].Prices.Last(),
                       Strangle[(int)InstrumentType.PE].Prices.Last(), AlgoIndex.FrequentBuySellWithMovement, maxProfitPoints, maxLossPoints, maxProfitPoints, maxLossPoints,
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

        private void LoadActiveData()
        {
            //InstrumentLinkedList[] strangleList = new InstrumentLinkedList[2] {
            //    new InstrumentLinkedList(),
            //    new InstrumentLinkedList()
            //};
            AlgoIndex algoIndex = AlgoIndex.FrequentBuySellWithMovement;
            DataLogic dl = new DataLogic();
            DataSet activeStrangles = dl.RetrieveActiveData(algoIndex);
            DataRelation strangle_Token_Relation = activeStrangles.Relations.Add("Strangle_Token", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"], activeStrangles.Tables[0].Columns["OptionType"] },
                new DataColumn[] { activeStrangles.Tables[1].Columns["StrategyId"], activeStrangles.Tables[1].Columns["Type"] });

            byte strangleCount = 0;
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
                    _bInstToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]);
                }
                listID = (int)strangleRow["Id"];
                InstrumentType instrumentType = (string)strangleRow["OptionType"] == "CE" ? InstrumentType.CE : InstrumentType.PE;


                Strangle[(int)instrumentType] = strangleTokenNode;
                Strangle[(int)instrumentType].CurrentPosition = PositionStatus.Open;

                MaxProfitPoints = (decimal)strangleRow["MaxProfitPoints"];
                MaxLossPoints = (decimal)strangleRow["MaxLossPoints"];
                //_bInst = bInst;
                //InstrumentLinkedList instrumentLinkedList = new InstrumentLinkedList(
                //        strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]))
                //{
                //    CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
                //    MaxLossPoints = (decimal)strangleRow["MaxLossPoints"],
                //    MaxProfitPoints = (decimal)strangleRow["MaxProfitPoints"],
                //    StopLossPoints = (decimal)strangleRow["StopLossPoints"],
                //    NetPrice = (decimal)strangleRow["NetPrice"],
                //    listID = strategyId
                //};

                //if (ActiveStrangles.ContainsKey(strategyId))
                //{
                //    ActiveStrangles[strategyId][(int)instrumentType] = instrumentLinkedList;
                //}
                //else
                //{
                //    InstrumentLinkedList[] strangle = new InstrumentLinkedList[2];
                //    strangle[(int)instrumentType] = instrumentLinkedList;

                //    ActiveStrangles.Add(strategyId, strangle);
                //}


            }

        }

        private decimal PlaceOrder(InstrumentListNode optionNode, bool buyOrder, decimal currentPrice)
        {
            //temp
            Instrument option = optionNode.Instrument;



            decimal averagePrice = PlaceOrder(optionNode.Instrument.TradingSymbol, buyOrder, currentPrice);//, currentPrice);

            optionNode.Prices.Add(averagePrice);
            option.LastPrice = Math.Abs(averagePrice);
            optionNode.Instrument = option;

            //make next symbol as next to current node. Also update PL data in linklist main
            //also update currnetindex in the linklist main
            DataLogic dl = new DataLogic();
            dl.UpdateOptionData(listID, option.InstrumentToken, option.InstrumentType,
               optionNode.Prices.Last(), averagePrice, AlgoIndex.FrequentBuySellWithMovement);

            return Math.Abs(averagePrice);
            
        }
        private decimal PlaceOrder(string tradingSymbol, bool buyOrder,  decimal currentPrice)
        {
            //Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
            //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, 150, Product: Constants.PRODUCT_MIS,
            //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);


            //string orderId = "0";
            decimal averagePrice = 0;
            //if (orderStatus["data"]["order_id"] != null)
            //{
            //    orderId = orderStatus["data"]["order_id"];
            //}
            //if (orderId != "0")
            //{
            //    System.Threading.Thread.Sleep(400);
            //    List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
            //    averagePrice = orderInfo[orderInfo.Count - 1].AveragePrice;
            //}
            if (averagePrice == 0)
                averagePrice = buyOrder ? currentPrice + 1 : currentPrice - 1;
            return buyOrder ? averagePrice * -1 : averagePrice;
        }
    }
}
