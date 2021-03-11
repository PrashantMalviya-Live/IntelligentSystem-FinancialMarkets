using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using ZMQFacade;
using Algorithms.Algorithms;
using Algorithms.Charts;
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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace MarketView.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChartController : ControllerBase
    {
        private const string okey = "ACTIVE_IVTrade_OPTIONS";

        IConfiguration configuration;
        ZMQClient zmqClient;
        private IMemoryCache _cache;
        public ChartController(IMemoryCache cache)
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
            return null;
            //DataLogic dl = new DataLogic();
            //DataSet ds = dl.GetActiveAlgos(AlgoIndex.IVSpreadTrade);

            //List<ActiveAlgosView> activeAlgos = new List<ActiveAlgosView>();

            //DataTable dtActiveAlgos = ds.Tables[0];
            //DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
            //    new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

            //foreach (DataRow drAlgo in dtActiveAlgos.Rows)
            //{
            //    ActiveAlgosView algosView = new ActiveAlgosView();

            //    OptionBuywithRSIInput algoInput = new OptionBuywithRSIInput
            //    {
            //        Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
            //        CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
            //        Qty = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
            //        BToken = Convert.ToUInt32(drAlgo["BToken"]),
            //        MinDFBI = Convert.ToDecimal(DBNull.Value != drAlgo["Arg5"] ? drAlgo["Arg5"] : 0),
            //        MaxDFBI = Convert.ToDecimal(DBNull.Value != drAlgo["Arg4"] ? drAlgo["Arg4"] : 0),
            //        RULX = Convert.ToDecimal(DBNull.Value != drAlgo["UpperLimit"] ? drAlgo["UpperLimit"] : 0),
            //        RLLE = Convert.ToDecimal(DBNull.Value != drAlgo["LowerLimit"] ? drAlgo["LowerLimit"] : 0),
            //        EMA = Convert.ToInt32(DBNull.Value != drAlgo["Arg1"] ? drAlgo["Arg1"] : 0),
            //        //PS = Convert.ToBoolean(DBNull.Value != drAlgo["PositionSizing"] ? drAlgo["PositionSizing"] : false),
            //        TP = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0),
            //        CELL = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0),
            //        PEUL = Convert.ToDecimal(DBNull.Value != drAlgo["Arg6"] ? drAlgo["Arg6"] : 0)
            //    };
            //    algosView.aid = Convert.ToInt32(drAlgo["AlgoId"]);
            //    algosView.an = Convert.ToString((AlgoIndex)algosView.aid);
            //    algosView.ains = Convert.ToInt32(drAlgo["Id"]);

            //    algosView.expiry = Convert.ToDateTime(drAlgo["Expiry"]).ToString("yyyy-MM-dd");
            //    algosView.mins = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]);
            //    algosView.lotsize = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]);
            //    algosView.binstrument = Convert.ToString(drAlgo["BToken"]);
            //    algosView.algodate = Convert.ToDateTime(drAlgo["Timestamp"]).ToString("yyyy-MM-dd");

            //    DataRow[] drOrders = drAlgo.GetChildRows(algo_orders_relation);
            //    List<Order> orders = new List<Order>();
            //    foreach (DataRow drOrder in drOrders)
            //    {
            //        Order o = ViewUtility.GetOrder(drOrder);
            //        orders.Add(o);
            //    }

            //    //var slmOrders = orders.Where(o => o.Status == Constants.ORDER_STATUS_TRIGGER_PENDING && o.OrderType == Constants.ORDER_TYPE_SLM);
            //    //var completedOrders = orders.Where(o => o.Status == Constants.ORDER_STATUS_COMPLETE);

            //    //var pendingOrders = slmOrders.Where(x => !completedOrders.Any(c => c.OrderId == x.OrderId));

            //    DateTime? lastCallOrderTime = orders.Where(x => x.Tradingsymbol.TakeLast(2).First().ToString().ToLower() == "c").Max(x => x.OrderTimestamp);
            //    DateTime? lastPutOrderTime = orders.Where(x => x.Tradingsymbol.TakeLast(2).First().ToString().ToLower() == "p").Max(x => x.OrderTimestamp);
            //    DateTime? lastFutOrderTime = orders.Where(x => x.Tradingsymbol.TakeLast(3).First().ToString().ToLower() == "f").Max(x => x.OrderTimestamp);

            //    var lastCallOrder = orders.Where(x => x.OrderTimestamp == lastCallOrderTime && x.Tradingsymbol.TakeLast(2).First().ToString().ToLower() == "c").FirstOrDefault();
            //    var lastPutOrder = orders.Where(x => x.OrderTimestamp == lastPutOrderTime && x.Tradingsymbol.TakeLast(2).First().ToString().ToLower() == "p").FirstOrDefault();
            //    var lastFutOrder = orders.Where(x => x.OrderTimestamp == lastFutOrderTime && x.Tradingsymbol.TakeLast(3).First().ToString().ToLower() == "f").FirstOrDefault();

            //    bool ordersPending = false;
            //    if (lastCallOrder != null && lastCallOrder.TransactionType.Trim(' ').ToLower() == "buy")
            //    {
            //        ordersPending = true;
            //        algosView.Orders.Add(ViewUtility.GetOrderView(lastCallOrder));
            //        algoInput.Order = lastCallOrder;
            //    }
            //    if (lastPutOrder != null && lastPutOrder.TransactionType.Trim(' ').ToLower() == "buy")
            //    {
            //        ordersPending = true;
            //        algosView.Orders.Add(ViewUtility.GetOrderView(lastPutOrder));
            //        algoInput.Order = lastPutOrder;
            //    }
            //    if (lastFutOrder != null)
            //    {
            //        ordersPending = true;
            //        algosView.Orders.Add(ViewUtility.GetOrderView(lastFutOrder));
            //        algoInput.Order = lastFutOrder;
            //    }
            //    if (ordersPending)
            //    {
            //        Trade(algoInput, algosView.ains);
            //        activeAlgos.Add(algosView);
            //    }
            //    //activeAlgos.Add(algosView);

            //    //if (pendingOrders != null && pendingOrders.Count() > 0)
            //    //{
            //    //    algoInput.ActiveOrder = pendingOrders.FirstOrDefault();
            //    //    algosView.Orders.Add(ViewUtility.GetOrderView(pendingOrders.FirstOrDefault()));
            //    //    if (algoInput.ActiveOrder != null)
            //    //    {
            //    //        Trade(algoInput, algosView.ains);
            //    //    }
            //    //    activeAlgos.Add(algosView);
            //    //}
            //}
            //return activeAlgos.ToArray();
        }


        [HttpPost]
        public async Task Draw([FromBody] ChartDataInputs chartDataInput, int algoInstance = 0)
        {
            uint instrumentToken = chartDataInput.BToken;
            DateTime endDateTime = DateTime.Now;

            DateTime expiry1 = chartDataInput.Expiry1;
            DateTime expiry2 = chartDataInput.Expiry2;
            string instrumentType1 = chartDataInput.IT1;
            string instrumentType2 = chartDataInput.IT2;

#if local
            endDateTime = Convert.ToDateTime("2020-11-09 09:15:00");
#endif
            //decimal minDistanceFromBaseInstrument = 10;
            ///FOR ALL STOCKS FUTURE , PASS INSTRUMENTTOKEN AS ZERO. FOR CE/PE ON BNF/NF SEND THE INDEX TOKEN AS INSTRUMENTTOKEN

            StrangleChart strangleChart =
                new StrangleChart(instrumentToken, expiry1, expiry2,
                new TimeSpan(0,5,0), algoInstance);

            algoInstance = strangleChart.AlgoInstance;

            strangleChart.OnOptionUniverseChange += OptionBuywithRSI_OnOptionUniverseChange;
            strangleChart.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
            strangleChart.OnTradeExit += OptionSellwithRSI_OnTradeExit;

            List<StrangleChart> activeAlgoObjects = _cache.Get<List<StrangleChart>>(okey);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<StrangleChart>();
            }
            activeAlgoObjects.Add(strangleChart);
            _cache.Set(okey, activeAlgoObjects);


            Task task = Task.Run(() => NMQClientSubscription(strangleChart, instrumentToken));

            ////await task;
            //return new Char
            //{
            //    aid = Convert.ToInt32(AlgoIndex.IVSpreadTrade),
            //    an = Convert.ToString((AlgoIndex)AlgoIndex.IVSpreadTrade),
            //    ains = algoInstance,
            //    algodate = endDateTime.ToString("yyyy-MM-dd"),
            //    binstrument = instrumentToken.ToString(),
            //    expiry = expiry1.ToString("yyyy-MM-dd"),
            //    lotsize = optionQuantity,
            //    mins = 0
            //};
        }

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

        private async Task NMQClientSubscription(StrangleChart buyWithRSI, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(buyWithRSI);
        }
        private void OptionBuywithRSI_OnOptionUniverseChange(StrangleChart source)
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
            return Task.FromResult((int)AlgoIndex.IVSpreadTrade);
        }
        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<StrangleChart> activeAlgoOptions;
            if (_cache.TryGetValue(okey, out activeAlgoOptions))
            {
                StrangleChart algoObject = activeAlgoOptions.FirstOrDefault(x => x.AlgoInstance == ain);
                if (algoObject != null)
                {
                    algoObject.StopTrade(!Convert.ToBoolean(start));
                }
                _cache.Set(okey, activeAlgoOptions);
            }
            return true;
        }

        //// GET api/<RSICrossController>/5
        //[HttpGet("{id}")]
        //public string Get(int id)
        //{
        //    return "value";
        //}

        //// POST api/<RSICrossController>
        //[HttpPost]
        //public void Post([FromBody] string value)
        //{
        //}

        //// PUT api/<RSICrossController>/5
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody] string value)
        //{
        //}

        //// DELETE api/<RSICrossController>/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}
    }
}
