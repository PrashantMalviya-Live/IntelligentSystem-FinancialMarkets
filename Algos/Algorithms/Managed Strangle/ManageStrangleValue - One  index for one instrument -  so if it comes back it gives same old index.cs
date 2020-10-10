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

namespace AdvanceAlgos.Algorithms
{
    public class ManageStrangleValue : IObserver<Tick[]>
    {
        //LinkedList<decimal> lowerPuts;
        //LinkedList<decimal> upperCalls;

        InstrumentLinkedList Puts;
        InstrumentLinkedList Calls;
        // Consumer consumer;
        List<UInt32> assignedTokens;

        //public List<UInt32> ITokensToSubscribe { get; set; }
        public IDisposable UnsubscriptionToken;


        //Variable set based on actual data

        Instrument _bInst;
        //Instrument _currentPE;
        //Instrument _currentCE;

        //decimal _peLowerValue, _peUpperValue, _ceLowerValue, _ceUpperValue;
        //double _pelowerDelta, _peUpperDelta, _celowerDelta, _ceUpperDelta;
        //decimal _stopLossPoints;


        public virtual void Subscribe(Publisher publisher)
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

        private async Task ReviewStrangle(InstrumentLinkedList optionList, Tick[] ticks)
        {
            try
            {
                Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == _bInst.InstrumentToken);
                if (baseInstrumentTick.LastPrice != 0)
                {
                    _bInst.LastPrice = baseInstrumentTick.LastPrice;
                }

                InstrumentListNode optionNode = optionList.Current;
                Instrument currentOption = optionNode.Instrument;

                Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == currentOption.InstrumentToken);
                if (optionTick.LastPrice != 0)
                {
                    currentOption.LastPrice = optionTick.LastPrice;
                    optionList.Current.Instrument = currentOption;
                }


                // Task<InstrumentListNode> nextNodes = null;
                Task<bool> nodelistformed = null;
                //bool nodelistformed = false;
                // InstrumentListNode subsequentNode = null;

                //Pulling next nodes from the begining and keeping it up to date from the ticks. This way next nodes will be available when needed, as Tick table takes time.
                //Doing it on Async way
                if (optionNode.NextNode == null || optionNode.PrevNode == null)
                    nodelistformed = AssignNextNodes(optionNode, _bInst.InstrumentToken, currentOption.InstrumentType, currentOption.Strike, currentOption.Expiry.Value);


                /////Relatively slower task kept after asnyc method call
                /////TODO: REMOVE DATE TIME PARAMETER FROM THE UPDATE DELTA METHOD IN CASE OF LIVE RUNNING. THIS WAS DONE TO BACKTEST
                //double optionDelta = currentOption.UpdateDelta(Convert.ToDouble(_bInst.LastPrice), 0.1, ticks[0].Timestamp);

                //if (nodelistformed && nodelistformed.IsCompleted)
                //{
                // optionNode = nextNodes.Result;
                UpdateLastPriceOnAllNodes(ref optionNode, ref ticks);
                //optionList.Current = optionNode;
                //}


                if ((optionNode.Prices.Last() > optionList.MaxProfitPoints) &&
                    (currentOption.LastPrice < optionNode.Prices.Last() - optionList.MaxProfitPoints || currentOption.LastPrice > optionNode.Prices.Last() + optionList.MaxLossPoints))
                {
                    if (nodelistformed != null)
                    {
                        await nodelistformed;
                        UpdateLastPriceOnAllNodes(ref optionNode, ref ticks);
                    }


                    //put P&L impact on node level on the linked list
                    //decimal previousNodeAvgPrice = PlaceOrder(currentOption.TradingSymbol, buyOrder: true);
                    optionNode.Prices.Add(PlaceOrder(currentOption.TradingSymbol, buyOrder: true) * -1); //buy prices are negative and sell are positive. as sign denotes cashflow direction

                    decimal previousNodeAvgPrice = optionNode.Prices.Skip(optionNode.Prices.Count - 2).Sum();

                    uint previousInstrumentToken = currentOption.InstrumentToken;
                    int previousInstrumentIndex = optionNode.Index;
                    //Logic to determine the next node and move there
                    optionNode = await MoveToSubsequentNode(optionNode, currentOption.LastPrice, optionList.MaxProfitPoints, optionList.MaxLossPoints, ticks);



                    //if (subsequentNode == null)
                    //{
                    //    await nodelistformed;
                    //    //optionNode = nextNodes.Result; //this is still the current node

                    //    UpdateLastPriceOnAllNodes(ref optionNode, ref ticks);
                    //}


                    //optionList.Current = optionNode;
                    currentOption = optionNode.Instrument;
                    optionList.Current = optionNode;

                    optionNode.Prices.Add(PlaceOrder(currentOption.TradingSymbol, buyOrder: false));
                    decimal currentNodeAvgPrice = optionNode.Prices.Last(); //buy prices are negative and sell are positive. as sign denotes cashflow direction

                    //Do we need current instrument index , when current instrument is already present in the linked list?
                    //It can see the index of current instrument using list.current.index
                    optionList.CurrentInstrumentIndex = optionNode.Index;



                    //make next symbol as next to current node. Also update PL data in linklist main
                    //also update currnetindex in the linklist main
                    DataLogic dl = new DataLogic();
                     dl.UpdateListData(optionList.listID, currentOption.InstrumentToken, optionNode.Index, previousInstrumentIndex, currentOption.InstrumentType,
                        previousInstrumentToken, currentNodeAvgPrice, previousNodeAvgPrice, AlgoIndex.PriceStrangle);
                }
            }
            catch (Exception exp)
            {
                //throw exp;
            }
        }
        private async Task<InstrumentListNode> MoveToSubsequentNode(InstrumentListNode optionNode, decimal optionPrice, decimal maxProfitPoints, decimal maxLossPoints, Tick[] ticks)
        {
            bool nodeFound = false;
            Instrument currentOption = optionNode.Instrument;
            decimal initialSellingPrice = optionNode.Prices.First();
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
                await AssignNextNodes(optionNode, currentOption.BaseInstrumentToken, currentOption.InstrumentType, currentOption.Strike, currentOption.Expiry.Value);
                optionNode = await MoveToSubsequentNode(optionNode, optionPrice, maxProfitPoints, maxLossPoints, ticks);
            }

            return optionNode;

        }


        //make sure ref is working with struct . else make it class
        public async virtual void OnNext(Tick[] ticks)
        {
          ReviewStrangle(Calls, ticks);
             ReviewStrangle(Puts, ticks);
        }

        private void LoadActiveStrangles()
        {
            InstrumentLinkedList[] strangleList = new InstrumentLinkedList[2] {
                new InstrumentLinkedList(),
                new InstrumentLinkedList()
            };

            DataLogic dl = new DataLogic();
             dl.RetrieveActiveStrangles();


             public class InstrumentLinkedList
        {
            public InstrumentLinkedList(InstrumentListNode instrument)
            {
                Current = instrument;
            }
            public int listID;

            public decimal MaxLossPoints { get; set; }
            public decimal MaxProfitPoints { get; set; }
            public double LowerDelta { get; set; }
            public double UpperDelta { get; set; }
            public decimal StopLossPoints { get; set; }

            public int CurrentInstrumentIndex { get; set; }
            public InstrumentListNode Current { get; set; }
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
        public void ManageStrangle(Instrument bInst, Instrument currentPE, Instrument currentCE,
            decimal peMaxProfitPoints = 0, decimal peMaxLossPoints = 0, decimal ceMaxProfitPoints = 0, 
            decimal ceMaxLossPoints = 0, decimal stopLossPoints = 0, int strangleId = 0)
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
                    //TEMP -> First price
                    put.Prices.Add(currentPE.LastPrice); //put.SellPrice = 100;
                    call.Prices.Add(currentCE.LastPrice);  // call.SellPrice = 100;

                    ///Uncomment below for real time orders
                    //put.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));
                    //call.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));

                    //Update Database
                    DataLogic dl = new DataLogic();
                    strangleId = dl.StoreStrangleData(currentCE.InstrumentToken, currentPE.InstrumentToken, call.Prices.Sum(),
                        put.Prices.Sum(), AlgoIndex.PriceStrangle, ceMaxProfitPoints, ceMaxLossPoints, peMaxProfitPoints, peMaxLossPoints, stopLossPoints = 0);
                }

                Calls = new InstrumentLinkedList(call);
                Puts = new InstrumentLinkedList(put);

                Calls.MaxProfitPoints = ceMaxProfitPoints;
                Calls.MaxLossPoints = ceMaxLossPoints;

                Puts.MaxProfitPoints = peMaxProfitPoints;
                Puts.MaxLossPoints = peMaxLossPoints;

                Calls.listID = Puts.listID = strangleId;

                _bInst = bInst;
            }
        }

        async Task<bool> AssignNextNodesWithDelta(InstrumentListNode currentNode, UInt32 baseInstrumentToken, string instrumentType,
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
        async Task<bool> AssignNextNodes(InstrumentListNode currentNode, UInt32 baseInstrumentToken, string instrumentType,
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

        private decimal PlaceOrder(string Symbol, bool buyOrder)
        {
            //temp
            if (Symbol.Trim(' ').Substring(Symbol.Trim(' ').Length - 2, 2) == "CE")
                return Calls.Current.Instrument.LastPrice;
            else
                return Puts.Current.Instrument.LastPrice;

            //Dictionary<string, dynamic> orderStatus;

            //orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, Symbol,
            //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_BUY, 75, Product: Constants.PRODUCT_MIS,
            //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            //string orderId = orderStatus["data"]["order_id"];
            //List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
            //return orderInfo[orderInfo.Count - 1].AveragePrice;
        }
    }
}
