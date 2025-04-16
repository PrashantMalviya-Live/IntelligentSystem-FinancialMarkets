using Algorithms.Algorithms;
using Algorithms.Utilities;
using Algorithms.Utilities.Views;
using Algos.Utilities.Views;
using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Data;

namespace LocalDBData.Test
{
    internal class DirectionalWithStraddleShiftTest : ITest
    {
        DirectionalWithStraddleShiftCandle paTrader;

        public void Execute(StraddleInput straddleInput)
        {
            paTrader = ExecuteAlgo(straddleInput);

            paTrader.OnOptionUniverseChange += ObserverSubscription;
            // paTrader.OnCriticalEvents += SendNotification;// (string title, string body)
            paTrader.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
            paTrader.OnTradeExit += OptionSellwithRSI_OnTradeExit;
        }

        public void OnNext(Tick tick)
        {
            paTrader.OnNext(tick);
        }
        public void StopTrade(bool stopTrade)
        {
            paTrader.StopTrade(stopTrade);
        }
        private DirectionalWithStraddleShiftCandle ExecuteAlgo(StraddleInput straddleInput)
        {
            try
            {
                int algoInstance = 0;
                StraddleInput strdlInput = GetActiveOrderTrios(out algoInstance);

                if (strdlInput != null)
                {
                    straddleInput = strdlInput;

                }
                DirectionalWithStraddleShiftCandle straddleManager = new DirectionalWithStraddleShiftCandle(TimeSpan.FromMinutes(straddleInput.CTF),
                    straddleInput.BToken, straddleInput.Expiry, straddleInput.Qty, straddleInput.UID,
                    straddleInput.TP, straddleInput.SL, straddleInput.IntraDay, straddleInput.SS, straddleInput.TR, algoInstance: algoInstance, httpClientFactory: null);

                if (strdlInput != null)
                {
                    straddleManager.LoadActiveOrders(strdlInput.ActiveOrderTrios);
                }
                return straddleManager;
            }
            catch (Exception ex)
            {
                return null;

            }
        }

        public StraddleInput GetActiveOrderTrios(out int algoInstance)
        {
            try
            {


                string userId = "NJ18111985";
                DataLogic dl = new DataLogic();
                DataSet ds = dl.GetActiveAlgos(AlgoIndex.MomentumBuyWithStraddle, userId);

                algoInstance = 0;
                DataTable dtActiveAlgos = ds.Tables[0];
                if (dtActiveAlgos.Rows.Count == 0)
                {
                    return null;
                }
                DataRow drAlgo = dtActiveAlgos.Rows[0];
                DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                    new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

                DataRelation algo_orderTrios_relation = ds.Relations.Add("Algo_OrderTrios", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                    new DataColumn[] { ds.Tables[3].Columns["AlgoInstance"] });

                //DataRelation orderTrios_orders_relation = ds.Relations.Add("OrderTrios_Orders", new DataColumn[] { ds.Tables[3].Columns["MainOrderId"] },
                //    new DataColumn[] { ds.Tables[2].Columns["OrderId"] });

                //foreach (DataRow drAlgo in dtActiveAlgos.Rows)
                //{
                StraddleInput algoInput = new StraddleInput
                {
                    Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
                    CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
                    Qty = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
                    BToken = Convert.ToUInt32(drAlgo["BToken"]),
                    UID = Convert.ToString(DBNull.Value != drAlgo["Arg9"] ? drAlgo["Arg9"] : 0),
                    //PS = Convert.ToBoolean(DBNull.Value != drAlgo["PositionSizing"] ? drAlgo["PositionSizing"] : false),
                    TP = Convert.ToDecimal(DBNull.Value != drAlgo["Arg1"] ? drAlgo["Arg1"] : 0),
                    SL = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0),
                    IntraDay = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0) == 1,
                    PnL = Convert.ToDecimal(DBNull.Value != drAlgo["NetPrice"] ? drAlgo["NetPrice"] : 0),
                    TR = Convert.ToDecimal(DBNull.Value != drAlgo["UpperLimit"] ? drAlgo["UpperLimit"] : 1.67m)
                };
                //algosView.aid = Convert.ToInt32(drAlgo["AlgoId"]);
                //algosView.an = Convert.ToString((AlgoIndex)algosView.aid);
                algoInstance = Convert.ToInt32(drAlgo["Id"]);

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
                            StopLoss = Convert.ToDecimal(drOrderTrio["StopLoss"])
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

                // Trade(algoInput, algosView.ains);

                //if (algosView.Orders != null)
                //{
                // activeAlgos.Add(algosView);
                //}
                //}
                return algoInput;
            }
            catch (Exception ex)
            {
                algoInstance = 0;
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

        private void ObserverSubscription(DirectionalWithStraddleShiftCandle paTrader)
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
#if LOCAL
        //private async Task KFKClientSubscription(CandleWickScalping paTrader, uint token)
        //{
        //    kfkClient = new KSubscriber();
        //    kfkClient.AddSubscriber(new List<uint>() { token });

        //    await kfkClient.Subscribe(paTrader);
        //}
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
