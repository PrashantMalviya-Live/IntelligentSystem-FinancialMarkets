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
    public class DeltaController : ControllerBase
    {
        private const string key = "ACTIVE_DELTA_TRADE_OBJECTS";
        IConfiguration configuration;
        ZMQClient zmqClient;
        private IMemoryCache _cache;
        public DeltaController(IMemoryCache cache)
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

        [HttpGet("activealgos")]
        public async Task<IEnumerable<ActiveAlgosView>> GetActiveAlgos()
        {
            DataLogic dl = new DataLogic();
            DataSet ds = dl.GetActiveAlgos(AlgoIndex.DeltaStrangle);

            List<ActiveAlgosView> activeAlgos = new List<ActiveAlgosView>();

            DataTable dtActiveAlgos = ds.Tables[0];
            DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

            foreach (DataRow drAlgo in dtActiveAlgos.Rows)
            {
                ActiveAlgosView algosView = new ActiveAlgosView();

                OptionBuywithRSIInput algoInput = new OptionBuywithRSIInput
                {
                    Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
                    CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
                    Qty = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
                    BToken = Convert.ToUInt32(drAlgo["BToken"]),
                    MinDFBI = Convert.ToDecimal(DBNull.Value != drAlgo["Arg5"] ? drAlgo["Arg5"] : 0),
                    MaxDFBI = Convert.ToDecimal(DBNull.Value != drAlgo["Arg4"] ? drAlgo["Arg4"] : 0),
                    //RULX = Convert.ToDecimal(DBNull.Value != drAlgo["UpperLimit"] ? drAlgo["UpperLimit"] : 0),
                    //RLLE = Convert.ToDecimal(DBNull.Value != drAlgo["LowerLimit"] ? drAlgo["LowerLimit"] : 0),
                    //EMA = Convert.ToInt32(DBNull.Value != drAlgo["Arg1"] ? drAlgo["Arg1"] : 0),
                    //PS = Convert.ToBoolean(DBNull.Value != drAlgo["PositionSizing"] ? drAlgo["PositionSizing"] : false),
                    TP = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0),
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

                var slmOrders = orders.Where(o => o.Status == Constants.ORDER_STATUS_TRIGGER_PENDING && o.OrderType == Constants.ORDER_TYPE_SLM);
                var completedOrders = orders.Where(o => o.Status == Constants.ORDER_STATUS_COMPLETE);

                var pendingOrders = slmOrders.Where(x => !completedOrders.Any(c => c.OrderId == x.OrderId));

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
        public async Task<ActiveAlgosView> Trade([FromBody] StrangleDetails strangleInput, int algoInstance = 0)
        {

            ManagedStrangleDelta manageDelta = new ManagedStrangleDelta();
            manageDelta.ManageStrangleDelta(strangleInput.peToken, strangleInput.ceToken, strangleInput.peSymbol, strangleInput.ceSymbol,
                Convert.ToDouble(strangleInput.pelowerThreshold), Convert.ToDouble(strangleInput.peUpperThreshold),
                Convert.ToDouble(strangleInput.celowerThreshold), Convert.ToDouble(strangleInput.ceUpperThreshold),
                strangleInput.stopLossPoints, strangleInput.strangleId);

            manageDelta.LoadActiveStrangles();
            //uint bInstrumentToken = optionExpiryStrangleInput.BToken;
            //DateTime endDateTime = DateTime.Now;
            //DateTime expiry = optionExpiryStrangleInput.Expiry;
            //int initialQuantity = optionExpiryStrangleInput.IQty;
            //int stepQuantity = optionExpiryStrangleInput.SQty;
            //int maxQuantity = optionExpiryStrangleInput.MQty;
            //int stoploss = optionExpiryStrangleInput.SL;
            //int targetProfit = optionExpiryStrangleInput.TP;
            //int minDistanceFromBase = optionExpiryStrangleInput.MDFBI;
            //int initialDistanceFromBase = optionExpiryStrangleInput.IDFBI;
            //int minPremiumToTrade = optionExpiryStrangleInput.MPTT;

            ////ZMQClient();
            //ExpiryTrade manageDelta = new ExpiryTrade(bInstrumentToken, expiry, initialQuantity,
            //    stepQuantity, maxQuantity, stoploss, minDistanceFromBase, minPremiumToTrade, initialDistanceFromBase, targetProfit);

            manageDelta.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;
            manageDelta.OnTradeEntry += ExpiryTrade_OnTradeEntry;
            manageDelta.OnTradeExit += ExpiryTrade_OnTradeExit;

            List<ExpiryTrade> activeAlgoObjects = _cache.Get<List<ExpiryTrade>>(key);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<ExpiryTrade>();
            }
            activeAlgoObjects.Add(manageDelta);
            _cache.Set(key, activeAlgoObjects);

            Task task = Task.Run(() => NMQClientSubscription(manageDelta, bInstrumentToken));

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.ExpiryTrade),
                an = Convert.ToString((AlgoIndex)AlgoIndex.ExpiryTrade),
                ains = manageDelta.AlgoInstance,
                algodate = endDateTime.ToString("yyyy-MM-dd"),
                binstrument = bInstrumentToken.ToString(),
                expiry = expiry.ToString("yyyy-MM-dd"),
                lotsize = maxQuantity,
                mins = 0
            };
        }
        private async Task NMQClientSubscription(ExpiryTrade manageDelta, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(manageDelta);
        }
        private void ExpiryTrade_OnTradeExit(Order st)
        {
            //publish trade details and count
            //Bind with trade token details, use that as an argument
            OrderCore.PublishOrder(st);
            Thread.Sleep(100);
        }

        private void ExpiryTrade_OnTradeEntry(Order st)
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

        private void ExpiryTrade_OnOptionUniverseChange(ExpiryTrade source)
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
            return Task.FromResult((int)AlgoIndex.ExpiryTrade);
        }

        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<ExpiryTrade> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<ExpiryTrade>();
            }

            ExpiryTrade algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
        }

        [HttpPost]
        public void ManageStrangleDelta(StrangleDetails strangle)
        {
            if(!ModelState.IsValid)
            {
                return;
            }
            ManagedStrangleDelta manageDelta = new ManagedStrangleDelta();
            manageDelta.ManageStrangleDelta(strangle.peToken, strangle.ceToken, strangle.peSymbol, strangle.ceSymbol, 
                Convert.ToDouble(strangle.pelowerThreshold), Convert.ToDouble(strangle.peUpperThreshold),
                Convert.ToDouble(strangle.celowerThreshold), Convert.ToDouble(strangle.ceUpperThreshold), 
                strangle.stopLossPoints, strangle.strangleId);
        }
        
    }
}