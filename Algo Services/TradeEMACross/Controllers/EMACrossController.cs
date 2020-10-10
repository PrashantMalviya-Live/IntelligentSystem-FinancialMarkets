using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Algos.TLogics;
using ZMQFacade;
using DataAccess;
//using TibcoMessaging;
namespace TradeEMACross.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EMACrossController : Controller
    {
        ZMQClient zmqClient;
        public IActionResult Index()
        {
            return View();
        }

        // GET: api/<EMACrossController>
        [HttpGet]
        public void Get()
        {
            StartService();
        }

        [HttpGet("startservice")]
        public void StartService()
        {
            uint instrumentToken = 260105;// 11622914;// LIVE

            //uint instrumentToken = 12516098; //LOCAL
            TimeSpan candleTimeSpan = new TimeSpan(0, 1, 0);
            int fEma = 375, sEMA = 500;

            //ZMQClient();
            SingleEMACross singleEMACross = new SingleEMACross(instrumentToken, candleTimeSpan, fEma, sEMA);
            singleEMACross.OnOptionUniverseChange += SingleEMACross_OnOptionUniverseChange;
            //ZMQClient.ZMQSubcribeAllTicks(expiryTrade);


            //expiryTrade.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;
            //ZMQClient.ZMQSubcribebyToken(expiryTrade, expiryTrade.SubscriptionTokens.ToArray());

            List<uint> tokens = new List<uint>();
            tokens.Add(instrumentToken);

            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(tokens);
            zmqClient.Subscribe(singleEMACross);

            //Subscriber s = new Subscriber(singleEMACross);
            //s.Subscribe(new List<uint>() { 260105 });
            //IgniteMessanger.Subscribe(singleEMACross, tokens);

            //IgniteConnector.QueryTickContinuous(tokens, singleEMACross);


        }

        private void SingleEMACross_OnOptionUniverseChange(SingleEMACross source)
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

        //// GET api/<EMACrossController>/5
        //[HttpGet("{id}")]
        //public string Get(int id)
        //{
        //    return "value";
        //}

        //// POST api/<EMACrossController>
        //[HttpPost]
        //public void Post([FromBody] string value)
        //{
        //}

        //// PUT api/<EMACrossController>/5
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody] string value)
        //{
        //}

        //// DELETE api/<EMACrossController>/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}
    }
}
