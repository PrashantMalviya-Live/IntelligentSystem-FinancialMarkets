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
    public class BalancedStrangle : IObserver<Tick[]>
    {
        public BalancedStrangle()
        {
            LoadActiveData();
        }

        Dictionary<int, StrangleInstrumentLinkedList> ActiveStrangles = new Dictionary<int, StrangleInstrumentLinkedList>();
        const double SAFETY_MARGIN = 0.15;
        const double DELTA_ENTRY = 0.3;//0.5;
        const double DELTA_NEUTRAL_BAND = 0.07;
        //const double DELTA_UPPER_CAP = 0.38;

        //make sure ref is working with struct . else make it class
        public virtual void OnNext(Tick[] ticks)
        {
            lock (ActiveStrangles)
            {
                foreach (KeyValuePair<int, StrangleInstrumentLinkedList> keyValuePair in ActiveStrangles)
                {
                    ReviewStrangle(keyValuePair.Value, ticks);
                }
            }
        }

        private void ReviewStrangle(StrangleInstrumentLinkedList strangleList, Tick[] ticks)
        {
            StrangleInstrumentListNode strangleNode = strangleList.Current;
            InstrumentListNode callNode = strangleNode.CallNode;
            InstrumentListNode putNode = strangleNode.PutNode;

            Instrument callOption = callNode.Instrument;
            Instrument putOption = putNode.Instrument;

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
                strangleList.Current.CallNode.Instrument = callOption;
            }

            optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == putOption.InstrumentToken);
            if (optionTick.LastPrice != 0)
            {
                putOption.LastPrice = optionTick.LastPrice;
                strangleList.Current.PutNode.Instrument = putOption;
            }

            //Pulling next nodes from the begining and keeping it up to date from the ticks. This way next nodes will be available when needed, as Tick table takes time.
            //if (strangleNode.NextNode == null || strangleNode.PrevNode == null)
            AssignNeighbouringNodes(callNode, callOption.BaseInstrumentToken, callOption);
            AssignNeighbouringNodes(putNode, putOption.BaseInstrumentToken, putOption);

            UpdateLastPriceOnAllNodes(callNode, ref ticks);
            UpdateLastPriceOnAllNodes(putNode, ref ticks);

            int currentIndex = strangleNode.Index;
            //StrangleListNode minimumPremiumNode = GetSubsequentNodes(strangleNode, strangleList.BaseInstrumentPrice, ticks);
            StrangleInstrumentListNode minimumPremiumNode = GetSubsequentNodes_v2(strangleNode, strangleList.BaseInstrumentPrice, ref ticks);
            

            decimal[] prevNodeBuyPrice = new decimal[3];
            decimal[] currentNodeSellPrice = new decimal[3];
            if (currentIndex != minimumPremiumNode.Index)
            {
                uint prevCallToken = callOption.InstrumentToken;
                uint prevPutToken = putOption.InstrumentToken;

                Instrument newCallOption = minimumPremiumNode.CallNode.Instrument;
                Instrument newPutOption = minimumPremiumNode.PutNode.Instrument;

                if (minimumPremiumNode.Prices.Count > 0 && minimumPremiumNode.Prices[0].First() == 0)
                {
                    minimumPremiumNode.Prices.RemoveAt(0);
                }

                //buy current strangle and close this position
                //buy prices are negative and sell are positive. as sign denotes cashflow direction

                //check if buy and sell is of same option before putting order
                if (strangleNode.CallNode.Index != minimumPremiumNode.CallNode.Index)
                {
                    prevNodeBuyPrice[0] = PlaceOrder(callOption.TradingSymbol, buyOrder: true, callOption.LastPrice);
                    currentNodeSellPrice[0] = PlaceOrder(newCallOption.TradingSymbol, buyOrder: false, newCallOption.LastPrice);
                }
                else
                {
                    prevNodeBuyPrice[0] = -1 * callOption.LastPrice;
                    currentNodeSellPrice[0] = newCallOption.LastPrice;
                }
                if (strangleNode.PutNode.Index != minimumPremiumNode.PutNode.Index)
                {
                    prevNodeBuyPrice[1] = PlaceOrder(putOption.TradingSymbol, buyOrder: true, putOption.LastPrice);
                    currentNodeSellPrice[1] = PlaceOrder(newPutOption.TradingSymbol, buyOrder: false, newPutOption.LastPrice);
                }
                else
                {
                    prevNodeBuyPrice[1] = -1* putOption.LastPrice;
                    currentNodeSellPrice[1] = newPutOption.LastPrice;
                }
                prevNodeBuyPrice[2] = strangleList.BaseInstrumentPrice;
                currentNodeSellPrice[2] = strangleList.BaseInstrumentPrice;

                strangleNode.Prices.Add(prevNodeBuyPrice);
                int prevInstrumentIndex = strangleNode.Index;
                minimumPremiumNode.Prices.Add(currentNodeSellPrice);

                //buy prices are negative and sell are positive. as sign denotes cashflow direction

                //Do we need current instrument index , when current instrument is already present in the linked list?
                //It can see the index of current instrument using list.current.index
                strangleList.CurrentInstrumentIndex = minimumPremiumNode.Index;

                //double callDelta = newCallOption.UpdateDelta(Convert.ToDouble(newCallOption.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(strangleList.BaseInstrumentPrice));
                double callDelta = newCallOption.UpdateDelta(Convert.ToDouble(newCallOption.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(strangleList.BaseInstrumentPrice));
                callDelta = Math.Abs(callDelta);

                //double putDelta = newPutOption.UpdateDelta(Convert.ToDouble(newPutOption.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(strangleList.BaseInstrumentPrice));
                double putDelta = newPutOption.UpdateDelta(Convert.ToDouble(newPutOption.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(strangleList.BaseInstrumentPrice));
                putDelta = Math.Abs(putDelta);

                //minimumPremiumNode.DeltaThreshold = Math.Max(callDelta, putDelta) + SAFETY_MARGIN;
                //minimumPremiumNode.DeltaThreshold = Math.Min(Math.Max(callDelta, putDelta), DELTA_ENTRY) + SAFETY_MARGIN;
                minimumPremiumNode.DeltaThreshold = Math.Min(Math.Max(putDelta, callDelta) * (1 + SAFETY_MARGIN / DELTA_ENTRY), DELTA_ENTRY+ SAFETY_MARGIN);

                //make next symbol as next to current node. Also update PL data in linklist main
                //also update currnetindex in the linklist main
                DataLogic dl = new DataLogic();
                dl.UpdateListData(strangleList.listID, newCallOption.InstrumentToken, newPutOption.InstrumentToken, prevCallToken, prevPutToken,
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
                System.Threading.Thread.Sleep(200);
            //    List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
            //    averagePrice = orderInfo[orderInfo.Count - 1].AveragePrice;
            //}
            if (averagePrice == 0)
                averagePrice = buyOrder ? currentPrice + 1 : currentPrice - 1;
            return buyOrder ? averagePrice * -1 : averagePrice;
        }

        private StrangleInstrumentListNode GetSubsequentNodes_v2(StrangleInstrumentListNode strangleNode, decimal baseInstrumentPrice, ref Tick[] ticks)
        {
            StrangleInstrumentListNode subsequentNode = strangleNode;
            InstrumentListNode callNode = strangleNode.CallNode;
            InstrumentListNode putNode = strangleNode.PutNode;

            Instrument callOption = callNode.Instrument;
            Instrument putOption = putNode.Instrument;

            int currentCallIndex = callNode.Index;
            int currentPutIndex = putNode.Index;

            //Store base instrument price at the begining as reference and then with every trade store the reference.
            //check with last reference for say 100 point movements, or check for node crossover of base instrument.

            if (strangleNode.Prices.Count > 0 && putOption.LastPrice != 0 && callOption.LastPrice != 0)
            {
                //decimal stranglePremium = currentNode.CallNode.Instrument.LastPrice + currentNode.PutNode.Instrument.LastPrice - safetyBand;
                //decimal premiumAvailable;

               // double callDelta = callOption.UpdateDelta(Convert.ToDouble(callOption.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
                double callDelta = callOption.UpdateDelta(Convert.ToDouble(callOption.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(baseInstrumentPrice));
                callDelta = Math.Abs(callDelta);

                //double putDelta = putOption.UpdateDelta(Convert.ToDouble(putOption.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
                double putDelta = putOption.UpdateDelta(Convert.ToDouble(putOption.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(baseInstrumentPrice));
                putDelta = Math.Abs(putDelta);

                if (putDelta < strangleNode.DeltaThreshold - SAFETY_MARGIN && callDelta < strangleNode.DeltaThreshold - SAFETY_MARGIN)
                {
                    strangleNode.DeltaThreshold = Math.Max(putDelta, callDelta) * (1 + SAFETY_MARGIN / DELTA_ENTRY);
                    Console.WriteLine(strangleNode.DeltaThreshold);
                }

                if (callDelta > strangleNode.DeltaThreshold)
                {
                    GetSubsequentInstrumentNodes(ref callNode, ref putNode, higherNodes: true, baseInstrumentPrice, ref ticks, callDelta, putDelta);

                    if (currentCallIndex == callNode.Index && currentPutIndex == putNode.Index)
                    {
                        subsequentNode = strangleNode;
                    }
                    else
                    {
                        subsequentNode = new StrangleInstrumentListNode(callNode, putNode);
                        strangleNode.NextNode = subsequentNode;
                        subsequentNode.PrevNode = strangleNode;
                        subsequentNode.Index = strangleNode.Index + 1;
                    }
                }
                else if (putDelta > strangleNode.DeltaThreshold)
                {
                    GetSubsequentInstrumentNodes(ref callNode, ref putNode, higherNodes: false, baseInstrumentPrice, ref ticks, callDelta, putDelta);

                    if (currentCallIndex == callNode.Index && currentPutIndex == putNode.Index)
                    {
                        subsequentNode = strangleNode;
                    }
                    else
                    {
                        subsequentNode = new StrangleInstrumentListNode(callNode, putNode);
                        strangleNode.PrevNode = subsequentNode;
                        subsequentNode.NextNode = strangleNode;
                        subsequentNode.Index = strangleNode.Index - 1;
                    }
                }
            }
            return subsequentNode;
        }


        /// <summary>
        /// Move to trigger node to below entry point and non trigger mode to its point
        /// </summary>
        /// <param name = "callNode" ></ param >
        /// < param name="putNode"></param>
        ///<param name = "moveHigher" ></ param >
        /// < param name="bInstrumentPrice"></param>
        /// <param name = "ticks" ></ param >
        /// < param name="currentCallDelta"></param>
        /// <param name = "currentPutDelta" ></ param >
        //private void GetSubsequentInstrumentNodes(ref InstrumentListNode callNode, ref InstrumentListNode putNode,
        //    bool moveHigher, decimal bInstrumentPrice, ref Tick[] ticks, double currentCallDelta, double currentPutDelta)
        //{
        //    if (moveHigher)
        //    {
        //        //Set
        //    }
        //}
        //private int MoveTriggerNode(ref InstrumentListNode triggerNode, bool moveHigher, decimal bInstrumentPrice, ref Tick[] ticks, double currentDelta)
        //{
        //    Instrument option = optionNode.Instrument;
        //    Instrument otherOption = otherOptionNode.Instrument;
        //    bool moveOtherNode;

        //    while (true)
        //    {
        //        AssignNeighbouringNodes(optionNode, option.BaseInstrumentToken, option);
        //        AssignNeighbouringNodes(otherOptionNode, otherOption.BaseInstrumentToken, otherOption);



        //        if ((higherNodes && higherDelta) || (!higherNodes && !higherDelta))
        //        {
        //            if (optionNode.PrevNode == null)
        //                return;
        //            else
        //            {
        //                optionNode = optionNode.PrevNode;
        //                moveOtherNode = false;
        //                if (otherOptionNode.PrevNode != null)
        //                {
        //                    otherOptionNode = otherOptionNode.PrevNode;
        //                    moveOtherNode = true;
        //                }
        //            }
        //        }
        //        else
        //        {
        //            if (optionNode.NextNode == null)
        //                return;
        //            else
        //            {
        //                optionNode = optionNode.NextNode;
        //                moveOtherNode = false;
        //                if (otherOptionNode.NextNode != null)
        //                {
        //                    otherOptionNode = otherOptionNode.NextNode;
        //                    moveOtherNode = true;
        //                }

        //            }
        //        }
            
        //        //optionNode = higherNodes ? optionNode.NextNode : optionNode.PrevNode;
        //        if (optionNode.Instrument.LastPrice != 0)
        //        {
        //            optionDelta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(optionNode.Instrument.LastPrice),
        //                0.1, timeoftrade, Convert.ToDouble(bInstrumentPrice));
        //            optionDelta = Math.Abs(optionDelta);

        //            if (!double.IsNaN(optionDelta) && higherDelta && optionDelta > DELTA_ENTRY)
        //            {
        //                optionNode = higherNodes ? optionNode.NextNode : optionNode.PrevNode;
        //                otherOptionNode = higherNodes ? otherOptionNode.NextNode : otherOptionNode.PrevNode;

        //                optionDelta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(optionNode.Instrument.LastPrice),
        //                0.1, timeoftrade, Convert.ToDouble(bInstrumentPrice));
        //                optionDelta = Math.Abs(optionDelta);

        //                break;
        //            }
        //            if (!double.IsNaN(optionDelta) && !higherDelta && optionDelta < DELTA_ENTRY)
        //            {
        //                break;
        //            }
        //        }
        //        else
        //        {
        //            if ((higherNodes && higherDelta) || (!higherNodes && !higherDelta))
        //            {
        //                optionNode = optionNode.NextNode;
        //                if (moveOtherNode)
        //                    otherOptionNode = otherOptionNode.NextNode;
        //            }
        //            else
        //            {
        //                optionNode = optionNode.PrevNode;
        //                if (moveOtherNode)
        //                    otherOptionNode = otherOptionNode.PrevNode;
        //            }
        //            break;
        //        }
        //    }


        //    InstrumentListNode subsequentCallNode = callNode, subsequentPutNode = putNode;
        //    Instrument callOption = callNode.Instrument;
        //    Instrument putOption = putNode.Instrument;
        //    double callDelta = currentCallDelta, putDelta = currentPutDelta;

        //    InstrumentListNode triggerNode, nonTriggerNode;

        //    triggerNode = higherNodes ? callNode : putNode;
        //    nonTriggerNode = higherNodes ? putNode : callNode;
        //    double triggerNodeDelta = higherNodes ? callDelta : putDelta;
        //    double nonTriggerNodeDelta = higherNodes ? putDelta : callDelta;

        //    //GetNodeNearEntryDelta(ref triggerNode, ref nonTriggerNode, DateTime.Now, bInstrumentPrice, ref triggerNodeDelta, higherNodes, (triggerNodeDelta < DELTA_ENTRY));
        //    //GetDeltaNeutralNode(triggerNodeDelta, ref nonTriggerNode, higherNodes, DateTime.Now, bInstrumentPrice);


        //    GetNodeNearEntryDelta(ref triggerNode, ref nonTriggerNode, ticks[0].Timestamp, bInstrumentPrice, ref triggerNodeDelta, higherNodes, (triggerNodeDelta < DELTA_ENTRY));
        //    GetDeltaNeutralNode(triggerNodeDelta, ref nonTriggerNode, higherNodes, ticks[0].Timestamp, bInstrumentPrice);

        //    callNode = higherNodes ? triggerNode : nonTriggerNode;
        //    putNode = higherNodes ? nonTriggerNode : triggerNode;

        //    return 0;
        //}
        //private void MoveNonTriggerNode()
        //{

        //}

        private void GetSubsequentInstrumentNodes(ref InstrumentListNode callNode, ref InstrumentListNode putNode, 
            bool higherNodes, decimal bInstrumentPrice, ref Tick[] ticks, double currentCallDelta, double currentPutDelta)
        {
            try
            {
                InstrumentListNode subsequentCallNode = callNode, subsequentPutNode = putNode;
                Instrument callOption = callNode.Instrument;
                Instrument putOption = putNode.Instrument;
                double callDelta = currentCallDelta, putDelta = currentPutDelta;

                InstrumentListNode triggerNode, nonTriggerNode;

                triggerNode = higherNodes ? callNode : putNode;
                nonTriggerNode = higherNodes ? putNode : callNode;
                double triggerNodeDelta = higherNodes ? callDelta : putDelta;
                double nonTriggerNodeDelta = higherNodes ? putDelta : callDelta;

                //GetNodeNearEntryDelta(ref triggerNode, ref nonTriggerNode, DateTime.Now, bInstrumentPrice, ref triggerNodeDelta, higherNodes, (triggerNodeDelta < DELTA_ENTRY));
                //GetDeltaNeutralNode(triggerNodeDelta, ref nonTriggerNode, higherNodes, DateTime.Now, bInstrumentPrice);


                GetNodeNearEntryDelta(ref triggerNode, ref nonTriggerNode, ticks[0].LastTradeTime, bInstrumentPrice, ref triggerNodeDelta, higherNodes, (triggerNodeDelta < DELTA_ENTRY));
                GetDeltaNeutralNode(triggerNodeDelta, ref nonTriggerNode, higherNodes, ticks[0].LastTradeTime, bInstrumentPrice);

                callNode = higherNodes ? triggerNode : nonTriggerNode;
                putNode = higherNodes ? nonTriggerNode : triggerNode;

            }
            catch (Exception ex)
            {
                throw ex;
            }
            #region CommentedCode
            //  while (true)
            //  {
            //      AssignNeighbouringNodes(subsequentCallNode, callOption.BaseInstrumentToken, callOption);
            //      if ((higherNodes && subsequentCallNode.NextNode == null) || (!higherNodes && subsequentCallNode.PrevNode == null))
            //      {
            //          return;
            //      }
            //      AssignNeighbouringNodes(subsequentPutNode, putOption.BaseInstrumentToken, putOption);
            //      if ((higherNodes && subsequentPutNode.NextNode == null) || (!higherNodes && subsequentPutNode.PrevNode == null))
            //      {
            //          return;
            //      }

            //      if (higherNodes) // With movement towards higher nodes, call delta will be driving delta
            //  {
            //      GetNodeNearEntryDelta(ref callNode, ticks[0].Timestamp, bInstrumentPrice, currentCallDelta, higherNodes, (callDelta < DELTA_ENTRY));
            //  }
            //  else // With movement towards lower nodes, put delta will be driving delta
            //  {
            //      GetNodeNearEntryDelta(ref putNode, ticks[0].Timestamp, bInstrumentPrice, currentPutDelta, higherNodes, (putDelta < DELTA_ENTRY));
            //  }


            //      GetDeltaNeutralNode(callDelta, ref putNode, higherNodes, ticks[0].Timestamp, bInstrumentPrice);



            //      if ((currentCallDelta < DELTA_ENTRY) || (optionType == InstrumentType.PE && currentNodeDelta > DELTA_ENTRY))
            //  {
            ////      private void GetNodeNearEntryDelta(ref InstrumentListNode optionNode, DateTime? timeoftrade,
            ////decimal bInstrumentPrice, double currentNodeDelta, InstrumentType optionType, bool higherNodes, bool higherDelta)
            //  }


            //  while (true)
            //  {
            //      AssignNeighbouringNodes(subsequentCallNode, callOption.BaseInstrumentToken, callOption);
            //      if ((higherNodes && subsequentCallNode.NextNode == null) || (!higherNodes && subsequentCallNode.PrevNode == null))
            //      {
            //          return;
            //      }
            //      AssignNeighbouringNodes(subsequentPutNode, putOption.BaseInstrumentToken, putOption);
            //      if ((higherNodes && subsequentPutNode.NextNode == null) || (!higherNodes && subsequentPutNode.PrevNode == null))
            //      {
            //          return;
            //      }

            //      if (higherNodes)
            //      {
            //         // GetNodeNearEntryDelta()

            //          if (callDelta < DELTA_ENTRY)
            //          {
            //              subsequentCallNode = subsequentCallNode.PrevNode;

            //              if (subsequentCallNode.Instrument.LastPrice != 0)
            //              {
            //                  // callDelta = subsequentCallNode.Call.UpdateDelta(Convert.ToDouble(subsequentCallNode.Instrument.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
            //                  callDelta = subsequentCallNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentCallNode.Instrument.LastPrice), 0.1, ticks[0].Timestamp, Convert.ToDouble(bInstrumentPrice));
            //                  callDelta = Math.Abs(callDelta);
            //                  if (!double.IsNaN(callDelta) && callDelta > DELTA_ENTRY)
            //                  {
            //                      callNode = subsequentCallNode.NextNode;

            //                      GetDeltaNeutralNode(callDelta, ref putNode, higherNodes, ticks[0].Timestamp, bInstrumentPrice);
            //                          //while (true)
            //                          //{
            //                          //    subsequentPutNode = subsequentPutNode.NextNode;

            //                          //    if (subsequentPutNode.Instrument.LastPrice != 0)
            //                          //    {
            //                          //        //putDelta = subsequentPutNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentPutNode.Instrument.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(bInstrumentPrice));
            //                          //        putDelta = subsequentPutNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentPutNode.Instrument.LastPrice), 0.1, ticks[0].Timestamp, Convert.ToDouble(bInstrumentPrice));
            //                          //        putDelta = Math.Abs(putDelta);
            //                          //        if (!double.IsNaN(putDelta))
            //                          //        {
            //                          //            if (Math.Abs(callDelta - putDelta) < DELTA_NEUTRAL_BAND || putDelta > callDelta) //90% of upper delta
            //                          //            {
            //                          //                putNode = subsequentPutNode;
            //                          //                break;
            //                          //            }
            //                          //        }
            //                          //    }
            //                          //}
            //                          break;
            //                  }
            //                  else
            //                  {
            //                      continue;
            //                  }
            //              }
            //              else
            //              {
            //                  subsequentCallNode = subsequentCallNode.NextNode;
            //                  callNode = subsequentCallNode;
            //              }
            //          }
            //          else
            //          {
            //              subsequentCallNode = subsequentCallNode.NextNode;
            //              if (subsequentCallNode.Instrument.LastPrice == 0)
            //              {
            //                  callNode = subsequentCallNode.PrevNode;
            //              }

            //              // callDelta = subsequentCallNode.Call.UpdateDelta(Convert.ToDouble(subsequentCallNode.Instrument.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
            //              callDelta = subsequentCallNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentCallNode.Instrument.LastPrice), 0.1, ticks[0].Timestamp, Convert.ToDouble(bInstrumentPrice));
            //              callDelta = Math.Abs(callDelta);
            //              if (!double.IsNaN(callDelta) && callDelta < DELTA_ENTRY)
            //              {
            //                  callNode = subsequentCallNode;

            //                  GetDeltaNeutralNode(callDelta, ref putNode, higherNodes, ticks[0].Timestamp, bInstrumentPrice);

            //                  //while (true)
            //                  //{
            //                  //    subsequentPutNode = subsequentPutNode.NextNode;

            //                  //    if (subsequentPutNode.Instrument.LastPrice != 0)
            //                  //    {
            //                  //        //putDelta = subsequentPutNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentPutNode.Instrument.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(bInstrumentPrice));
            //                  //        putDelta = subsequentPutNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentPutNode.Instrument.LastPrice), 0.1, ticks[0].Timestamp, Convert.ToDouble(bInstrumentPrice));
            //                  //        putDelta = Math.Abs(putDelta);
            //                  //        if (!double.IsNaN(putDelta))
            //                  //        {
            //                  //            if (Math.Abs(callDelta - putDelta) < DELTA_NEUTRAL_BAND || putDelta > callDelta) //90% of upper delta
            //                  //            {
            //                  //                putNode = subsequentPutNode;
            //                  //                break;
            //                  //            }
            //                  //        }
            //                  //    }
            //                  //}
            //                  break;
            //              }
            //              else
            //              {
            //                  continue;
            //              }
            //          }
            //      }
            //      else
            //      {
            //          if (putDelta < DELTA_ENTRY)
            //          {
            //              subsequentPutNode = subsequentPutNode.NextNode;

            //              if (subsequentPutNode.Instrument.LastPrice != 0)
            //              {
            //                  // callDelta = subsequentCallNode.Call.UpdateDelta(Convert.ToDouble(subsequentCallNode.Instrument.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
            //                  putDelta = subsequentPutNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentPutNode.Instrument.LastPrice), 0.1, ticks[0].Timestamp, Convert.ToDouble(bInstrumentPrice));
            //                  putDelta = Math.Abs(putDelta);
            //                  if (!double.IsNaN(putDelta) && putDelta > DELTA_ENTRY)
            //                  {
            //                      putNode = subsequentPutNode.PrevNode;

            //                      //GetDeltaNeutralNode(putDelta, ref callNode, higherNodes: false, DateTime.Now, bInstrumentPrice);
            //                      GetDeltaNeutralNode(putDelta, ref callNode, higherNodes, ticks[0].Timestamp, bInstrumentPrice);

            //                      //while (true)
            //                      //{
            //                      //    subsequentCallNode = subsequentCallNode.PrevNode;

            //                      //    if (subsequentCallNode.Instrument.LastPrice != 0)
            //                      //    {
            //                      //        //putDelta = subsequentPutNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentPutNode.Instrument.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(bInstrumentPrice));
            //                      //        callDelta = subsequentCallNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentCallNode.Instrument.LastPrice), 0.1, ticks[0].Timestamp, Convert.ToDouble(bInstrumentPrice));
            //                      //        callDelta = Math.Abs(callDelta);
            //                      //        if (!double.IsNaN(callDelta))
            //                      //        {
            //                      //            if (Math.Abs(callDelta - putDelta) < DELTA_NEUTRAL_BAND || callDelta > putDelta) //90% of upper delta
            //                      //            {
            //                      //                callNode = subsequentCallNode;
            //                      //                break;
            //                      //            }
            //                      //        }
            //                      //    }
            //                      //}
            //                      break;
            //                  }
            //                  else
            //                  {
            //                      continue;
            //                  }
            //              }
            //              else
            //              {
            //                  subsequentPutNode = subsequentPutNode.PrevNode;
            //                  putNode = subsequentPutNode;
            //              }
            //          }
            //          else
            //          {
            //              subsequentPutNode = subsequentPutNode.PrevNode;
            //              if (subsequentPutNode.Instrument.LastPrice == 0)
            //              {
            //                  putNode = subsequentPutNode.NextNode;
            //              }

            //              // callDelta = subsequentCallNode.Call.UpdateDelta(Convert.ToDouble(subsequentCallNode.Instrument.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
            //              putDelta = subsequentPutNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentPutNode.Instrument.LastPrice), 0.1, ticks[0].Timestamp, Convert.ToDouble(bInstrumentPrice));
            //              putDelta = Math.Abs(putDelta);
            //              if (!double.IsNaN(putDelta) && putDelta < DELTA_ENTRY)
            //              {
            //                  putNode = subsequentPutNode;
            //                  //GetDeltaNeutralNode(putDelta, ref callNode, higherNodes: false, DateTime.Now, bInstrumentPrice);
            //                  GetDeltaNeutralNode(putDelta, ref callNode, higherNodes: false, ticks[0].Timestamp, bInstrumentPrice);

            //                  //while (true)
            //                  //{
            //                  //    subsequentCallNode = subsequentCallNode.PrevNode;

            //                  //    if (subsequentCallNode.Instrument.LastPrice != 0)
            //                  //    {
            //                  //        //putDelta = subsequentPutNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentPutNode.Instrument.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(bInstrumentPrice));
            //                  //        callDelta = subsequentCallNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentCallNode.Instrument.LastPrice), 0.1, ticks[0].Timestamp, Convert.ToDouble(bInstrumentPrice));
            //                  //        callDelta = Math.Abs(callDelta);
            //                  //        if (!double.IsNaN(callDelta))
            //                  //        {
            //                  //            if (Math.Abs(callDelta - putDelta) < DELTA_NEUTRAL_BAND || callDelta > putDelta) //90% of upper delta
            //                  //            {
            //                  //                callNode = subsequentCallNode;
            //                  //                break;
            //                  //            }
            //                  //        }
            //                  //    }
            //                  //}
            //                  break;
            //              }
            //              else
            //              {
            //                  continue;
            //              }
            //          }
            //      }
            //      //check current delta to undersand which nodes to be moved.

            //      //subsequentCallNode = higherNodes ? subsequentCallNode.NextNode : subsequentCallNode.PrevNode;
            //      //subsequentPutNode = higherNodes ? subsequentPutNode.NextNode : subsequentPutNode.PrevNode;

            //      //if (subsequentCallNode.Instrument.LastPrice == 0 || subsequentPutNode.Instrument.LastPrice == 0)
            //      //{ return; }

            //      //// callDelta = subsequentCallNode.Call.UpdateDelta(Convert.ToDouble(subsequentCallNode.Instrument.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
            //      //callDelta = subsequentCallNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentCallNode.Instrument.LastPrice), 0.1, ticks[0].Timestamp, Convert.ToDouble(bInstrumentPrice));
            //      //callDelta = Math.Abs(callDelta);

            //      //// putDelta = subsequentPutNode.Call.UpdateDelta(Convert.ToDouble(subsequentPutNode.Put.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
            //      //putDelta = subsequentPutNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentPutNode.Instrument.LastPrice), 0.1, ticks[0].Timestamp, Convert.ToDouble(bInstrumentPrice));
            //      //putDelta = Math.Abs(putDelta);

            //      //if (double.IsNaN(callDelta) || double.IsNaN(putDelta))
            //      //{
            //      //    return;
            //      //}


            //      //if (Math.Abs(callDelta - putDelta) < DELTA_NEUTRAL_BAND) //90% of upper delta
            //      //{
            //      //    callNode = subsequentCallNode;
            //      //    putNode = subsequentPutNode;
            //      //    break;
            //      //}
            //  }
            #endregion
        }

        private void GetNodeNearEntryDelta(ref InstrumentListNode optionNode, ref InstrumentListNode otherOptionNode, DateTime? timeoftrade,
            decimal bInstrumentPrice, ref double optionDelta, bool higherNodes, bool higherDelta)
        {
            Instrument option = optionNode.Instrument;
            Instrument otherOption = otherOptionNode.Instrument;
            bool moveOtherNode;

            while (true)
            {
                AssignNeighbouringNodes(optionNode, option.BaseInstrumentToken, option);
                AssignNeighbouringNodes(otherOptionNode, otherOption.BaseInstrumentToken, otherOption);



                if ((higherNodes && higherDelta) || (!higherNodes && !higherDelta))
                {
                    if (optionNode.PrevNode == null)
                        return;
                    else
                    {
                        optionNode = optionNode.PrevNode;
                        moveOtherNode = false;
                        if (otherOptionNode.PrevNode != null)
                        {
                            otherOptionNode = otherOptionNode.PrevNode;
                            moveOtherNode = true;
                        }
                    }
                }
                else
                {
                    if (optionNode.NextNode == null)
                        return;
                    else
                    {
                        optionNode = optionNode.NextNode;
                        moveOtherNode = false;
                        if (otherOptionNode.NextNode != null)
                        {
                            otherOptionNode = otherOptionNode.NextNode;
                            moveOtherNode = true;
                        }

                    }
                }

                #region commented code
                //if(higherNodes)
                //{
                //    if(higherDelta)
                //    {
                //        if (optionNode.PrevNode == null)
                //            return;
                //        else
                //            optionNode = optionNode.PrevNode;
                //    }
                //    else
                //    {
                //        if (optionNode.NextNode == null)
                //            return;
                //        else
                //        optionNode = optionNode.NextNode;
                //    }
                //}
                //else
                //{
                //    if (higherDelta)
                //    {
                //        if (optionNode.NextNode == null)
                //            return;
                //        else
                //            optionNode = optionNode.NextNode;
                //    }
                //    else
                //    {
                //        if (optionNode.PrevNode == null)
                //            return;
                //        else
                //            optionNode = optionNode.PrevNode;
                //    }
                //}

                //if (higherNodes && higherDelta)
                //{
                //    if (optionNode.PrevNode == null)
                //        return;
                //}
                //else
                //{
                //    if (optionNode.NextNode == null)
                //        return;
                //}
                #endregion
                //optionNode = higherNodes ? optionNode.NextNode : optionNode.PrevNode;
                if (optionNode.Instrument.LastPrice != 0)
                {
                    optionDelta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(optionNode.Instrument.LastPrice),
                        0.1, timeoftrade, Convert.ToDouble(bInstrumentPrice));
                    optionDelta = Math.Abs(optionDelta);

                    if (!double.IsNaN(optionDelta) && higherDelta && optionDelta > DELTA_ENTRY)
                    {
                        optionNode = higherNodes ? optionNode.NextNode : optionNode.PrevNode;
                        otherOptionNode = higherNodes ? otherOptionNode.NextNode : otherOptionNode.PrevNode;

                        optionDelta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(optionNode.Instrument.LastPrice),
                        0.1, timeoftrade, Convert.ToDouble(bInstrumentPrice));
                        optionDelta = Math.Abs(optionDelta);

                        break;
                    }
                    if (!double.IsNaN(optionDelta) && !higherDelta && optionDelta < DELTA_ENTRY)
                    {
                        break;
                    }
                }
                else
                {
                    if ((higherNodes && higherDelta) || (!higherNodes && !higherDelta))
                    {
                        optionNode = optionNode.NextNode;
                        if (moveOtherNode)
                            otherOptionNode = otherOptionNode.NextNode;
                    }
                    else
                    {
                        optionNode = optionNode.PrevNode;
                        if (moveOtherNode)
                            otherOptionNode = otherOptionNode.PrevNode;
                    }
                    break;
                }
            }

        }


        /// <summary>
        /// This method will be called on non-trigerring node. and the idea will be to move node to get to target delta but always lower than Entry delta
        /// </summary>
        /// <param name="targetDelta"></param>
        /// <param name="optionNode"></param>
        /// <param name="higherNodes"></param>
        /// <param name="timeOfTrade"></param>
        /// <param name="bInstrumentPrice"></param>
        private void GetDeltaNeutralNode(double targetDelta, ref InstrumentListNode optionNode, bool higherNodes, DateTime? timeOfTrade, decimal bInstrumentPrice)
        {
            Instrument option = optionNode.Instrument;
            double previousOptionDelta = targetDelta = Math.Min(DELTA_ENTRY, targetDelta);
            while (true)
            {
                if (optionNode.Instrument.LastPrice != 0)
                {
                    double optionDelta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(optionNode.Instrument.LastPrice), 0.1, timeOfTrade, Convert.ToDouble(bInstrumentPrice));
                    optionDelta = Math.Abs(optionDelta);

                    if (!double.IsNaN(optionDelta))
                    {
                        if (optionDelta > DELTA_ENTRY)
                        {
                            //move back
                            optionNode = higherNodes ? optionNode.PrevNode : optionNode.NextNode;
                            previousOptionDelta = optionDelta;
                            continue;
                        }
                        else if (Math.Abs(targetDelta - optionDelta) < DELTA_NEUTRAL_BAND)// || optionDelta > targetDelta) //90% of upper delta
                        {
                            break;
                        }
                        else if ((previousOptionDelta < targetDelta && optionDelta > targetDelta) || (previousOptionDelta > targetDelta && optionDelta < targetDelta))
                        {
                            break;
                        }
                    }
                    // AssignNeighbouringNodes(optionNode, option.BaseInstrumentToken, option);

                    if ((higherNodes && (optionDelta < targetDelta)) || (!higherNodes && (optionDelta > targetDelta)))
                    {//Put                                                 //Call
                        if (optionNode.NextNode == null || optionNode.NextNode.Instrument.LastPrice == 0)
                            return;
                        else
                            optionNode = optionNode.NextNode;
                    }
                    else if ((!higherNodes && (optionDelta < targetDelta)) || (higherNodes && (optionDelta > targetDelta)))
                    { //Call                                                //Put
                        if (optionNode.PrevNode == null || optionNode.PrevNode.Instrument.LastPrice == 0)
                            return;
                        else
                            optionNode = optionNode.PrevNode;
                    }
                    previousOptionDelta = optionDelta;
                }
                else
                {
                    //move back
                    optionNode = higherNodes ? optionNode.PrevNode : optionNode.NextNode;
                    break;
                    //optionNode = optionNode == null ? higherNodes ? optionNode.NextNode : optionNode.PrevNode:optionNode;

                    //Check if next/prev node has value, if not then do not move and break
                }
            }

            #region Old While loop
            //while (true)
            //{
            //    if (optionNode.Instrument.LastPrice != 0)
            //    {
            //        //putDelta = subsequentPutNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentPutNode.Instrument.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(bInstrumentPrice));
            //        double optionDelta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(optionNode.Instrument.LastPrice), 0.1, timeOfTrade, Convert.ToDouble(bInstrumentPrice));
            //        optionDelta = Math.Abs(optionDelta);
            //        if (!double.IsNaN(optionDelta))
            //        {
            //            if (Math.Abs(targetDelta - optionDelta) < DELTA_NEUTRAL_BAND)// || optionDelta > targetDelta) //90% of upper delta
            //            {
            //                break;
            //            }
            //            else if ((currentDelta > targetDelta && optionDelta < targetDelta) || (currentDelta < targetDelta && optionDelta > targetDelta))
            //            {
            //                if (optionDelta > DELTA_ENTRY)
            //                {
            //                    //move back
            //                    optionNode = higherNodes ? optionNode.PrevNode : optionNode.NextNode;
            //                }
            //                break;
            //            }
            //        }
            //    }
            //    else
            //    {
            //        //   optionNode = higherNodes ? optionNode.PrevNode : optionNode.NextNode;

            //        if ((higherNodes && (currentDelta < targetDelta)) || (!higherNodes && (currentDelta > targetDelta)))
            //        {//Put                                                 //Call
            //            optionNode = optionNode.PrevNode;
            //        }
            //        else if ((!higherNodes && (currentDelta < targetDelta)) || (higherNodes && (currentDelta > targetDelta)))
            //        { //Call                                                //Put
            //            optionNode = optionNode.NextNode;
            //        }



            //        break;
            //    }

            //    //optionNode = higherNodes? optionNode.NextNode: optionNode.PrevNode;
            //    AssignNeighbouringNodes(optionNode, option.BaseInstrumentToken, option);

            //    if ((higherNodes && (currentDelta < targetDelta)) || (!higherNodes && (currentDelta > targetDelta)))
            //    {//Put                                                 //Call
            //        if (optionNode.NextNode == null)
            //            return;
            //        else
            //            optionNode = optionNode.NextNode;
            //    }
            //    else if ((!higherNodes && (currentDelta < targetDelta)) || (higherNodes && (currentDelta > targetDelta)))
            //    { //Call                                                //Put
            //        if (optionNode.PrevNode == null)
            //            return;
            //        else
            //            optionNode = optionNode.PrevNode;
            //    }

            //}
            #endregion
        }


        //    //double optionDelta;
        //    //if ((optionType == InstrumentType.CE && currentNodeDelta < DELTA_ENTRY) || (optionType == InstrumentType.PE && currentNodeDelta > DELTA_ENTRY))
        //    //{
        //    optionNode = optionNode.PrevNode;
        //        if (optionNode.Instrument.LastPrice != 0)
        //        {
        //            optionDelta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(optionNode.Instrument.LastPrice), 0.1, timeoftrade, Convert.ToDouble(bInstrumentPrice));
        //            optionDelta = Math.Abs(optionDelta);

        //            if (!double.IsNaN(optionDelta) && optionDelta > DELTA_ENTRY)
        //            {
        //                optionNode = optionNode.NextNode;
        //                break;
        //            }

        //        }
        //        else
        //        {
        //            optionNode = optionNode.NextNode;
        //            break;
        //        }
        //    }
        //    else if ((optionType == InstrumentType.PE && currentNodeDelta < DELTA_ENTRY) || (optionType == InstrumentType.CE && currentNodeDelta > DELTA_ENTRY))
        //    {
        //        optionNode = optionNode.NextNode;
        //        if (optionNode.Instrument.LastPrice != 0)
        //        {
        //            optionDelta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(optionNode.Instrument.LastPrice), 0.1, timeoftrade, Convert.ToDouble(bInstrumentPrice));
        //            optionDelta = Math.Abs(optionDelta);

        //            if (!double.IsNaN(optionDelta) && optionDelta < DELTA_ENTRY)
        //            {
        //                break;
        //            }

        //        }
        //        else
        //        {
        //            optionNode = optionNode.NextNode;
        //            break;
        //        }
        //    }







        //        if (currentNodeDelta < DELTA_ENTRY)
        //        {
        //            optionNode = optionNode.PrevNode;
        //            if (optionNode.Instrument.LastPrice != 0)
        //            {
        //                optionDelta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(optionNode.Instrument.LastPrice), 0.1, timeoftrade, Convert.ToDouble(bInstrumentPrice));
        //                optionDelta = Math.Abs(optionDelta);

        //                if (!double.IsNaN(optionDelta) && optionDelta > DELTA_ENTRY)
        //                {
        //                    optionNode = optionNode.NextNode;
        //                    break;
        //                }

        //            }
        //            else
        //            {
        //                optionNode = optionNode.NextNode;
        //                break;
        //            }
        //        }
        //        else
        //        {
        //            optionNode = optionNode.NextNode;
        //            if (optionNode.Instrument.LastPrice != 0)
        //            {
        //                optionDelta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(optionNode.Instrument.LastPrice), 0.1, timeoftrade, Convert.ToDouble(bInstrumentPrice));
        //                optionDelta = Math.Abs(optionDelta);

        //                if (!double.IsNaN(optionDelta) && optionDelta < DELTA_ENTRY)
        //                {
        //                    break;
        //                }

        //            }
        //            else
        //            {
        //                optionNode = optionNode.NextNode;
        //                break;
        //            }
        //        }
        //    }

        //    if (callDelta < DELTA_ENTRY)
        //    {
        //        subsequentCallNode = subsequentCallNode.PrevNode;

        //        if (subsequentCallNode.Instrument.LastPrice != 0)
        //        {
        //            // callDelta = subsequentCallNode.Call.UpdateDelta(Convert.ToDouble(subsequentCallNode.Instrument.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(baseInstrumentPrice));
        //            callDelta = subsequentCallNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentCallNode.Instrument.LastPrice), 0.1, ticks[0].Timestamp, Convert.ToDouble(bInstrumentPrice));
        //            callDelta = Math.Abs(callDelta);
        //            if (!double.IsNaN(callDelta) && callDelta > DELTA_ENTRY)
        //            {
        //                callNode = subsequentCallNode.NextNode;

        //                GetDeltaNeutralNode(callDelta, ref putNode, higherNodes, ticks[0].Timestamp, bInstrumentPrice);
        //                //while (true)
        //                //{
        //                //    subsequentPutNode = subsequentPutNode.NextNode;

        //                //    if (subsequentPutNode.Instrument.LastPrice != 0)
        //                //    {
        //                //        //putDelta = subsequentPutNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentPutNode.Instrument.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(bInstrumentPrice));
        //                //        putDelta = subsequentPutNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentPutNode.Instrument.LastPrice), 0.1, ticks[0].Timestamp, Convert.ToDouble(bInstrumentPrice));
        //                //        putDelta = Math.Abs(putDelta);
        //                //        if (!double.IsNaN(putDelta))
        //                //        {
        //                //            if (Math.Abs(callDelta - putDelta) < DELTA_NEUTRAL_BAND || putDelta > callDelta) //90% of upper delta
        //                //            {
        //                //                putNode = subsequentPutNode;
        //                //                break;
        //                //            }
        //                //        }
        //                //    }
        //                //}
        //                break;
        //            }
        //            else
        //            {
        //                continue;
        //            }
        //        }
        //        else
        //        {
        //            subsequentCallNode = subsequentCallNode.NextNode;
        //            callNode = subsequentCallNode;
        //        }
        //    }
        //}
        //private void GetDeltaNeutralNode(double targetDelta, ref InstrumentListNode optionNode, bool higherNodes, DateTime? timeOfTrade, decimal bInstrumentPrice)
        //{
        //    Instrument option = optionNode.Instrument;

        //    double currentDelta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(optionNode.Instrument.LastPrice), 0.1, timeOfTrade, Convert.ToDouble(bInstrumentPrice));
        //    currentDelta = Math.Abs(currentDelta);


        //    //Secondary nodes will come...higherNodes


        //    while (true)
        //    {
        //        if (optionNode.Instrument.LastPrice != 0)
        //        {
        //            //putDelta = subsequentPutNode.Instrument.UpdateDelta(Convert.ToDouble(subsequentPutNode.Instrument.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(bInstrumentPrice));
        //            double optionDelta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(optionNode.Instrument.LastPrice), 0.1, timeOfTrade, Convert.ToDouble(bInstrumentPrice));
        //            optionDelta = Math.Abs(optionDelta);
        //            if (!double.IsNaN(optionDelta))
        //            {
        //                if (Math.Abs(targetDelta - optionDelta) < DELTA_NEUTRAL_BAND)// || optionDelta > targetDelta) //90% of upper delta
        //                {
        //                    break;
        //                }
        //                else if ((currentDelta > targetDelta && optionDelta < targetDelta) || (currentDelta < targetDelta && optionDelta > targetDelta))
        //                {
        //                    if(optionDelta > DELTA_ENTRY)
        //                    {
        //                        //move back
        //                        optionNode = higherNodes ? optionNode.PrevNode : optionNode.NextNode;
        //                    }
        //                    break;
        //                }
        //            }
        //        }
        //        else
        //        {
        //            //   optionNode = higherNodes ? optionNode.PrevNode : optionNode.NextNode;

        //            if(!higherNodes) //Call
        //            {
        //                if(currentDelta < targetDelta)
        //                {

        //                }
        //            }

        //            if ((higherNodes && (currentDelta < targetDelta)) || (!higherNodes && (currentDelta > targetDelta)))
        //            {//Put                                                 //Call
        //                    optionNode = optionNode.PrevNode;
        //            }
        //            else if ((!higherNodes && (currentDelta < targetDelta)) || (higherNodes && (currentDelta > targetDelta)))
        //            { //Call                                                //Put
        //                    optionNode = optionNode.NextNode;
        //            }



        //            break;
        //        }

        //        //optionNode = higherNodes? optionNode.NextNode: optionNode.PrevNode;
        //        AssignNeighbouringNodes(optionNode, option.BaseInstrumentToken, option);

        //        if ((higherNodes && (currentDelta<targetDelta)) || (!higherNodes && (currentDelta > targetDelta)))
        //        {//Put                                                 //Call
        //            if (optionNode.NextNode == null)
        //                return;
        //            else
        //                optionNode = optionNode.NextNode;
        //        }
        //        else if ((!higherNodes && (currentDelta < targetDelta)) || (higherNodes && (currentDelta > targetDelta)))
        //        { //Call                                                //Put
        //            if (optionNode.PrevNode == null)
        //                return;
        //            else
        //                optionNode = optionNode.PrevNode;
        //        }

        //    }
        //}

        private void UpdateLastPriceOnAllNodes(InstrumentListNode currentNode, ref Tick[] ticks)
        {
            Instrument option;
            Tick optionTick;

            int currentNodeIndex = currentNode.Index;

            InstrumentListNode activeNode = currentNode;

            //go to the first node:
            while (activeNode.PrevNode != null)
            {
                activeNode = activeNode.PrevNode;
            }

            //Update all the price all the way till the end
            while (activeNode.NextNode != null)
            {
                option = activeNode.Instrument;
                optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == option.InstrumentToken);
                if (optionTick.LastPrice != 0)
                {
                    option.LastPrice = optionTick.LastPrice;
                    activeNode.Instrument = option;
                }
                activeNode = activeNode.NextNode;
            }
        }

        private void AssignNeighbouringNodes(InstrumentListNode currentNode, UInt32 baseInstrumentToken, Instrument option)
        {
            int? retrievalIndex = GetRetrievalIndex(currentNode);
            if (retrievalIndex == null)
                return;

            int currentIndex = currentNode.Index;

            DataLogic dl = new DataLogic();
            SortedList<Decimal, Instrument> nodeData = dl.RetrieveNextNodes(baseInstrumentToken, option.InstrumentType, option.Strike, option.Expiry.Value, retrievalIndex.Value);

            //Instrument currentInstrument = currentNode.Instrument;

            //SortedList<Decimal, Instrument> callNodeData = NodeData[0];
            //SortedList<Decimal, Instrument> putNodeData = NodeData[1];

            //int lowerNodeCount = NodeData.Count(x => x.Key < strangleNode.Instrument.Strike);
            //int lowerPutNodeCount = putNodeData.Count(x => x.Key < strangleNode.Put.Strike);
            //int lowerNodeCount = Math.Min(lowerCallNodeCount, lowerPutNodeCount);

            var lowerNodeData = nodeData.Where(x => x.Key < currentNode.Instrument.Strike).ToList();
            int lowerNodeCount = lowerNodeData.Count();
            //var lowerPutNodeData = putNodeData.Where(x => x.Key < strangleNode.Put.Strike).ToList();

            InstrumentListNode instrumentNodeL, instrumentNodeU;
            instrumentNodeL = instrumentNodeU =  currentNode;

            for (int i = lowerNodeCount-1; i >=0; i--)
            {
                InstrumentListNode lowerNode = new InstrumentListNode(lowerNodeData[--lowerNodeCount].Value);

                lowerNode.Index = instrumentNodeL.Index - 1;
                instrumentNodeL.PrevNode = lowerNode;
                lowerNode.NextNode = instrumentNodeL;

                instrumentNodeL = instrumentNodeL.PrevNode;
            }

            //instrumentNodeL = instrumentNode.GetNodebyIndex((Int16) currentIndex);

            var higherNodeData = nodeData.Where(x => x.Key > currentNode.Instrument.Strike).ToList();
            int higherNodeCount = higherNodeData.Count();

            for (int i = 0; i < higherNodeCount; i++)
            {
                InstrumentListNode higherNode = new InstrumentListNode(higherNodeData[i].Value);

                higherNode.Index = instrumentNodeU.Index + 1;
                instrumentNodeU.NextNode = higherNode;
                higherNode.PrevNode = instrumentNodeU;

                instrumentNodeU = instrumentNodeU.NextNode;
            }
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
                StrangleInstrumentListNode strangleTokenNode = null;
                InstrumentListNode callNode = null;
                InstrumentListNode putNode = null;

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

                    callNode = new InstrumentListNode(call);
                    putNode = new InstrumentListNode(put);

                    if (strangleTokenNode == null)
                    {
                        strangleTokenNode = new StrangleInstrumentListNode(callNode, putNode)
                        {
                            Index = (int)strangleTokenRow["InstrumentIndex"]
                        };
                        strangleTokenNode.DeltaThreshold = (double)strangleRow["StopLossPoints"];
                        strangleTokenNode.Prices = prices;
                    }
                    else
                    {
                        StrangleInstrumentListNode newNode = new StrangleInstrumentListNode(callNode, putNode)
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


                StrangleInstrumentListNode currentNode = strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]);


                StrangleInstrumentLinkedList strangleLinkedList = new StrangleInstrumentLinkedList(currentNode)
                {
                    CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
                    //MaxLossPoints = Math.Max((decimal)strangleRow["MaxLossPoints"], Math.Abs(currentNode.Prices.Last()) * 0.15m),
                    //MaxProfitPoints = Math.Max((decimal)strangleRow["MaxProfitPoints"], Math.Abs(currentNode.Prices.Last()) * 0.3m),
                    StopLossPoints = (double)strangleRow["StopLossPoints"],
                    NetPrice = (decimal)strangleRow["NetPrice"],
                    listID = strategyId,
                    BaseInstrumentToken = strangleTokenNode.CallNode.Instrument.BaseInstrumentToken
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
        private int? GetRetrievalIndex(InstrumentListNode strangleNode)
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

                    double stopLossPoints = Math.Min(Math.Max(callDelta, putDelta), DELTA_ENTRY) + SAFETY_MARGIN;


                    //Update Database
                    DataLogic dl = new DataLogic();
                    strangleId = dl.StoreStrangleData(ceToken: currentCE.InstrumentToken, peToken: currentPE.InstrumentToken, cePrice: cePrice,
                       pePrice: pePrice, bInstPrice:bPrice, algoIndex: AlgoIndex.StrangleWithConstantWidth, safetyMargin:10, stopLossPoints: stopLossPoints);
                }
            }
        }

        public virtual void OnCompleted()
        {
        }
        public virtual void OnError(Exception ex)
        {
            ///TODO: Log the error. Also handle the error.
        }
    }
}
