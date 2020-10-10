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
    public class ManagedStrangle : IObserver<Tick[]>
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

        public async virtual void OnNext(Tick[] ticks)
        {
            Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == _bInst.InstrumentToken);

            if(baseInstrumentTick.LastPrice != 0)
            {
                _bInst.LastPrice = baseInstrumentTick.LastPrice;
            }

            InstrumentListNode call = Calls.Current;
            Instrument currentCall = call.Instrument;
            

            //CALL PROCESS
            Tick currentCallTick = ticks.FirstOrDefault(x => x.InstrumentToken == currentCall.InstrumentToken);
            if (currentCallTick.LastPrice != 0)
            {
                currentCall.LastPrice = currentCallTick.LastPrice;
            }

            double currentCallDelta = currentCall.UpdateDelta(Convert.ToDouble(_bInst.LastPrice), 0.1); // currentCallTick.
            

            Task<InstrumentListNode> nextNode = null;
            //Task<InstrumentListNode> assignUpperCE = null;

            if (currentCallDelta > 0.7 * Calls.UpperDelta)
            {

                if (call.NextNode == null)
                    nextNode = AssignNextNodes(call, _bInst.InstrumentToken, "CE", currentCall.Strike, currentCall.Expiry.Value, true);

                //  AssignPartitions(nextNodes.Keys.ToList<int>());

                if (currentCallDelta > Calls.UpperDelta)
                {
                    //move to a higer calls
                    if (call.NextNode == null)
                    {
                            await nextNode;
                    }

                    //put P&L impact on node level on the linked list
                    decimal avgPrice = PlaceOrder(currentCall.TradingSymbol, buyOrder: true);
                    call.Prices.Add(avgPrice * -1); //buy prices are negative and sell are positive. as sign denotes cashflow direction

                    // call = call.NextNode;
                    call = Calls.Current = nextNode.Result;

                    avgPrice = PlaceOrder(currentCall.TradingSymbol, buyOrder: false);
                    call.Prices.Add(avgPrice); //buy prices are negative and sell are positive. as sign denotes cashflow direction



                    //Do we need current instrument index , when current instrument is already present in the linked list?
                    //It can see the index of current instrument using list.current.index
                    Calls.CurrentInstrumentIndex = call.Index;
                    //call = Calls.Current;
                    //Calls.
                    //make next symbol as next to current node. Also update PL data in linklist main
                    //also update currnetindex in the linklist main
                    DataLogic dl = new DataLogic();
                    dl.UpdateListData(Calls.listID, call.Instrument.InstrumentToken, call.Index, "CE");
                }
            }
            else if (currentCallDelta < 1.3 * Calls.LowerDelta)
            {
                ////assign lower calls
                //AssignPartitions();
                //if (currentCallDelta < celowerDelta)
                //{
                //    //move to a lower call
                //    decimal avgPrice = placeOrder(nextSymbol);
                //}

                if (call.PrevNode == null)
                    nextNode = AssignNextNodes(call, _bInst.InstrumentToken, "CE", currentCall.Strike, currentCall.Expiry.Value, false);

                //  AssignPartitions(nextNodes.Keys.ToList<int>());

                if (currentCallDelta < Calls.LowerDelta)
                {
                    //move to a higer put
                    if (call.PrevNode == null)
                    {
                            await nextNode;
                    }

                    //put P&L impact on node level on the linked list
                    decimal avgPrice = PlaceOrder(currentCall.TradingSymbol, buyOrder: true);
                    call.Prices.Add(avgPrice * -1); //buy prices are negative and sell are positive. as sign denotes cashflow direction

                    //make next symbol as next to current node. Also update PL data in linklist main
                    //also update currnetindex in the linklist main
                    // call = call.NextNode;
                    call = Calls.Current = nextNode.Result;

                    avgPrice = PlaceOrder(currentCall.TradingSymbol, buyOrder: false);
                    call.Prices.Add(avgPrice); //buy prices are negative and sell are positive. as sign denotes cashflow direction


                    //Do we need current instrument index , when current instrument is already present in the linked list?
                    //It can see the index of current instrument using list.current.index
                    Calls.CurrentInstrumentIndex = call.Index;

                    DataLogic dl = new DataLogic();
                    dl.UpdateListData(Calls.listID, currentCall.InstrumentToken, call.Index, "CE");
                }
            }


            //PUT PROCESS
            //Two seperate linked list are maintained with their incides on the linked list.
            InstrumentListNode put = Puts.Current;
            Instrument currentPut = put.Instrument;

            Tick currentPutTick = ticks.FirstOrDefault(x => x.InstrumentToken == currentPut.InstrumentToken);
            if (currentPutTick.LastPrice != 0)
            {
                currentPut.LastPrice = currentPutTick.LastPrice;
            }

            
            double currentPutDelta = currentPut.UpdateDelta(Convert.ToDouble(_bInst.LastPrice), 0.1);

            ///TODO: Should it be last price or 3 offer price
            //if (currentPutTick.LastPrice < lowerPuts.Value + stopLossPoints ||
            //    upperCallTick.LastPrice > upperCalls.Value + stopLossPoints)
            //{
            //    //move to different tick
            //}

            //Task<InstrumentListNode> _assignLowerPE = null;
            //Task<InstrumentListNode> assignUpperPE = null;
            

            if (currentPutDelta > 0.7 * Puts.UpperDelta)
            {
                //Start scanning lower puts

                if (put.PrevNode == null)
                    nextNode = AssignNextNodes(put, _bInst.InstrumentToken, "PE", currentPut.Strike, currentPut.Expiry.Value, false);

                //  AssignPartitions(nextNodes.Keys.ToList<int>());

                if (currentPutDelta > Puts.UpperDelta)
                {
                    //move to a lower put 
                    if (put.PrevNode == null)
                    {
                       await nextNode;
                    }


                    //put P&L impact on node level on the linked list
                    decimal avgPrice = PlaceOrder(put.Instrument.TradingSymbol, buyOrder: true);
                    put.Prices.Add(avgPrice * -1); //buy prices are negative and sell are positive. as sign denotes cashflow direction

                    //make next symbol as next to current node. Also update PL data in linklist main
                    //also update currnetindex in the linklist main
                    put = Puts.Current = nextNode.Result;

                    avgPrice = PlaceOrder(put.Instrument.TradingSymbol, buyOrder: false);
                    put.Prices.Add(avgPrice); //buy prices are negative and sell are positive. as sign denotes cashflow direction

                    Puts.CurrentInstrumentIndex = put.Index;
                    
                    DataLogic dl = new DataLogic();
                    dl.UpdateListData(Puts.listID, put.Instrument.InstrumentToken, put.Index, "PE");

                }
            }
            else if (currentPutDelta < 1.3 * Puts.LowerDelta)
            {
                //Start scanning lower puts

                if (put.NextNode == null)
                    nextNode = AssignNextNodes(put, _bInst.InstrumentToken, "PE", currentPut.Strike, currentPut.Expiry.Value, true);

                //  AssignPartitions(nextNodes.Keys.ToList<int>());

                if (currentPutDelta < Puts.LowerDelta)
                {
                    //move to a higer put
                    if (put.NextNode == null)
                    {
                       await nextNode;
                    }

                    //put P&L impact on node level on the linked list
                    decimal avgPrice = PlaceOrder(put.Instrument.TradingSymbol, buyOrder: true);
                    put.Prices.Add(avgPrice * -1); //buy prices are negative and sell are positive. as sign denotes cashflow direction

                    //make next symbol as next to current node. Also update PL data in linklist main
                    //also update currnetindex in the linklist main
                    put = Puts.Current = nextNode.Result;

                    avgPrice = PlaceOrder(put.Instrument.TradingSymbol, buyOrder: false);
                    put.Prices.Add(avgPrice); //buy prices are negative and sell are positive. as sign denotes cashflow direction

                    Puts.CurrentInstrumentIndex = put.Index;

                    DataLogic dl = new DataLogic();
                    dl.UpdateListData(Puts.listID, put.Instrument.InstrumentToken, put.Index, "PE");
                }

            }

            
        }

        public virtual void OnError(Exception ex)
        {
            ///TODO: Log the error. Also handle the error.
        }

        /// <summary>
        /// Currently Implemented only for delta range.
        /// </summary>
        /// <param name="bToken"></param>
        /// <param name="bValue"></param>
        /// <param name="peToken"></param>
        /// <param name="pevalue"></param>
        /// <param name="ceToken"></param>
        /// <param name="ceValue"></param>
        /// <param name="peLowerValue"></param>
        /// <param name="pelowerDelta"></param>
        /// <param name="peUpperValue"></param>
        /// <param name="peUpperDelta"></param>
        /// <param name="ceLowerValue"></param>
        /// <param name="celowerDelta"></param>
        /// <param name="ceUpperValue"></param>
        /// <param name="ceUpperDelta"></param>
        /// <param name="stopLossPoints"></param>
        public void ManageStrangle(Instrument bInst, Instrument currentPE, Instrument currentCE,
            decimal peLowerValue = 0, double pelowerDelta = 0, decimal peUpperValue = 0, double peUpperDelta = 0,
            decimal ceLowerValue = 0, double celowerDelta = 0, decimal ceUpperValue = 0, double ceUpperDelta = 0,
            decimal stopLossPoints = 0, int strangleId = 0)
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
                    put.Prices.Add(100); //put.SellPrice = 100;
                    put.Prices.Add(100);  // call.SellPrice = 100;

                    ///Uncomment below for real time orders
                    //put.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));
                    //call.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));
                    
                    //Update Database
                    DataLogic dl = new DataLogic();
                    strangleId = dl.StoreStrangleData(currentPE.InstrumentToken, currentCE.InstrumentToken, ceLowerValue, celowerDelta, ceUpperValue, ceUpperDelta, peLowerValue, pelowerDelta, peUpperValue, peUpperDelta, stopLossPoints = 0);
                }

                Calls = new InstrumentLinkedList(call);
                Puts = new InstrumentLinkedList(put);

                Calls.LowerDelta = celowerDelta;
                Calls.LowerPrice = ceLowerValue;
                Calls.UpperDelta = ceUpperDelta;
                Calls.UpperPrice = ceUpperValue;

                Puts.LowerDelta = pelowerDelta;
                Puts.LowerPrice = peLowerValue;
                Puts.UpperDelta = peUpperDelta;
                Puts.UpperPrice = peUpperValue;

                Calls.listID = Puts.listID = strangleId;

                //consumer = new Consumer(Constants.CONSUMER_GROUP);
                //dirve 

                //consumer.KConsumer.Subscribe("Market_Ticks");
                ///TODO: check if INT of Partition works fine with all Instrument Token

                _bInst = bInst;
                //_currentPE = currentPE;
                //_currentCE = currentCE;

                assignedTokens = new List<uint>();
                assignedTokens.Add(bInst.InstrumentToken);
                assignedTokens.Add(currentPE.InstrumentToken);
                assignedTokens.Add(currentCE.InstrumentToken);

                AssignPartitions();

            }
        }
        /// <summary>
        /// Pulls nodes data from database.
        /// </summary>
        /// <param name="instrumentType"></param>
        /// <param name="currentStrikePrice"></param>
        /// <param name="up"></param>
        async Task<InstrumentListNode> AssignNextNodes(InstrumentListNode currentNode, UInt32 baseInstrumentToken, string instrumentType, 
            decimal currentStrikePrice, DateTime expiry, bool up)
        {
            DataLogic dl = new DataLogic();
            Dictionary<UInt32, Instrument> NodeData = dl.RetrieveNextNodes(baseInstrumentToken, instrumentType, 
                currentStrikePrice, expiry, up);


            InstrumentListNode tempNode = currentNode;
            int currentIndex = currentNode.Index;
            ///TODO: is it getting sorted properly?
            ///It should be either assigned as next or prev depending on up parameter
            foreach (KeyValuePair<UInt32, Instrument> optionData in NodeData)
            {
                InstrumentListNode option = new InstrumentListNode(optionData.Value);

                if (up)
                {
                    currentNode.NextNode = option;
                    option.PrevNode = currentNode;
                    option.Index = currentNode.Index + 1;
                    currentNode = option;
                }
                else
                {
                    currentNode.PrevNode = option;
                    option.NextNode = currentNode;
                    option.Index = currentNode.Index - 1;
                    currentNode = option;

                }
            }
            assignedTokens.AddRange(NodeData.Keys.ToList());
            AssignPartitions();
            return up?tempNode.NextNode: tempNode.PrevNode;
        }
        
        private void AssignPartitions()//List<UInt32> tokens)
        {

            //Publisher publisher = new Publisher();
            //publisher.Subscribe(this);
            //Ticker ticker = new Ticker();
            //ticker.Subscribe(assignedTokens);

            //foreach (int token in assignedTokens)
            //{
            //    //subscribe for such tokens only
            //}


            //List<TopicPartition> topicPartitions = new List<TopicPartition>();
            //foreach (int token in tokens)
            //{
            //    topicPartitions.Add(new TopicPartition(Constants.TOPIC_NAME, new Partition((int)token)));
            //}
            //consumer.KConsumer.Assign(topicPartitions);
        }

        private decimal PlaceOrder(string Symbol, bool buyOrder)
        {
            //temp
            if(Symbol.Trim(' ').Substring(Symbol.Trim(' ').Length - 2, 2) == "CE")
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
