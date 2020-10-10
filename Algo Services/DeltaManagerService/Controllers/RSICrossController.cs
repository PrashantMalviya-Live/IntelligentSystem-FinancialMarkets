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

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace ExpiryStrangle.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RSICrossController : ControllerBase
    {
        IConfiguration configuration;
        ZMQClient zmqClient;

        // GET: api/<RSIStrangleController>
        [HttpGet]
        public void Get()
        {
            StartService();
        }

        [HttpGet("startservice")]
        public void StartService()
        {
            //BNF PE/ CE
            uint instrumentToken = 260105;
            DateTime endDateTime = DateTime.Now;
            TimeSpan candleTimeSpan = new TimeSpan(1, 15, 0);

            DateTime? expiry = Convert.ToDateTime("2020-10-29");
            //decimal strikePriceRange = 1;

#if local
            endDateTime = Convert.ToDateTime("2020-10-29 09:15:00");
#endif
            OptionSellOnRSICross expiryTrade = new OptionSellOnRSICross(candleTimeSpan, instrumentToken, endDateTime, expiry);
            expiryTrade.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;

            List<uint> tokens = new List<uint>();
            tokens.Add(instrumentToken);
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(tokens);
            zmqClient.Subscribe(expiryTrade);
        }

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
