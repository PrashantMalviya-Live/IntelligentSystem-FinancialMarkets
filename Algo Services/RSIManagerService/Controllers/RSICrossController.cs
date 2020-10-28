using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using GlobalLayer;
using Algos.TLogics;
using Algorithms.Utilities;
using ZMQFacade;
using System.Data;
using Microsoft.Extensions.Configuration;
using Algorithms.Algorithms;
using Algorithms.Utilities.Views;
using Global.Web;
using GlobalCore;
using System.Threading;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace RSIManagerService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RSICrossController : ControllerBase
    {
        IConfiguration configuration;
        ZMQClient zmqClient;
        List<OptionSellOnRSICross> activeAlgoObjects;
        public RSICrossController()
        {
            activeAlgoObjects = new List<OptionSellOnRSICross>();
            // GetActiveAlgos();
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
            DataLogic dl = new DataLogic();
            DataSet ds = dl.GetActiveAlgos(AlgoIndex.MomentumTrade_Option);

            List<ActiveAlgosView> activeAlgos = new List<ActiveAlgosView>();

            DataTable dtActiveAlgos = ds.Tables[0];
            DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

            foreach (DataRow drAlgo in dtActiveAlgos.Rows)
            {
                ActiveAlgosView algosView = new ActiveAlgosView();

                OptionMomentumInput algoInput = new OptionMomentumInput
                {
                    Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
                    CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
                    Quantity = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
                    Token = Convert.ToUInt32(drAlgo["BToken"]),
                    PS = Convert.ToBoolean(DBNull.Value != drAlgo["PositionSizing"] ? drAlgo["PositionSizing"] : false),
                    MLPT = Convert.ToDecimal(DBNull.Value != drAlgo["MaxLossPerTrade"] ? drAlgo["MaxLossPerTrade"] : 0),
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

                if (pendingOrders != null && pendingOrders.Count() > 0)
                {
                    algoInput.ActiveOrder = pendingOrders.FirstOrDefault();
                    algosView.Orders.Add(ViewUtility.GetOrderView(pendingOrders.FirstOrDefault()));
                    if (algoInput.ActiveOrder != null)
                    {
                        Trade(algoInput, algosView.ains);
                    }
                    activeAlgos.Add(algosView);
                }
            }
            return activeAlgos.ToArray();
        }


        [HttpPost]
        public async Task<ActiveAlgosView> Trade([FromBody] OptionMomentumInput optionMomentumInput, int algoInstance = 0)
        {
            uint instrumentToken = optionMomentumInput.Token;
            DateTime endDateTime = DateTime.Now;
            TimeSpan candleTimeSpan = TimeSpan.FromMinutes(optionMomentumInput.CTF);

            DateTime? expiry = optionMomentumInput.Expiry; // Convert.ToDateTime("2020-10-01");
            //decimal strikePriceRange = 1;
            int optionQuantity = optionMomentumInput.Quantity;

#if local
            endDateTime = Convert.ToDateTime("2020-10-16 12:21:00");
#endif

            ///FOR ALL STOCKS FUTURE , PASS INSTRUMENTTOKEN AS ZERO. FOR CE/PE ON BNF/NF SEND THE INDEX TOKEN AS INSTRUMENTTOKEN
            OptionSellOnRSICross sellOnRSICross =
                new OptionSellOnRSICross(candleTimeSpan, instrumentToken, endDateTime, expiry,
                optionQuantity, algoInstance);

            Order activeOrder = optionMomentumInput.ActiveOrder;
            if (activeOrder != null)
            {
                sellOnRSICross.LoadActiveOrders(activeOrder);
            }

            sellOnRSICross.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;
            //sellOnRSICross.OnTradeEntry +=
            //sellOnRSICross.OnTradeExit += 

            activeAlgoObjects.Add(sellOnRSICross);


            Task task = Task.Run(() => NMQClientSubscription(sellOnRSICross, instrumentToken));

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.MomentumTrade_Option),
                an = Convert.ToString((AlgoIndex)AlgoIndex.MomentumTrade_Option),
                ains = sellOnRSICross.AlgoInstance,
                algodate = endDateTime.ToString("yyyy-MM-dd"),
                binstrument = instrumentToken.ToString(),
                expiry = expiry.HasValue ? expiry.Value.ToString("yyyy-MM-dd") : "",
                lotsize = optionQuantity,
                mins = optionMomentumInput.CTF
            };
        }

        private async Task NMQClientSubscription(OptionSellOnRSICross sellOnRSICross, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(sellOnRSICross);
        }
        // GET: api/<RSIStrangleController>
        //[HttpGet]
        //public void Get()
        //{
        //    StartService();
        //}

//        [HttpGet("startservice")]
//        public void StartService()
//        {
//            //BNF PE/ CE
//            uint instrumentToken = 260105;
//            DateTime endDateTime = DateTime.Now;
//            TimeSpan candleTimeSpan = new TimeSpan(1, 15, 0);

//            DateTime? expiry = Convert.ToDateTime("2020-10-29");
//            //decimal strikePriceRange = 1;

//#if local
//            endDateTime = Convert.ToDateTime("2020-10-29 09:15:00");
//#endif
//            OptionSellOnRSICross expiryTrade = new OptionSellOnRSICross(candleTimeSpan, instrumentToken, endDateTime, expiry);
//            expiryTrade.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;

//            List<uint> tokens = new List<uint>();
//            tokens.Add(instrumentToken);
//            zmqClient = new ZMQClient();
//            zmqClient.AddSubscriber(tokens);
//            zmqClient.Subscribe(expiryTrade);
//        }

        private void ExpiryTrade_OnOptionUniverseChange(OptionSellOnRSICross source)
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
            return Task.FromResult((int)AlgoIndex.SellOnRSICross);
        }

        // GET api/<RSICrossController>/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/<RSICrossController>
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/<RSICrossController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<RSICrossController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
