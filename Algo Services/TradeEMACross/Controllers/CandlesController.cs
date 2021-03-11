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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace MarketView.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CandlesController : ControllerBase
    {
        private const string key = "ACTIVE_CANDLES_OBJECTS";
        ZMQClient zmqClient;
        private IMemoryCache _cache;

        public CandlesController(IMemoryCache cache)
        {
            this._cache = cache;
        }

        // GET: api/<CandlesController>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/<CandlesController>/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/<CandlesController>
        [HttpPost]
        public void StoreCandles([FromBody] int start)
        {
            uint instrumentToken = 260105;
            DateTime endDateTime = DateTime.Now;
            TimeSpan candleTimeSpan = TimeSpan.FromMinutes(5);
            uint volumeThreshold = 10000;
            uint moneyThreshold = 32000000;
            DateTime expiry = Convert.ToDateTime("2020-12-31");


            ///FOR ALL STOCKS FUTURE , PASS INSTRUMENTTOKEN AS ZERO. FOR CE/PE ON BNF/NF SEND THE INDEX TOKEN AS INSTRUMENTTOKEN
            GenerateCandles generateCandles =
                new GenerateCandles(expiry, volumeThreshold, moneyThreshold, candleTimeSpan);

            generateCandles.OnOptionUniverseChange += generateCandles_OnOptionUniverseChange;

            Task task = Task.Run(() => NMQClientSubscription(generateCandles, instrumentToken));

        }

        // PUT api/<CandlesController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<CandlesController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }

        private void CandleManger_MoneyCandleFinished(object sender, Candle e)
        {
            DataLogic dataLogic = new DataLogic();
            dataLogic.SaveCandle(e);
        }

        private void CandleManger_VolumeCandleFinished(object sender, Candle e)
        {
            DataLogic dataLogic = new DataLogic();
            dataLogic.SaveCandle(e);
        }
        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            DataLogic dataLogic = new DataLogic();
            dataLogic.SaveCandle(e);
        }

        private async Task NMQClientSubscription(GenerateCandles generateCandles, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(generateCandles);
        }

        private void generateCandles_OnOptionUniverseChange(GenerateCandles source)
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
    }
}
