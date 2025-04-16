using Algorithms.Algorithms;
using Algorithms.Utilities.Views;
using Algorithms.Utilities;
using Algos.Utilities.Views;
using Global.Web;
using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace LocalDBData.Test
{
    internal class ActiveBuyStrangleWithVariableQtyTest : ITest
    {
        ActiveBuyStrangleManagerWithVariableQty paTrader;

        public void Execute(StrangleWithDeltaandLevelInputs paInputs)
        {
            paTrader = ExecuteAlgo(paInputs);

            paTrader.OnOptionUniverseChange += ObserverSubscription;
            //paTrader.OnCriticalEvents += SendNotification;// (string title, string body)
            paTrader.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
            paTrader.OnTradeExit += OptionSellwithRSI_OnTradeExit;

            //            Task task = Task.Run(() => NMQClientSubscription(paTrader, paInputs.BToken));
            //#if Local
            // Task observerSubscriptionTask = Task.Run(() => ObserverSubscription(paTrader));
            //            Task kftask = Task.Run(() => KFKClientSubscription(paTrader, paInputs.BToken));
            //#endif
        }

        public void OnNext(Tick tick)
        {
            paTrader.OnNext(tick);
        }
        public void StopTrade(bool stopTrade)
        {
            paTrader.StopTrade(stopTrade);
        }
        private void SendNotification(string title, string body)
        {

        }
        private ActiveBuyStrangleManagerWithVariableQty ExecuteAlgo(StrangleWithDeltaandLevelInputs strangleWithDeltaandLevelInputs)
        {
            uint instrumentToken = strangleWithDeltaandLevelInputs.BToken;
            DateTime expiry = strangleWithDeltaandLevelInputs.Expiry;

            decimal algoInstance = 0;
            StrangleWithDeltaandLevelInputs input = GetActiveAlgos(out algoInstance);
            
            if (input != null && input.Expiry.Date == expiry)
            {
                strangleWithDeltaandLevelInputs = input;
            }

            paTrader = new ActiveBuyStrangleManagerWithVariableQty(instrumentToken, expiry,
                strangleWithDeltaandLevelInputs.IQty, strangleWithDeltaandLevelInputs.StepQty, strangleWithDeltaandLevelInputs.MaxQty, strangleWithDeltaandLevelInputs.TP,
                strangleWithDeltaandLevelInputs.SL, strangleWithDeltaandLevelInputs.IDelta, strangleWithDeltaandLevelInputs.MaxDelta, strangleWithDeltaandLevelInputs.MinDelta,
                AlgoIndex.ActiveTradeWithVariableQty);
            return paTrader;
        }



        private void OptionSellwithRSI_OnTradeExit(Order st)
        {
            ////publish trade details and count
            ////Bind with trade token details, use that as an argument
            //OrderCore.PublishOrder(st);
            //Thread.Sleep(100);
        }

        private void OptionSellwithRSI_OnTradeEntry(Order st)
        {
            ////publish trade details and count
            //OrderCore.PublishOrder(st);
            //Thread.Sleep(100);
        }

        private void ObserverSubscription(ActiveBuyStrangleManagerWithVariableQty paTrader)
        {
            //GlobalObjects.ObservableFactory ??= new ObservableFactory();
            //paTrader.Subscribe(GlobalObjects.ObservableFactory);
        }

        //private async Task NMQClientSubscription(CandleWickScalping paTrader, uint token)
        //{
        //    zmqClient = new ZMQClient();
        //    zmqClient.AddSubscriber(new List<uint>() { token });

        //    await zmqClient.Subscribe(paTrader);
        //}

        public StrangleWithDeltaandLevelInputs GetActiveAlgos(out decimal algoInstance)
        {
                string userId = "PM27031981";
                DataLogic dl = new DataLogic();
                DataSet ds = dl.GetActiveAlgos(AlgoIndex.ActiveTradeWithVariableQty, userId);

                List<ActiveAlgosView> activeAlgos = new List<ActiveAlgosView>();
            StrangleWithDeltaandLevelInputs algoInput = null;
            if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
            {
                DataTable dtActiveAlgos = ds.Tables[0];
                DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                    new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

                DataRelation algo_orderTrios_relation = ds.Relations.Add("Algo_OrderTrios", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                    new DataColumn[] { ds.Tables[3].Columns["AlgoInstance"] });

                //DataRelation orderTrios_orders_relation = ds.Relations.Add("OrderTrios_Orders", new DataColumn[] { ds.Tables[3].Columns["MainOrderId"] },
                //    new DataColumn[] { ds.Tables[2].Columns["OrderId"] });
                
                if (dtActiveAlgos.Rows.Count > 0)
                {
                    DataRow drAlgo = dtActiveAlgos.Rows[0];

                    algoInput = new StrangleWithDeltaandLevelInputs
                    {
                        Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
                        CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
                        IQty = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
                        MaxQty = Convert.ToInt32(drAlgo["MaxQtyInLotSize"]),
                        StepQty = Convert.ToInt32(drAlgo["StepQtyInLotSize"]),
                        MinQty = Convert.ToInt32(drAlgo["lowerLimit"]),

                        BToken = Convert.ToUInt32(drAlgo["BToken"]),
                        UID = Convert.ToString(DBNull.Value != drAlgo["Arg9"] ? drAlgo["Arg9"] : 0),
                        //PS = Convert.ToBoolean(DBNull.Value != drAlgo["PositionSizing"] ? drAlgo["PositionSizing"] : false),
                        TP = Convert.ToDecimal(DBNull.Value != drAlgo["Arg1"] ? drAlgo["Arg1"] : 0),
                        SL = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0),
                        //IntD = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0) == 1,
                        PnL = Convert.ToDecimal(DBNull.Value != drAlgo["NetPrice"] ? drAlgo["NetPrice"] : 0)
                    };
                    //algosView.aid = Convert.ToInt32(drAlgo["AlgoId"]);
                    //algosView.an = Convert.ToString((AlgoIndex)algosView.aid);
                    //algosView.ains = Convert.ToInt32(drAlgo["Id"]);

                    //algosView.expiry = Convert.ToDateTime(drAlgo["Expiry"]).ToString("yyyy-MM-dd");
                    //algosView.mins = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]);
                    //algosView.lotsize = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]);
                    //algosView.binstrument = Convert.ToString(drAlgo["BToken"]);
                    //algosView.algodate = Convert.ToDateTime(drAlgo["Timestamp"]).ToString("yyyy-MM-dd");

                    DataRow[] drOrderTrios = drAlgo.GetChildRows(algo_orderTrios_relation);

                    //algoInput.CallOrder ??= new OrderTrio();
                    List<OrderTrio> orderTrios = new List<OrderTrio>();
                    foreach (DataRow drOrderTrio in drOrderTrios)
                    {
                        if (Convert.ToBoolean(drOrderTrio["Active"]))
                        {
                            OrderTrio orderTrio = new OrderTrio()
                            {
                                Id = Convert.ToInt32(drOrderTrio["Id"]),
                                //TargetProfit = Convert.ToDecimal(drOrderTrio["TargetProfit"]),
                                //StopLoss = Convert.ToDecimal(drOrderTrio["StopLoss"])
                            };

                            DataRow[] drOrders = drAlgo.GetChildRows(algo_orders_relation);
                            foreach (DataRow drOrder in drOrders)
                            {
                                if (Convert.ToString(drOrderTrio["MainOrderId"]) == Convert.ToString(drOrder["OrderId"]))
                                {
                                    Order o = ViewUtility.GetOrder(drOrder);

                                    switch (o.OrderType.ToUpper())
                                    {
                                        case Constants.ORDER_TYPE_MARKET:
                                            orderTrio.Order = o;
                                            break;

                                        case Constants.ORDER_TYPE_SLM:
                                            orderTrio.TPOrder = o;
                                            break;
                                    }
                                    //algosView.Orders.Add(ViewUtility.GetOrderView(o));
                                }
                            }
                            //Instrument option = dl.GetInstrument(orderTrio.Order.Tradingsymbol);
                            //orderTrio.Option = option;
                            orderTrios.Add(orderTrio);
                        }
                    }
                    //activeAlgos.Add(algosView);
                    algoInput.ActiveOrderTrios = orderTrios;

                    algoInstance = Convert.ToInt32(drAlgo["Id"]);
                }
            }
            algoInstance = 0;
            return algoInput;
        }
#if local
        private async Task KFKClientSubscription(CandleWickScalping paTrader, uint token)
        {
            kfkClient = new KSubscriber();
            kfkClient.AddSubscriber(new List<uint>() { token });

            await kfkClient.Subscribe(paTrader);
        }
        //private async Task ObserverSubscription(CandleWickScalping paTrader, uint token)
        //{
        //    GlobalObjects.ObservableFactory ??= new ObservableFactory();
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
        //private async Task MQTTCientSubscription(CandleWickScalping paTrader, uint token)
        //{
        //    mqttSubscriber = new MQTTSubscriber();
        //    mqttSubscriber.
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
#endif
        //        private void PATrade_OnOptionUniverseChange(CandleWickScalping source)
        //        {
        //            try
        //            {
        //                zmqClient.AddSubscriber(source.SubscriptionTokens);
        //#if local
        //                        kfkClient.AddSubscriber(source.SubscriptionTokens);
        //#endif
        //            }
        //            catch (Exception ex)
        //            {
        //                throw ex;

        //            }
        //        }
        //        [HttpGet("healthy")]
        //        public Task<int> Health()
        //        {
        //            return Task.FromResult((int)AlgoIndex.CandleWickScalping);
        //        }

        //        [HttpPut("{ain}")]
        //        public bool Put(int ain, [FromBody] int start)
        //        {
        //            List<CandleWickScalping> activeAlgoObjects;
        //            if (!_cache.TryGetValue(key, out activeAlgoObjects))
        //            {
        //                activeAlgoObjects = new List<CandleWickScalping>();
        //            }

        //            CandleWickScalping algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
        //            if (algoObject != null)
        //            {
        //                algoObject.StopTrade(!Convert.ToBoolean(start));
        //            }
        //            _cache.Set(key, activeAlgoObjects);

        //            return true;
        //        }
    }
}

