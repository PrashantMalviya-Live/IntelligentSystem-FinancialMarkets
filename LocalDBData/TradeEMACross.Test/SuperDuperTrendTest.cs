using Algorithms.Algorithms;
using Algorithms.Utilities.Views;
using Algorithms.Utilities;
using Algos.Utilities.Views;
using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Data;
using Newtonsoft.Json;

namespace LocalDBData.Test
{
    internal class SuperDuperTrendTest : ITest
    {
        SuperDuperTrend paTrader;
        public void Execute(PriceActionInput paInputs)
        {
            PriceActionInput input = GetActiveAlgos();
            if (input == null)
            {
                paTrader = ExecuteAlgo(paInputs);
            }
            else
            {
                paTrader = ExecuteAlgo(input);
                paTrader.LoadActiveOrders(input);
            }


            paTrader.OnOptionUniverseChange += ObserverSubscription;
            paTrader.OnCriticalEvents += SendNotification;// (string title, string body)
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
        private SuperDuperTrend ExecuteAlgo(PriceActionInput paInputs)
        {
            SuperDuperTrend paTrader =
              new SuperDuperTrend(TimeSpan.FromMinutes(paInputs.CTF), paInputs.BToken, paInputs.Expiry,
              paInputs.Qty, paInputs.UID, paInputs.TP, paInputs.SL, paInputs.AID,
              positionSizing: false, maxLossPerTrade: 0, httpClientFactory: null);// firebaseMessaging);

            return paTrader;
        }

        public PriceActionInput GetActiveAlgos()
        {
            try
            {
                string userId = "client1188";
                DataLogic dl = new DataLogic();
                DataSet ds = dl.GetActiveAlgos(AlgoIndex.StockInitialMomentum, userId);


                DataTable dtActiveAlgos = ds.Tables[0];
                DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                    new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

                DataRelation algo_orderTrios_relation = ds.Relations.Add("Algo_OrderTrios", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                    new DataColumn[] { ds.Tables[3].Columns["AlgoInstance"] });

                //DataRelation orderTrios_orders_relation = ds.Relations.Add("OrderTrios_Orders", new DataColumn[] { ds.Tables[3].Columns["MainOrderId"] },
                //    new DataColumn[] { ds.Tables[2].Columns["OrderId"] });
                PriceActionInput algoInput = null;
                foreach (DataRow drAlgo in dtActiveAlgos.Rows)
                {
                    //ActiveAlgosView algosView = new ActiveAlgosView();

                    algoInput = new PriceActionInput
                    {
                        AID = Convert.ToInt32(drAlgo["Id"]),
                        Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
                        CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
                        Qty = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
                        BToken = Convert.ToUInt32(drAlgo["BToken"]),
                        UID = Convert.ToString(DBNull.Value != drAlgo["Arg9"] ? drAlgo["Arg9"] : 0),
                        //PS = Convert.ToBoolean(DBNull.Value != drAlgo["PositionSizing"] ? drAlgo["PositionSizing"] : false),
                        //TP = Convert.ToDecimal(DBNull.Value != drAlgo["Arg1"] ? drAlgo["Arg1"] : 0),
                        //SL = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0),
                        //IntD = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0) == 1,
                        PnL = Convert.ToDecimal(DBNull.Value != drAlgo["NetPrice"] ? drAlgo["NetPrice"] : 0),


                    };

                    //Arg
                    //arg1: Convert.ToInt32(breakout), upperLimit: _currentPriceRange.Upper, lowerLimit: _currentPriceRange.Lower
                    //arg2: _currentPriceRange.NextUpper, arg3: _currentPriceRange.NextLower
                    //arg4: _previousPriceRange.Upper, arg5: _previousPriceRange.Lower
                    //arg6: _previousPriceRange.NextUpper, arg7: _currentPriceRange.NextLower

                    //algoInput.CPR = new PriceRange();
                    //algoInput.CPR.Upper = Convert.ToDecimal(DBNull.Value != drAlgo["UpperLimit"] ? drAlgo["UpperLimit"] : 0);
                    //algoInput.CPR.Lower = Convert.ToDecimal(DBNull.Value != drAlgo["LowerLimit"] ? drAlgo["LowerLimit"] : 0);
                    //algoInput.CPR.NextUpper = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0);
                    //algoInput.CPR.NextLower = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0);
                    //algoInput.PPR = new PriceRange();
                    //algoInput.PPR.Upper = Convert.ToDecimal(DBNull.Value != drAlgo["Arg4"] ? drAlgo["Arg4"] : 0);
                    //algoInput.PPR.Lower = Convert.ToDecimal(DBNull.Value != drAlgo["Arg5"] ? drAlgo["Arg5"] : 0);
                    //algoInput.PPR.NextUpper = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0);
                    //algoInput.PPR.NextLower = Convert.ToDecimal(DBNull.Value != drAlgo["Arg6"] ? drAlgo["Arg6"] : 0);

                    ////algoInput.BM = (DBNull.Value != drAlgo["Arg4"] ? drAlgo["Arg5"] : 0) as Breakout;
                    //if (DBNull.Value != drAlgo["Arg1"])
                    //{
                    //    switch (Convert.ToDecimal(drAlgo["Arg1"]))
                    //    {
                    //        case 0:
                    //            algoInput.BM = Breakout.NONE;
                    //            break;
                    //        case 1:
                    //            algoInput.BM = Breakout.UP;
                    //            break;
                    //        case -1:
                    //            algoInput.BM = Breakout.DOWN;
                    //            break;
                    //    }
                    //}

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
                                BaseInstrumentStopLoss = Convert.ToDecimal(drOrderTrio["BaseInstrumentStopLoss"]),
                                SLFlag = DBNull.Value != drOrderTrio["SLFlag"] ? Convert.ToBoolean(drOrderTrio["SLFlag"]) : null,
                                EntryTradeTime = Convert.ToDateTime(drOrderTrio["EntryTradeTime"]),
                                IndicatorsValue = DBNull.Value != drOrderTrio["IndicatorsValue"] ? JsonConvert.DeserializeObject<Dictionary<int, bool>>((string) drOrderTrio["IndicatorsValue"]) : null
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

        private void ObserverSubscription(SuperDuperTrend paTrader)
        {
            //GlobalObjects.ObservableFactory ??= new ObservableFactory();
            //paTrader.Subscribe(GlobalObjects.ObservableFactory);
        }

        //private async Task NMQClientSubscription(StockMomentum paTrader, uint token)
        //{
        //    zmqClient = new ZMQClient();
        //    zmqClient.AddSubscriber(new List<uint>() { token });

        //    await zmqClient.Subscribe(paTrader);
        //}
#if local
        private async Task KFKClientSubscription(StockMomentum paTrader, uint token)
        {
            kfkClient = new KSubscriber();
            kfkClient.AddSubscriber(new List<uint>() { token });

            await kfkClient.Subscribe(paTrader);
        }
        //private async Task ObserverSubscription(StockMomentum paTrader, uint token)
        //{
        //    GlobalObjects.ObservableFactory ??= new ObservableFactory();
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
        //private async Task MQTTCientSubscription(StockMomentum paTrader, uint token)
        //{
        //    mqttSubscriber = new MQTTSubscriber();
        //    mqttSubscriber.
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
#endif
        //        private void PATrade_OnOptionUniverseChange(StockMomentum source)
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
        //            return Task.FromResult((int)AlgoIndex.StockMomentum);
        //        }

        //        [HttpPut("{ain}")]
        //        public bool Put(int ain, [FromBody] int start)
        //        {
        //            List<StockMomentum> activeAlgoObjects;
        //            if (!_cache.TryGetValue(key, out activeAlgoObjects))
        //            {
        //                activeAlgoObjects = new List<StockMomentum>();
        //            }

        //            StockMomentum algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
        //            if (algoObject != null)
        //            {
        //                algoObject.StopTrade(!Convert.ToBoolean(start));
        //            }
        //            _cache.Set(key, activeAlgoObjects);

        //            return true;
        //        }
    }
}

