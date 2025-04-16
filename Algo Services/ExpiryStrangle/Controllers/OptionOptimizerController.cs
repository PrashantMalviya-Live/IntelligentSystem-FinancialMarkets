using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using GlobalLayer;
using Algorithms.Utilities;
using ZMQFacade;
using System.Data;
using Microsoft.Extensions.Configuration;
using DataAccess;
using Newtonsoft.Json;
using Algos.TLogics;
using Algos.Utilities.Views;
using Global.Web;
using GlobalCore;
using System.Threading;

namespace OptionOptimizerController.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OptionOptimizerController : Controller
    {
        IConfiguration configuration;
        ZMQClient zmqClient;

        public OptionOptimizerController(IConfiguration config)
        {
            configuration = config;
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

        [HttpPost]
        public async Task<ActiveAlgosView> Trade([FromBody] OptionOptimizerInput optionOptimizerInput, int algoInstance = 0)
        {
            uint bInstrumentToken = optionOptimizerInput.BToken;
            DateTime endDateTime = optionOptimizerInput.EDT;

            DateTime[] expiries = new DateTime[3];
            expiries[0] = optionOptimizerInput.E1;
            expiries[1] = optionOptimizerInput.E2;
            expiries[2] = optionOptimizerInput.E3;

            decimal maxDrawdown = optionOptimizerInput.MDD;

            OptionOptimizer optionOptimizer = new OptionOptimizer(endDateTime, bInstrumentToken, maxDrawdown, expiries);
            optionOptimizer.OnOptionUniverseChange += OptionOptimizer_OnOptionUniverseChange;
            optionOptimizer.OnTradeEntry += OptionOptimizer_OnTradeEntry;
            optionOptimizer.OnTradeExit += OptionOptimizer_OnTradeExit;

            //activeAlgoObjects.Add(volumeThreshold);

            Task task = Task.Run(() => NMQClientSubscription(optionOptimizer, bInstrumentToken));

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.OptionOptimizer),
                an = Convert.ToString((AlgoIndex)AlgoIndex.OptionOptimizer),
                ains = optionOptimizer.AlgoInstance,
                algodate = endDateTime.ToString("yyyy-MM-dd"),
                binstrument = bInstrumentToken.ToString(),
                expiry = endDateTime.ToString("yyyy-MM-dd"),
                lotsize = 0,
                mins = 0
            };
        }
        private async Task NMQClientSubscription(OptionOptimizer optionOptimizer, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(optionOptimizer);
        }
        private void OptionOptimizer_OnTradeExit(Order st)
        {
            //publish trade details and count
            //Bind with trade token details, use that as an argument
            OrderCore.PublishOrder(st);
            Thread.Sleep(100);
        }

        private void OptionOptimizer_OnTradeEntry(Order st)
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

        [HttpGet("positions/all")]
        public IEnumerable<AlgoPosition> GetCurrentPositions()
        {
            DataLogic dl = new DataLogic();
            DataSet dsActiveStrangles = dl.RetrieveActiveStrangles(AlgoIndex.OptionOptimizer);

            return null;


            //var rng = new Random();
            //return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            //{
            //    Date = DateTime.Now.AddDays(index),
            //    TemperatureC = rng.Next(-20, 55),
            //    Summary = Summaries[rng.Next(Summaries.Length)]
            //})
            //.ToArray();
        }


        //[HttpGet("startservice")]
        //public void StartService()
        //{
        //    string baseInstrumentToken = "260105";

        //    //ZMQClient();
        //    ExpiryTrade optionOptimizer = new ExpiryTrade();
        //    optionOptimizer.OnOptionUniverseChange += OptionOptimizer_OnOptionUniverseChange;

        //    List<uint> tokens = new List<uint>();
        //    tokens.Add(260105);
        //    zmqClient = new ZMQClient();
        //    zmqClient.AddSubscriber(tokens);
        //    zmqClient.Subscribe(optionOptimizer);

        //    //ZMQClient.ZMQSubcribeAllTicks(optionOptimizer);

        //    //ZMQClient.ZMQSubcribebyToken(optionOptimizer, optionOptimizer.SubscriptionTokens.ToArray());

        //    //List<uint> tokens = new List<uint>();
        //    //tokens.Add(baseInstrumentToken);

        //    //IgniteMessanger.Subscribe(optionOptimizer, tokens);
        //    //IgniteConnector.QueryTickContinuous(tokens, optionOptimizer);

        //    //List<string> tokens = new List<string>();
        //    //tokens.Add(baseInstrumentToken);


        //    ////consumer.Subscribe(tokens);
        //    ////consumer.Consume(optionOptimizer, tokens);
        //    //Subscriber s = new Subscriber(optionOptimizer);
        //    //s.Subscribe(new List<uint>() { 260105 });
        //    //s.Listen();
        //}

        private void OptionOptimizer_OnOptionUniverseChange(OptionOptimizer source)
        {
            //ZMQClient.ZMQSubcribebyToken(source, source.SubscriptionTokens.ToArray());

            //IgniteMessanger.Subscribe(source, source.SubscriptionTokens.ToArray());

            try
            {
                zmqClient.AddSubscriber(source.SubscriptionTokens);
                // consumer.Subscribe(source.SubscriptionTokens.Select(x => x.ToString()).ToList());
            }
            catch (Exception ex)
            {
                throw ex;

            }
            //IgniteMessanger.Subscribe(source, source.SubscriptionTokens);
            //IgniteConnector.QueryTickContinuous(source.SubscriptionTokens, source);
        }

        [HttpGet("healthy")]
        public Task<int> Health()
        {
            return Task.FromResult((int)AlgoIndex.OptionOptimizer);
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
