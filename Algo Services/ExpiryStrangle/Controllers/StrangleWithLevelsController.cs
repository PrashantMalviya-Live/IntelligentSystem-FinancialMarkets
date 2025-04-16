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


namespace ExpiryStrangle.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StrangleWithLevelsController : ControllerBase
    {
        private const string key = "ACTIVE_STRANGLE_WITH_LEVEL_TRADE_OBJECTS";
        ZMQClient zmqClient;
        private IMemoryCache _cache;
        public StrangleWithLevelsController(IMemoryCache cache)
        {
            this._cache = cache;
        }

        [HttpGet]
        public IEnumerable<BInstumentView> Get()
        {
            DataLogic dl = new DataLogic();
            List<Instrument> bInstruments = dl.RetrieveBaseInstruments();

            return (from n in bInstruments select new BInstumentView { InstrumentToken = n.InstrumentToken, TradingSymbol = n.TradingSymbol.Trim(' ') }).ToList();
        }
        // GET api/<HomeController>/5
        [HttpGet("{token}")]
        public IEnumerable<string> OptionExpiries(uint token)
        {
            DataLogic dl = new DataLogic();
            List<string> expiryList = dl.RetrieveOptionExpiries(token);
            return expiryList;
        }

       // [HttpGet("activealgos")]
        public async Task<IEnumerable<ActiveAlgosView>> GetActiveAlgos()
        {
            DataLogic dl = new DataLogic();
            DataSet ds = dl.GetActiveAlgos(AlgoIndex.DeltaStrangleWithLevels);

            List<ActiveAlgosView> activeAlgos = new List<ActiveAlgosView>();

            DataTable dtActiveAlgos = ds.Tables[0];
            DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

            foreach (DataRow drAlgo in dtActiveAlgos.Rows)
            {
                ActiveAlgosView algosView = new ActiveAlgosView();

                StrangleWithLevelInputs algoInput = new StrangleWithLevelInputs
                {
                    Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
                    L1 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0),
                    L2 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg4"] ? drAlgo["Arg5"] : 0),
                    U1 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg5"] ? drAlgo["Arg5"] : 0),
                    U2 = Convert.ToDecimal(DBNull.Value != drAlgo["Arg6"] ? drAlgo["Arg5"] : 0),
                    IDelta = Convert.ToDecimal(DBNull.Value != drAlgo["Arg7"] ? drAlgo["Arg7"] : 0),
                    MaxDelta = Convert.ToDecimal(DBNull.Value != drAlgo["UpperLimit"] ? drAlgo["UpperLimit"] : 0),
                    MinDelta = Convert.ToDecimal(DBNull.Value != drAlgo["LowerLimit"] ? drAlgo["LowerLimit"] : 0),
                    CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
                    IQty = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
                    SQty = Convert.ToInt32(drAlgo["StepQtyInLotSize"]),
                    MQty = Convert.ToInt32(drAlgo["MaxQtyInLotSize"]),
                    BToken = Convert.ToUInt32(drAlgo["BToken"]),
                    SL = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0),
                    TP = Convert.ToDecimal(DBNull.Value != drAlgo["Arg1"] ? drAlgo["Arg1"] : 0),
                };
                algosView.aid = Convert.ToInt32(drAlgo["AlgoId"]);
                algosView.an = Convert.ToString((AlgoIndex)algosView.aid);
                algosView.ains = Convert.ToInt32(drAlgo["Id"]);

                algosView.expiry = Convert.ToDateTime(drAlgo["Expiry"]).ToString("yyyy-MM-dd");
                algosView.mins = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]);
                algosView.lotsize = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]);
                algosView.binstrument = Convert.ToString(drAlgo["BToken"]);
                algosView.algodate = Convert.ToDateTime(drAlgo["Timestamp"]).ToString("yyyy-MM-dd");

                DataRow[] drOrders = drAlgo.GetChildRows(algo_orders_relation);
                List<Order> orders = new List<Order>();
                foreach (DataRow drOrder in drOrders)
                {
                    Order o = ViewUtility.GetOrder(drOrder);
                    orders.Add(o);
                }

                //var slmOrders = orders.Where(o => o.Status == Constants.ORDER_STATUS_TRIGGER_PENDING && o.OrderType == Constants.ORDER_TYPE_SLM);
                //var completedOrders = orders.Where(o => o.Status == Constants.ORDER_STATUS_COMPLETE);

                //var pendingOrders = slmOrders.Where(x => !completedOrders.Any(c => c.OrderId == x.OrderId));

                DateTime? lastCallOrderTime = orders.Where(x => x.Tradingsymbol.TakeLast(2).First().ToString().ToLower() == "c").Max(x => x.OrderTimestamp);
                DateTime? lastPutOrderTime = orders.Where(x => x.Tradingsymbol.TakeLast(2).First().ToString().ToLower() == "p").Max(x => x.OrderTimestamp);

                var lastCallOrder = orders.Where(x => x.OrderTimestamp == lastCallOrderTime && x.Tradingsymbol.TakeLast(2).First().ToString().ToLower() == "c").FirstOrDefault();
                var lastPutOrder = orders.Where(x => x.OrderTimestamp == lastPutOrderTime && x.Tradingsymbol.TakeLast(2).First().ToString().ToLower() == "p").FirstOrDefault();

                bool ordersPending = false;
                if (lastCallOrder != null && lastCallOrder.TransactionType.Trim(' ').ToLower() == "sell")
                {
                    ordersPending = true;
                    algosView.Orders.Add(ViewUtility.GetOrderView(lastCallOrder));
                    algoInput.CallOrder = lastCallOrder;
                }
                if (lastPutOrder != null && lastPutOrder.TransactionType.Trim(' ').ToLower() == "sell")
                {
                    ordersPending = true;
                    algosView.Orders.Add(ViewUtility.GetOrderView(lastPutOrder));
                    algoInput.PutOrder = lastPutOrder;
                }
                if (ordersPending)
                {
                    Trade(algoInput, algosView.ains);
                    activeAlgos.Add(algosView);
                }
                //activeAlgos.Add(algosView);

                //if (pendingOrders != null && pendingOrders.Count() > 0)
                //{
                //    algoInput.ActiveOrder = pendingOrders.FirstOrDefault();
                //    algosView.Orders.Add(ViewUtility.GetOrderView(pendingOrders.FirstOrDefault()));
                //    if (algoInput.ActiveOrder != null)
                //    {
                //        Trade(algoInput, algosView.ains);
                //    }
                //    activeAlgos.Add(algosView);
                //}
            }
            return activeAlgos.ToArray();
        }


        [HttpPost]
        public async Task<ActiveAlgosView> Trade([FromBody] StrangleWithLevelInputs strangleWithLevelInputs, int algoInstance = 0)
        {
            uint bInstrumentToken = strangleWithLevelInputs.BToken;
            DateTime endDateTime = DateTime.Now;
            DateTime expiry = strangleWithLevelInputs.Expiry;
            int initialQuantity = strangleWithLevelInputs.IQty;
            int stepQuantity = strangleWithLevelInputs.SQty;
            int maxQuantity = strangleWithLevelInputs.MQty;
            decimal stoploss = strangleWithLevelInputs.SL;
            decimal targetProfit = strangleWithLevelInputs.TP;
            TimeSpan candleTimeSpan = TimeSpan.FromMinutes(strangleWithLevelInputs.CTF);

            strangleWithLevelInputs.uid = "PM27031981";

            ManageStrangleWithLevels strangleWithLevels = new ManageStrangleWithLevels(candleTimeSpan, bInstrumentToken, expiry, 
                strangleWithLevelInputs.L1, strangleWithLevelInputs.L2, strangleWithLevelInputs.U1, strangleWithLevelInputs.U2, 
                strangleWithLevelInputs.IQty, strangleWithLevelInputs.SQty, strangleWithLevelInputs.MQty, strangleWithLevelInputs.IDelta, 
                strangleWithLevelInputs.MinDelta, strangleWithLevelInputs.MaxDelta, strangleWithLevelInputs.uid, strangleWithLevelInputs.TP, strangleWithLevelInputs.SL, algoInstance);

            algoInstance = strangleWithLevels.AlgoInstance;
            strangleWithLevels.LoadActiveOrders(strangleWithLevelInputs.CallOrder, strangleWithLevelInputs.PutOrder);

            strangleWithLevels.OnOptionUniverseChange += ManageStrangleWithLevels_OnOptionUniverseChange;
            strangleWithLevels.OnTradeEntry += ManageStrangleWithLevels_OnTradeEntry;
            strangleWithLevels.OnTradeExit += ManageStrangleWithLevels_OnTradeExit;

            List<ManageStrangleWithLevels> activeAlgoObjects = _cache.Get<List<ManageStrangleWithLevels>>(key);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<ManageStrangleWithLevels>();
            }
            activeAlgoObjects.Add(strangleWithLevels);
            _cache.Set(key, activeAlgoObjects);

            Task task = Task.Run(() => NMQClientSubscription(strangleWithLevels, bInstrumentToken));

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.DeltaStrangleWithLevels),
                an = Convert.ToString((AlgoIndex)AlgoIndex.DeltaStrangleWithLevels),
                ains = strangleWithLevels.AlgoInstance,
                algodate = endDateTime.ToString("yyyy-MM-dd"),
                binstrument = bInstrumentToken.ToString(),
                expiry = expiry.ToString("yyyy-MM-dd"),
                lotsize = maxQuantity,
                mins = 0
            };
        }
        private async Task NMQClientSubscription(ManageStrangleWithLevels strangleWithLevels, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(strangleWithLevels);
        }
        private void ManageStrangleWithLevels_OnTradeExit(Order st)
        {
            //publish trade details and count
            //Bind with trade token details, use that as an argument
            OrderCore.PublishOrder(st);
            Thread.Sleep(100);
        }

        private void ManageStrangleWithLevels_OnTradeEntry(Order st)
        {
            //publish trade details and count
            OrderCore.PublishOrder(st);
            Thread.Sleep(100);
        }

        //[HttpGet]
        //public void Get()
        //{
        //    StartService();
        //}

        //[HttpGet("positions/all")]
        //public IEnumerable<AlgoPosition> GetCurrentPositions()
        //{
        //    DataLogic dl = new DataLogic();
        //    DataSet dsActiveStrangles = dl.RetrieveActiveStrangles(AlgoIndex.ExpiryTrade);

        //    return null;

        //}

        private void ManageStrangleWithLevels_OnOptionUniverseChange(ManageStrangleWithLevels source)
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
        ////[HttpPost]
        //public async Task CreateOptionStrategy(OptionStrategy ostrategy)
        //{
        //    ///Depending on strategy, UI will call apppropriate API. This API will be called to place manual orders only.
        //    DataLogic dl = new DataLogic();
        //    ostrategy.Id = dl.CreateOptionStrategy(ostrategy);

        //    MarketOrders orders = new MarketOrders();
        //    orders.PlaceOrder(ostrategy.Id, ostrategy.Orders);

        //}
    }
}
