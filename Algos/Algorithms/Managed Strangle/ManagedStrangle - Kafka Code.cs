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
namespace AdvanceAlgos.Algorithms
{
    public class ManagedStrangle : IObserver <Tick[]>
    {
        //LinkedList<decimal> lowerPuts;
        //LinkedList<decimal> upperCalls;

        InstrumentLinkedList Puts;
        InstrumentLinkedList Calls;
        Consumer consumer;
        List<UInt32> assignedTokens;

        public List<UInt32> ITokensToSubscribe { get; set; }
        public IDisposable UnsubscriptionToken;


        //Variable set based on actual data

        UInt32 bToken;
        Instrument currentPE;
        Instrument currentCE;

        decimal peLowerValue, peUpperValue, ceLowerValue,ceUpperValue;
        double pelowerDelta, peUpperDelta, celowerDelta, ceUpperDelta;
        decimal stopLossPoints;


        public virtual void Subscribe(Publisher publisher)
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

        public virtual void OnNext(Tick[] ticks)
        {
            //using (consumer.KConsumer)
            //{
                

                //consumer.KConsumer.Assign(new List<TopicPartition> {
                //new TopicPartition("Market_Ticks", new Partition((int)bToken)),
                //new TopicPartition("Market_Ticks", new Partition((int)peToken)),
                //new TopicPartition("Market_Ticks", new Partition((int)ceToken))});

             //  TickDataSchema dataSchema = new TickDataSchema();
                //int offset;
                try
                {
                    while (true)
                    {
                        try
                        {
                            ///TODO: What should be the time span for exit. Current put as 2 hours
                            var consumeResult = consumer.KConsumer.Consume(TimeSpan.FromHours(2));

                            //byte[] ticks = consumeResult.Value;
                            UInt32 itoken = consumeResult.Key;

                            Tick baseInstrumentTick = new Tick();
                            if (itoken == bToken)
                            {
                                offset = 0;
                                baseInstrumentTick = dataSchema.ReadFull(ticks, ref offset);
                            }

                            Tick currentPutTick = new Tick();// Tick.FromBinary(ticks[0]);
                            if (itoken == currentPE.InstrumentToken)
                            {
                                offset = 0;
                                currentPutTick = dataSchema.ReadFull(ticks, ref offset);
                            }


                            Tick currentCallTick = new Tick();// 100;

                            if (itoken == currentCE.InstrumentToken)
                            {
                                offset = 0;
                                currentCallTick = dataSchema.ReadFull(ticks, ref offset);
                            }



                            ///This need not be a low latency logic but will be better to display delta live
                            currentPE.LastPrice = currentPutTick.LastPrice;
                            currentCE.LastPrice = currentCallTick.LastPrice;
                            double currentPutDelta = currentPE.UpdateDelta(Convert.ToDouble(baseInstrumentTick.LastPrice), 0.1);
                            double currentCallDelta = currentCE.UpdateDelta(Convert.ToDouble(baseInstrumentTick.LastPrice), 0.1); // currentCallTick.

                            ///TODO: Should it be last price or 3 offer price
                            //if (currentPutTick.LastPrice < lowerPuts.Value + stopLossPoints ||
                            //    upperCallTick.LastPrice > upperCalls.Value + stopLossPoints)
                            //{
                            //    //move to different tick
                            //}


                            //bool assignPartitions = false;

                            Task<InstrumentListNode> assignLowerPE = null;
                            Task<InstrumentListNode> assignLowerCE = null;
                            Task<InstrumentListNode> assignUpperPE = null;
                            Task<InstrumentListNode> assignUpperCE = null;

                            if (currentPutDelta > 0.7 * peUpperDelta)
                            {
                                //Start scanning lower puts

                                if (put.PrevNode == null)
                                    assignLowerPE = AssignNextNodes(put, bToken, "PE", currentPE.Strike, currentPE.Expiry.Value, false);

                                //  AssignPartitions(nextNodes.Keys.ToList<int>());

                                if (currentPutDelta > peUpperDelta)
                                {
                                    //move to a lower put 
                                    if (put.PrevNode == null)
                                    {
                                        await assignLowerPE;
                                    }


                                    //put = put.PrevNode;

                                    //make next symbol as next to current node. Also update PL data in linklist main
                                    //also update currnetindex in the linklist main
                                    // tempNode = put;
                                    Puts.Current = put.PrevNode;
                                    put = Puts.Current;

                                    decimal avgPrice = placeOrder(put.Instrument.TradingSymbol);

                                    DataLogic dl = new DataLogic();
                                    dl.UpdateListData(Puts.listID, put.Instrument.InstrumentToken, "PE");

                                }
                            }
                            else if (currentPutDelta < 1.3 * pelowerDelta)
                            {
                                //Start scanning lower puts

                                if (put.NextNode == null)
                                    assignUpperPE = AssignNextNodes(put, bToken, "PE", currentPE.Strike, currentPE.Expiry.Value, true);

                                //  AssignPartitions(nextNodes.Keys.ToList<int>());

                                if (currentPutDelta < pelowerDelta)
                                {
                                    //move to a higer put
                                    if (put.NextNode == null)
                                    {
                                        await assignUpperPE;
                                    }


                                    //make next symbol as next to current node. Also update PL data in linklist main
                                    //also update currnetindex in the linklist main
                                    //put = put.NextNode;
                                    Puts.Current = put.NextNode;
                                    put = Puts.Current;

                                    decimal avgPrice = placeOrder(put.Instrument.TradingSymbol);

                                    DataLogic dl = new DataLogic();
                                    dl.UpdateListData(Puts.listID, put.Instrument.InstrumentToken, "PE");
                                }

                            }

                            if (currentCallDelta > 0.7 * ceUpperDelta)
                            {

                                if (call.NextNode == null)
                                    assignUpperCE = AssignNextNodes(call, bToken, "CE", currentCE.Strike, currentCE.Expiry.Value, true);

                                //  AssignPartitions(nextNodes.Keys.ToList<int>());

                                if (currentCallDelta > ceUpperDelta)
                                {
                                    //move to a higer calls
                                    if (call.NextNode == null)
                                    {
                                        await assignUpperCE;
                                    }
                                    decimal avgPrice = placeOrder(call.Instrument.TradingSymbol);

                                    // call = call.NextNode;
                                    Calls.Current = call.NextNode;
                                    call = Calls.Current;
                                    //make next symbol as next to current node. Also update PL data in linklist main
                                    //also update currnetindex in the linklist main
                                    DataLogic dl = new DataLogic();
                                    dl.UpdateListData(Calls.listID, call.Instrument.InstrumentToken, "CE");
                                }
                            }
                            else if (currentCallDelta < 1.3 * celowerDelta)
                            {
                                ////assign lower calls
                                //AssignPartitions();
                                //if (currentCallDelta < celowerDelta)
                                //{
                                //    //move to a lower call
                                //    decimal avgPrice = placeOrder(nextSymbol);
                                //}


                                if (call.PrevNode == null)
                                    assignLowerCE = AssignNextNodes(call, bToken, "CE", currentCE.Strike, currentCE.Expiry.Value, false);

                                //  AssignPartitions(nextNodes.Keys.ToList<int>());

                                if (currentCallDelta < celowerDelta)
                                {
                                    //move to a higer put
                                    if (call.NextNode == null)
                                    {
                                        await assignLowerCE;
                                    }


                                    //make next symbol as next to current node. Also update PL data in linklist main
                                    //also update currnetindex in the linklist main

                                    Calls.Current = call.PrevNode;
                                    call = Calls.Current;

                                    decimal avgPrice = placeOrder(call.Instrument.TradingSymbol);
                                    DataLogic dl = new DataLogic();
                                    dl.UpdateListData(Calls.listID, call.Instrument.InstrumentToken, "CE");


                                }
                            }

                            if (consumeResult.Offset % 5 == 0)
                            {
                                try
                                {
                                    consumer.KConsumer.Commit(consumeResult);
                                }
                                catch (KafkaException e)
                                {
                                    Console.WriteLine($"Commit error: {e.Error.Reason}");
                                }
                            }
                        }
                        catch (ConsumeException e)
                        {
                            Console.WriteLine($"Consume error: {e.Error.Reason}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Closing consumer.");
                    consumer.KConsumer.Close();
                }
                finally
                {
                    consumer.KConsumer.Close();
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
        public async Task ManageStrangle(UInt32 bToken, Instrument currentPE, Instrument currentCE,
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
                Puts = new InstrumentLinkedList(put);

                InstrumentListNode call = new InstrumentListNode(currentCE);
                Calls = new InstrumentLinkedList(call);

                //InstrumentListNode tempNode;

                //If new strangle, place the order and update the data base. If old strangle monitor it.
                if (strangleId == 0)
                {

                    //TEMP
                    put.SellPrice = 100;
                    call.SellPrice = 100;
                    
                    ///Uncomment below for real time orders
                    //put.SellPrice = placeOrder(currentPE.TradingSymbol);
                    //call.SellPrice = placeOrder(currentCE.TradingSymbol);


                    //Update Database
                    DataLogic dl = new DataLogic();
                    strangleId = dl.StoreStrangleData(currentPE.InstrumentToken,currentCE.InstrumentToken, ceLowerValue, celowerDelta, ceUpperValue, ceUpperDelta, peLowerValue, pelowerDelta, peUpperValue ,peUpperDelta , stopLossPoints=0 );
                   // dl.UpdateListData(0, currentCE.InstrumentToken, "CE");
                }

                //consumer = new Consumer(Constants.CONSUMER_GROUP);
                //dirve 

                //consumer.KConsumer.Subscribe("Market_Ticks");
                ///TODO: check if INT of Partition works fine with all Instrument Token

                assignedTokens.Add(bToken);
                assignedTokens.Add(currentPE.InstrumentToken);
                assignedTokens.Add(currentCE.InstrumentToken);

                AssignPartitions(assignedTokens);

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
            ///TODO: is it getting sorted properly?
            ///It should be either assigned as next or prev depending on up parameter
            foreach (KeyValuePair<UInt32, Instrument> optionData in NodeData)
            {
                InstrumentListNode option = new InstrumentListNode(optionData.Value);

                if (up)
                {
                    currentNode.NextNode = option;
                    option.PrevNode = currentNode;
                    currentNode = option;
                }
                else
                {
                    currentNode.PrevNode = option;
                    option.NextNode = currentNode;
                    currentNode = option;

                }
            }
            assignedTokens.AddRange(NodeData.Keys.ToList());
            AssignPartitions(assignedTokens);
            return tempNode;
        }
        
        private void AssignPartitions(List<UInt32> tokens)
        {

            Publisher publisher = new Publisher();
            publisher.Subscribe(this);
            //List<TopicPartition> topicPartitions = new List<TopicPartition>();
            //foreach (int token in tokens)
            //{
            //    topicPartitions.Add(new TopicPartition(Constants.TOPIC_NAME, new Partition((int)token)));
            //}
            //consumer.KConsumer.Assign(topicPartitions);
        }

        private decimal placeOrder(string nextSymbol)
        {
            Dictionary<string, dynamic> orderStatus;

            orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, nextSymbol,
                                      Constants.TRANSACTION_TYPE_SELL, 75, Product: Constants.PRODUCT_MIS,
                                      OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            string orderId = orderStatus["data"]["order_id"];
            List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
            return orderInfo[orderInfo.Count - 1].AveragePrice;
        }
    }
}
