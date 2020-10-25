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

namespace ExpiryStrangle.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExpiryStrangleController : Controller
    {
        IConfiguration configuration;
        ZMQClient zmqClient;
        //KConsumer consumer = new KConsumer();
        public IActionResult Index()
        {
            return View();
        }

        public ExpiryStrangleController(IConfiguration config)
        {
            configuration = config;
        }
        private static readonly string[] Summaries = new[]
      {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        [HttpGet]
        public void Get()
        {
            StartService();

            //var rng = new Random();
            //return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            //{
            //    Date = DateTime.Now.AddDays(index),
            //    TemperatureC = rng.Next(-20, 55),
            //    Summary = Summaries[rng.Next(Summaries.Length)]
            //})
            //.ToArray();
        }

        [HttpGet("positions/all")]
        public IEnumerable<AlgoPosition> GetCurrentPositions()
        {
            DataLogic dl = new DataLogic();
            DataSet dsActiveStrangles = dl.RetrieveActiveStrangles(AlgoIndex.ExpiryTrade);

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


        [HttpGet("startservice")]
        public void StartService()
        {
            string baseInstrumentToken = "260105";
            
            //ZMQClient();
            ExpiryTrade expiryTrade = new ExpiryTrade();
            expiryTrade.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;

            List<uint> tokens = new List<uint>();
            tokens.Add(260105);
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(tokens);
            zmqClient.Subscribe(expiryTrade);

            //ZMQClient.ZMQSubcribeAllTicks(expiryTrade);
            
            //ZMQClient.ZMQSubcribebyToken(expiryTrade, expiryTrade.SubscriptionTokens.ToArray());

            //List<uint> tokens = new List<uint>();
            //tokens.Add(baseInstrumentToken);

            //IgniteMessanger.Subscribe(expiryTrade, tokens);
            //IgniteConnector.QueryTickContinuous(tokens, expiryTrade);

            //List<string> tokens = new List<string>();
            //tokens.Add(baseInstrumentToken);


            ////consumer.Subscribe(tokens);
            ////consumer.Consume(expiryTrade, tokens);
            //Subscriber s = new Subscriber(expiryTrade);
            //s.Subscribe(new List<uint>() { 260105 });
            //s.Listen();
        }



        private void ExpiryTrade_OnOptionUniverseChange(ExpiryTrade source)
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