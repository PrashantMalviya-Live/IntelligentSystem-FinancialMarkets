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
using ZMQFacade;

namespace Algos.TLogics
{

    /// <summary>
    /// This algorithm sells in the money options and books loss and then wait for profit. 3 points loss and then expect 5 point profit
    /// </summary>
    public class FrequentBuySell : IZMQ// IObserver<Tick[]>
    {
        InstrumentListNode[] Strangle = new InstrumentListNode[2];

        List<UInt32> assignedTokens;

        public IDisposable UnsubscriptionToken;

        public decimal MaxProfitPoints;
        public decimal MaxLossPoints;
        Instrument _bInst;
        decimal SafetyBand = 10;
        int listID = 0;

        //public virtual void Subscribe(Publisher publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}
        ////public virtual void Subscribe(Ticker publisher)
        ////{
        ////    UnsubscriptionToken = publisher.Subscribe(this);
        ////}
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

        private async Task ReviewStrangle(Tick tick)
        {
            Tick[] ticks = new Tick[] { tick };
            InstrumentListNode ceNode = Strangle[(int)InstrumentType.CE];
            InstrumentListNode peNode = Strangle[(int)InstrumentType.PE];

            Instrument ce = ceNode.Instrument;
            Instrument pe = peNode.Instrument;

            /// ToDO: Remove this on the live environment as this will come with the tick data
            //-----------Start--------------
            if (ce.LastPrice == 0 || pe.LastPrice == 0)
            {
                DataLogic dl = new DataLogic();
                ce.LastPrice = dl.RetrieveLastPrice(ce.InstrumentToken);
                pe.LastPrice = dl.RetrieveLastPrice(pe.InstrumentToken);
            }
            //-----------END -------------

            _bInst.InstrumentToken = ce.BaseInstrumentToken;

            Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == _bInst.InstrumentToken);
            if (baseInstrumentTick.LastPrice != 0)
            {
                _bInst.LastPrice = baseInstrumentTick.LastPrice;
            }


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

            decimal orderAvgPrice = 0;
            if (ceNode.CurrentPosition == PositionStatus.Open && peNode.CurrentPosition == PositionStatus.Open)
            {
                if (ce.LastPrice > Math.Abs(ceNode.Prices.Last()) + MaxLossPoints)
                {
                    orderAvgPrice = PlaceOrder(ceNode, true, ce.LastPrice);

                    if (orderAvgPrice > 0)
                    {
                        ceNode.CurrentPosition = PositionStatus.Closed;
                    }
                }
                else if (pe.LastPrice > Math.Abs(peNode.Prices.Last()) + MaxLossPoints)
                {
                    orderAvgPrice = PlaceOrder(peNode, true, pe.LastPrice);

                    if (orderAvgPrice > 0)
                    {
                        peNode.CurrentPosition = PositionStatus.Closed;
                    }
                }
            }
            else if (ceNode.CurrentPosition == PositionStatus.Closed)
            {
                if (pe.LastPrice < Math.Abs(peNode.Prices.Last()) - MaxProfitPoints)
                {
                    orderAvgPrice = PlaceOrder(ceNode, false, ce.LastPrice);

                    if (orderAvgPrice > 0)
                    {
                        ceNode.CurrentPosition = PositionStatus.Open;

                        //do not place order in the system, but add the price for new reference
                        peNode.Prices.Add(pe.LastPrice * -1);
                        peNode.Prices.Add(pe.LastPrice);

                        //peNode.CurrentPosition = PositionStatus.Closed;
                    }
                }
                else if (ce.LastPrice < Math.Abs(ceNode.Prices.ElementAt(ceNode.Prices.Count - 2)))
                {
                    orderAvgPrice = PlaceOrder(ceNode, false, ce.LastPrice);

                    if (orderAvgPrice > 0)
                    {
                        ceNode.CurrentPosition = PositionStatus.Open;
                    }
                }
            }

            else if (peNode.CurrentPosition == PositionStatus.Closed)
            {
                if (ce.LastPrice < Math.Abs(ceNode.Prices.Last()) - MaxProfitPoints)
                {
                    orderAvgPrice = PlaceOrder(peNode, false, pe.LastPrice);
                    

                    if (orderAvgPrice > 0)
                    {
                        //ceNode.CurrentPosition = PositionStatus.Closed;
                        peNode.CurrentPosition = PositionStatus.Open;

                        //do not place order in the system, but add the price for new reference
                        ceNode.Prices.Add(ce.LastPrice * -1);
                        ceNode.Prices.Add(ce.LastPrice);
                    }
                }
                else if (pe.LastPrice < Math.Abs(peNode.Prices.ElementAt(peNode.Prices.Count - 2)))
                {
                    orderAvgPrice = PlaceOrder(peNode, false, pe.LastPrice);

                    if (orderAvgPrice > 0)
                    {
                        peNode.CurrentPosition = PositionStatus.Open;
                    }
                }
            }
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
        public virtual void OnNext(Tick tick)
        {
            ReviewStrangle(tick);

            return;
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
                    Strangle[(int)InstrumentType.PE].Prices.Add(currentPE.LastPrice); //put.SellPrice = 100;
                    Strangle[(int)InstrumentType.CE].Prices.Add(currentCE.LastPrice);  // call.SellPrice = 100;

                    Strangle[(int)InstrumentType.CE].CurrentPosition = PositionStatus.Open;
                    Strangle[(int)InstrumentType.PE].CurrentPosition = PositionStatus.Open;

                    ///Uncomment below for real time orders
                    //put.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));
                    //call.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));

                    MaxProfitPoints = maxProfitPoints;
                    MaxLossPoints = maxLossPoints;

                    //Update Database
                    DataLogic dl = new DataLogic();
                    listID = strangleId = dl.StoreStrangleData(currentCE.InstrumentToken, currentPE.InstrumentToken, Strangle[(int)InstrumentType.CE].Prices.Last(),
                       Strangle[(int)InstrumentType.PE].Prices.Last(), AlgoIndex.FrequentBuySell, maxProfitPoints, maxLossPoints, maxProfitPoints, maxLossPoints,
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

        public void StopTrade(bool stop)
        {
            //_stopTrade = stop;
        }
        private decimal PlaceOrder(InstrumentListNode optionNode, bool buyOrder, decimal currentPrice)
        {
            //temp
            Instrument option = optionNode.Instrument;

            //Dictionary<string, dynamic> orderStatus;

            //orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, Symbol,
            //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_BUY, 75, Product: Constants.PRODUCT_MIS,
            //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            //string orderId = orderStatus["data"]["order_id"];
            //List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
            //return orderInfo[orderInfo.Count - 1].AveragePrice;

            decimal averagePrice = currentPrice * (buyOrder ? -1 : 1);
            //make next symbol as next to current node. Also update PL data in linklist main
            //also update currnetindex in the linklist main
            DataLogic dl = new DataLogic();
            dl.UpdateOptionData(listID, option.InstrumentToken, option.InstrumentType,
               optionNode.Prices.Last(), averagePrice, AlgoIndex.FrequentBuySell);


            optionNode.Prices.Add(averagePrice);

            return Math.Abs(averagePrice);
        }
    }
}
