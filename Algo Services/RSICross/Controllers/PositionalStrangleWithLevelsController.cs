using Algos.TLogics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using ZMQFacade;
using Algorithms.Algorithms;
using Algorithms.Utilities;
using GlobalLayer;
using Global.Web;
using GlobalCore;
using System.Linq;
using System.Data;
using System.Threading.Tasks;
using Algorithms.Utilities.Views;
using Algos.Utilities.Views;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;

namespace RSIManagerService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StrangleWithLevelsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string key = "ACTIVE_POSITIONAL_STRANGLE_WITH_LEVELS_OBJECTS_SHIFT";
        IConfiguration configuration;
        ZMQClient zmqClient;
        private IMemoryCache _cache;
        public StrangleWithLevelsController(IMemoryCache cache, IHttpClientFactory httpClientFactory)
        {
            this._cache = cache;
            this._httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IEnumerable<BInstumentView> Get()
        {
            DataLogic dl = new DataLogic();
            List<Instrument> bInstruments = dl.RetrieveBaseInstruments();

            return (from n in bInstruments select new BInstumentView { InstrumentToken = n.InstrumentToken, TradingSymbol = n.TradingSymbol.Trim(' ') }).ToList();
        }

        [HttpGet("{token}")]
        public IEnumerable<string> OptionExpiries(uint token)
        {
            DataLogic dl = new DataLogic();
            List<string> expiryList = dl.RetrieveOptionExpiries(token);
            return expiryList;
        }
        [HttpGet("activealgos")]
        public async Task<IEnumerable<ActiveAlgosView>> GetActiveAlgos()
        {
            try
            {
                string userId = "PM27031981";
                DataLogic dl = new DataLogic();
                DataSet ds = dl.GetActiveAlgos(AlgoIndex.DeltaStrangleWithLevels, userId);

                List<ActiveAlgosView> activeAlgos = new List<ActiveAlgosView>();

                DataTable dtActiveAlgos = ds.Tables[0];
                DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                    new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

                DataRelation algo_orderTrios_relation = ds.Relations.Add("Algo_OrderTrios", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                    new DataColumn[] { ds.Tables[3].Columns["AlgoInstance"] });

                //DataRelation orderTrios_orders_relation = ds.Relations.Add("OrderTrios_Orders", new DataColumn[] { ds.Tables[3].Columns["MainOrderId"] },
                //    new DataColumn[] { ds.Tables[2].Columns["OrderId"] });

                foreach (DataRow drAlgo in dtActiveAlgos.Rows)
                {
                    ActiveAlgosView algosView = new ActiveAlgosView();


                    StrangleWithDeltaandLevelInputs algoInput = new StrangleWithDeltaandLevelInputs
                    {
                        Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
                        CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
                        IQty = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
                        StepQty = Convert.ToInt32(drAlgo["StepQtyInLotSize"]),
                        MaxQty = Convert.ToInt32(drAlgo["MaxQtyInLotSize"]),
                        BToken = Convert.ToUInt32(drAlgo["BToken"]),
                        
                        UID = Convert.ToString(DBNull.Value != drAlgo["Arg9"] ? drAlgo["Arg9"] : 0),
                        L1 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg1"] ? drAlgo["Arg1"] : 0),
                        L2 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0),
                        L3 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0),
                        U1 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg1"] ? drAlgo["Arg4"] : 0),
                        U2 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg5"] : 0),
                        U3 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg6"] : 0),
                        PnL = Convert.ToDecimal(DBNull.Value != drAlgo["NetPrice"] ? drAlgo["NetPrice"] : 0)
                    };
                    algosView.aid = Convert.ToInt32(drAlgo["AlgoId"]);
                    algosView.an = Convert.ToString((AlgoIndex)algosView.aid);
                    algosView.ains = Convert.ToInt32(drAlgo["Id"]);

                    algosView.expiry = Convert.ToDateTime(drAlgo["Expiry"]).ToString("yyyy-MM-dd");
                    algosView.mins = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]);
                    algosView.binstrument = Convert.ToString(drAlgo["BToken"]);
                    algosView.algodate = Convert.ToDateTime(drAlgo["Timestamp"]).ToString("yyyy-MM-dd");

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
                                    algosView.Orders.Add(ViewUtility.GetOrderView(o));
                                }
                            }
                            //Instrument option = dl.GetInstrument(orderTrio.Order.Tradingsymbol);
                            //orderTrio.Option = option;
                            orderTrios.Add(orderTrio);
                        }
                    }
                    //activeAlgos.Add(algosView);
                    algoInput.ActiveOrderTrios = orderTrios;

                    Trade(algoInput, algosView.ains);
                    activeAlgos.Add(algosView);
                }
                return activeAlgos.ToArray();
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        [HttpPost]
        public async Task<ActiveAlgosView> Trade([FromBody] StrangleWithDeltaandLevelInputs paInputs, int algoInstance = 0)
        {
            TimeSpan candleTimeSpan = TimeSpan.FromMinutes(5);

            ManageStrangleWithLevels paTrader =
               new ManageStrangleWithLevels(new TimeSpan(0, paInputs.CTF, 0), paInputs.BToken, paInputs.Expiry, paInputs.CurrentDate,
               paInputs.L1, paInputs.L2, paInputs.U1, paInputs.U2, paInputs.IQty, paInputs.StepQty, paInputs.MaxQty,
               paInputs.IDelta, paInputs.MinDelta, paInputs.MaxDelta, paInputs.UID, paInputs.TP, paInputs.SL,
               httpClientFactory: _httpClientFactory);

            paTrader.LoadActiveOrders(paInputs.ActiveOrderTrios);

            paTrader.OnOptionUniverseChange += PaTrader_OnOptionUniverseChange;
            paTrader.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
            paTrader.OnTradeExit += OptionSellwithRSI_OnTradeExit;

            List<ManageStrangleWithLevels> activeAlgoObjects = _cache.Get<List<ManageStrangleWithLevels>>(key);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<ManageStrangleWithLevels>();
            }
            activeAlgoObjects.Add(paTrader);
            _cache.Set(key, activeAlgoObjects);

            Task task = Task.Run(() => NMQClientSubscription(paTrader, paInputs.BToken));

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.DeltaStrangleWithLevels),
                an = Convert.ToString((AlgoIndex)AlgoIndex.DeltaStrangleWithLevels),
                ains = paTrader.AlgoInstance,
                algodate = DateTime.Now.ToString("yyyy-MM-dd"),
                binstrument = paInputs.BToken.ToString(),
                expiry = DateTime.Now.ToString("yyyy-MM-dd"),
                lotsize = paInputs.IQty,
                mins = 0
            };
        }


        // [HttpGet("activealgos")]
        //public async Task<IEnumerable<ActiveAlgosView>> GetActiveAlgos()
        //{
        //    DataLogic dl = new DataLogic();
        //    DataSet ds = dl.GetActiveAlgos(AlgoIndex.DeltaStrangleWithLevels);

        //    List<ActiveAlgosView> activeAlgos = new List<ActiveAlgosView>();

        //    DataTable dtActiveAlgos = ds.Tables[0];
        //    DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
        //        new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

        //    foreach (DataRow drAlgo in dtActiveAlgos.Rows)
        //    {
        //        ActiveAlgosView algosView = new ActiveAlgosView();

        //        StrangleWithLevelInputs algoInput = new StrangleWithLevelInputs
        //        {
        //            Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
        //            L1 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0),
        //            L2 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg4"] ? drAlgo["Arg5"] : 0),
        //            U1 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg5"] ? drAlgo["Arg5"] : 0),
        //            U2 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg6"] ? drAlgo["Arg5"] : 0),
        //            IDelta = Convert.ToDecimal(DBNull.Value != drAlgo["Arg7"] ? drAlgo["Arg7"] : 0),
        //            MaxDelta = Convert.ToDecimal(DBNull.Value != drAlgo["UpperLimit"] ? drAlgo["UpperLimit"] : 0),
        //            MinDelta = Convert.ToDecimal(DBNull.Value != drAlgo["LowerLimit"] ? drAlgo["LowerLimit"] : 0),
        //            CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
        //            IQty = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
        //            SQty = Convert.ToInt32(drAlgo["StepQtyInLotSize"]),
        //            MQty = Convert.ToInt32(drAlgo["MaxQtyInLotSize"]),
        //            BToken = Convert.ToUInt32(drAlgo["BToken"]),
        //            SL = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0),
        //            TP = Convert.ToDecimal(DBNull.Value != drAlgo["Arg1"] ? drAlgo["Arg1"] : 0),
        //        };
        //        algosView.aid = Convert.ToInt32(drAlgo["AlgoId"]);
        //        algosView.an = Convert.ToString((AlgoIndex)algosView.aid);
        //        algosView.ains = Convert.ToInt32(drAlgo["Id"]);

        //        algosView.expiry = Convert.ToDateTime(drAlgo["Expiry"]).ToString("yyyy-MM-dd");
        //        algosView.mins = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]);
        //        algosView.lotsize = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]);
        //        algosView.binstrument = Convert.ToString(drAlgo["BToken"]);
        //        algosView.algodate = Convert.ToDateTime(drAlgo["Timestamp"]).ToString("yyyy-MM-dd");

        //        DataRow[] drOrders = drAlgo.GetChildRows(algo_orders_relation);
        //        List<Order> orders = new List<Order>();
        //        foreach (DataRow drOrder in drOrders)
        //        {
        //            Order o = ViewUtility.GetOrder(drOrder);
        //            orders.Add(o);
        //        }

        //        //var slmOrders = orders.Where(o => o.Status == Constants.ORDER_STATUS_TRIGGER_PENDING && o.OrderType == Constants.ORDER_TYPE_SLM);
        //        //var completedOrders = orders.Where(o => o.Status == Constants.ORDER_STATUS_COMPLETE);

        //        //var pendingOrders = slmOrders.Where(x => !completedOrders.Any(c => c.OrderId == x.OrderId));

        //        DateTime? lastCallOrderTime = orders.Where(x => x.Tradingsymbol.TakeLast(2).First().ToString().ToLower() == "c").Max(x => x.OrderTimestamp);
        //        DateTime? lastPutOrderTime = orders.Where(x => x.Tradingsymbol.TakeLast(2).First().ToString().ToLower() == "p").Max(x => x.OrderTimestamp);

        //        var lastCallOrder = orders.Where(x => x.OrderTimestamp == lastCallOrderTime && x.Tradingsymbol.TakeLast(2).First().ToString().ToLower() == "c").FirstOrDefault();
        //        var lastPutOrder = orders.Where(x => x.OrderTimestamp == lastPutOrderTime && x.Tradingsymbol.TakeLast(2).First().ToString().ToLower() == "p").FirstOrDefault();

        //        bool ordersPending = false;
        //        if (lastCallOrder != null && lastCallOrder.TransactionType.Trim(' ').ToLower() == "sell")
        //        {
        //            ordersPending = true;
        //            algosView.Orders.Add(ViewUtility.GetOrderView(lastCallOrder));
        //            algoInput.CallOrder = lastCallOrder;
        //        }
        //        if (lastPutOrder != null && lastPutOrder.TransactionType.Trim(' ').ToLower() == "sell")
        //        {
        //            ordersPending = true;
        //            algosView.Orders.Add(ViewUtility.GetOrderView(lastPutOrder));
        //            algoInput.PutOrder = lastPutOrder;
        //        }
        //        if (ordersPending)
        //        {
        //            Trade(algoInput, algosView.ains);
        //            activeAlgos.Add(algosView);
        //        }
        //        //activeAlgos.Add(algosView);

        //        //if (pendingOrders != null && pendingOrders.Count() > 0)
        //        //{
        //        //    algoInput.ActiveOrder = pendingOrders.FirstOrDefault();
        //        //    algosView.Orders.Add(ViewUtility.GetOrderView(pendingOrders.FirstOrDefault()));
        //        //    if (algoInput.ActiveOrder != null)
        //        //    {
        //        //        Trade(algoInput, algosView.ains);
        //        //    }
        //        //    activeAlgos.Add(algosView);
        //        //}
        //    }
        //    return activeAlgos.ToArray();
        //}

        private void OptionSellwithRSI_OnTradeExit(Order st)
        {
            //publish trade details and count
            //Bind with trade token details, use that as an argument
            OrderCore.PublishOrder(st);
            Thread.Sleep(100);
        }

        private void OptionSellwithRSI_OnTradeEntry(Order st)
        {
            //publish trade details and count
            OrderCore.PublishOrder(st);
            Thread.Sleep(100);
        }

        private async Task NMQClientSubscription(ManageStrangleWithLevels straddleManager, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(straddleManager);
        }

        private void PaTrader_OnOptionUniverseChange(ManageStrangleWithLevels source)
        {
            try
            {
                zmqClient.AddSubscriber(source.SubscriptionTokens);
            }
            catch (Exception ex)
            {
                throw ex;

            }
        }
        [HttpGet("healthy")]
        public Task<int> Health()
        {
            return Task.FromResult((int)AlgoIndex.DeltaStrangleWithLevels);
        }

        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<ManageStrangleWithLevels> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<ManageStrangleWithLevels>();
            }

            ManageStrangleWithLevels algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
        }
    }
}
