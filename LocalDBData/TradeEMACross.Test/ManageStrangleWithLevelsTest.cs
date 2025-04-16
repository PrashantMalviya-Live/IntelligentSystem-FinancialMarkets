using Algorithms.Algorithms;
using Algorithms.Utilities.Views;
using Algorithms.Utilities;
using Algos.Utilities.Views;
using GlobalLayer;
using Google.Apis.Http;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using ZMQFacade;

namespace LocalDBData.Test
{
    public class ManageStrangleWithLevelsTest : ITest
    {
        IZMQ paTrader;
        public void Execute(StrangleWithDeltaandLevelInputs paInputs)
        {
            StrangleWithDeltaandLevelInputs input = GetActiveAlgos();

            if (paInputs.IntraDay)
            {
                paTrader =
                  new ManageStrangleValue(new TimeSpan(0, paInputs.CTF, 0), paInputs.BToken, paInputs.Expiry, paInputs.CurrentDate,
                  paInputs.L1, paInputs.L2, paInputs.U1, paInputs.U2, paInputs.IQty, paInputs.StepQty, paInputs.MaxQty,
                  paInputs.IDelta, paInputs.MinDelta, paInputs.MaxDelta, paInputs.UID, paInputs.TP, paInputs.SL,
                  httpClientFactory: null);

                (paTrader as ManageStrangleValue).OnOptionUniverseChange += ObserverSubscription;
                (paTrader as ManageStrangleValue).OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
                (paTrader as ManageStrangleValue).OnTradeExit += OptionSellwithRSI_OnTradeExit;
            }
            else
            {
                paInputs = input == null? paInputs:input;
                paTrader =
                    new ManageStrangleWithLevels(new TimeSpan(0, paInputs.CTF, 0), paInputs.BToken, paInputs.Expiry, paInputs.CurrentDate,
                    paInputs.L1, paInputs.L2, paInputs.U1, paInputs.U2, paInputs.IQty, paInputs.StepQty, paInputs.MaxQty,
                    paInputs.IDelta, paInputs.MinDelta, paInputs.MaxDelta, paInputs.UID, paInputs.TP, paInputs.SL, paInputs.AID,
                    httpClientFactory: null);

                (paTrader as ManageStrangleWithLevels).OnOptionUniverseChange += ObserverSubscription;
                (paTrader as ManageStrangleWithLevels).OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
                (paTrader as ManageStrangleWithLevels).OnTradeExit += OptionSellwithRSI_OnTradeExit;

                (paTrader as ManageStrangleWithLevels).LoadActiveOrders(paInputs.ActiveOrderTrios);
            }
        }

        public void OnNext(Tick tick)
        {
            paTrader.OnNext(tick);
        }
        public void StopTrade(bool stopTrade)
        {
            paTrader.StopTrade(stopTrade);
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

        public StrangleWithDeltaandLevelInputs GetActiveAlgos()
        {
            try
            {
                string userId = "PM27031981";
                DataLogic dl = new DataLogic();
                DataSet ds = dl.GetActiveAlgos(AlgoIndex.DeltaStrangleWithLevels, userId);


                DataTable dtActiveAlgos = ds.Tables[0];
                DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                    new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

                DataRelation algo_orderTrios_relation = ds.Relations.Add("Algo_OrderTrios", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                    new DataColumn[] { ds.Tables[3].Columns["AlgoInstance"] });

                //DataRelation orderTrios_orders_relation = ds.Relations.Add("OrderTrios_Orders", new DataColumn[] { ds.Tables[3].Columns["MainOrderId"] },
                //    new DataColumn[] { ds.Tables[2].Columns["OrderId"] });
                StrangleWithDeltaandLevelInputs algoInput = null;



                foreach (DataRow drAlgo in dtActiveAlgos.Rows)
                {
                    //ActiveAlgosView algosView = new ActiveAlgosView();

                    algoInput = new StrangleWithDeltaandLevelInputs
                    {
                        AID = Convert.ToInt32(drAlgo["Id"]),
                        Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
                        CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
                        IQty = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
                        BToken = Convert.ToUInt32(drAlgo["BToken"]),
                        UID = Convert.ToString(DBNull.Value != drAlgo["Arg9"] ? drAlgo["Arg9"] : 0),
                        //PS = Convert.ToBoolean(DBNull.Value != drAlgo["PositionSizing"] ? drAlgo["PositionSizing"] : false),
                        L1 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg1"] ? drAlgo["Arg1"] : 0),
                        L2 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0),
                        L3 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0),
                        U1 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg4"] ? drAlgo["Arg4"] : 0),
                        U2 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg5"] ? drAlgo["Arg5"] : 0),
                        U3 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg6"] ? drAlgo["Arg6"] : 0),
                        //IntD = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0) == 1,
                        PnL = Convert.ToDecimal(DBNull.Value != drAlgo["NetPrice"] ? drAlgo["NetPrice"] : 0),
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

                    algoInput.ActiveOrderTrios ??= new List<OrderTrio>();
                    List<OrderTrio> orderTrios = new List<OrderTrio>();
                    foreach (DataRow drOrderTrio in drOrderTrios)
                    {
                        if (Convert.ToBoolean(drOrderTrio["Active"]))
                        {
                            OrderTrio orderTrio = new OrderTrio()
                            {
                                Id = Convert.ToInt32(drOrderTrio["Id"]),
                                TargetProfit = Convert.ToDecimal(drOrderTrio["TargetProfit"]),
                                StopLoss = Convert.ToDecimal(drOrderTrio["StopLoss"]),
                                BaseInstrumentStopLoss = Convert.ToDecimal(drOrderTrio["BaseInstrumentStopLoss"])
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
                                    // algosView.Orders.Add(ViewUtility.GetOrderView(o));
                                }
                            }
                            //Instrument option = dl.GetInstrument(orderTrio.Order.Tradingsymbol);
                            //orderTrio.Option = option;
                            orderTrios.Add(orderTrio);
                        }
                    }
                    //activeAlgos.Add(algosView);
                    algoInput.ActiveOrderTrios = orderTrios;

                    //Trade(algoInput, algosView.ains);

                    ////if (algosView.Orders != null)
                    ////{
                    //activeAlgos.Add(algosView);
                    ////}
                }
                // return activeAlgos.ToArray();
                return algoInput;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        private void ObserverSubscription(IZMQ paTrader)
        {
            //GlobalObjects.ObservableFactory ??= new ObservableFactory();
            //paTrader.Subscribe(GlobalObjects.ObservableFactory);
        }

        //private async Task NMQClientSubscription(ManageStrangleWithLevels paTrader, uint token)
        //{
        //    zmqClient = new ZMQClient();
        //    zmqClient.AddSubscriber(new List<uint>() { token });

        //    await zmqClient.Subscribe(paTrader);
        //}
#if local
        private async Task KFKClientSubscription(ManageStrangleWithLevels paTrader, uint token)
        {
            kfkClient = new KSubscriber();
            kfkClient.AddSubscriber(new List<uint>() { token });

            await kfkClient.Subscribe(paTrader);
        }
        //private async Task ObserverSubscription(ManageStrangleWithLevels paTrader, uint token)
        //{
        //    GlobalObjects.ObservableFactory ??= new ObservableFactory();
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
        //private async Task MQTTCientSubscription(ManageStrangleWithLevels paTrader, uint token)
        //{
        //    mqttSubscriber = new MQTTSubscriber();
        //    mqttSubscriber.
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
#endif
        //        private void PATrade_OnOptionUniverseChange(ManageStrangleWithLevels source)
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
        //            return Task.FromResult((int)AlgoIndex.ManageStrangleWithLevels);
        //        }

        //        [HttpPut("{ain}")]
        //        public bool Put(int ain, [FromBody] int start)
        //        {
        //            List<ManageStrangleWithLevels> activeAlgoObjects;
        //            if (!_cache.TryGetValue(key, out activeAlgoObjects))
        //            {
        //                activeAlgoObjects = new List<ManageStrangleWithLevels>();
        //            }

        //            ManageStrangleWithLevels algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
        //            if (algoObject != null)
        //            {
        //                algoObject.StopTrade(!Convert.ToBoolean(start));
        //            }
        //            _cache.Set(key, activeAlgoObjects);

        //            return true;
        //        }
    }
}

