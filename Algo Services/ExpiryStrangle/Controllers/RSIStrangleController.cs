using Algos.TLogics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using ZMQFacade;

namespace ExpiryStrangle.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RSIStrangleController : ControllerBase
    {
        IConfiguration configuration;
        ZMQClient zmqClient;

        // GET: api/<RSIStrangleController>
        [HttpGet]
        public void Get()
        {
            StartService();
        }

        // GET api/<RSIStrangleController>/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }
        [HttpGet("startservice")]
        public void StartService()
        {
            //uint instrumentToken = 256265;
            //DateTime startDateTime = DateTime.Today.AddDays(-1).Add(new TimeSpan(15, 0, 0));
            //DateTime endDateTime = DateTime.Now;
            //TimeSpan candleTimeSpan = new TimeSpan(0, 3, 0);

            //BNF PE/ CE
            uint instrumentToken = 260105;
            DateTime startDateTime = DateTime.Today.AddDays(-1).Add(new TimeSpan(15, 0, 0));
            DateTime endDateTime = DateTime.Now;
            TimeSpan candleTimeSpan = new TimeSpan(0, 30, 0);

            DateTime? expiry = Convert.ToDateTime("2020-09-17");
            //decimal strikePriceRange = 1;

            ///TODO: Testing. Remove during Live operation
            endDateTime = Convert.ToDateTime("2020-09-11 09:15:00");
            startDateTime = Convert.ToDateTime("2020-09-10 15:00:00");

            //ZMQClient();
            ExpiryTradeWithRSI expiryTrade = new ExpiryTradeWithRSI(candleTimeSpan,instrumentToken, endDateTime, expiry);
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

        private void ExpiryTrade_OnOptionUniverseChange(ExpiryTradeWithRSI source)
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

        // POST api/<RSIStrangleController>
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/<RSIStrangleController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<RSIStrangleController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
