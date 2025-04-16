using Algos.TLogics;
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
using Algos.Utilities.Views.ModelViews;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;
using Algorithm.Algorithm;


namespace StockAlerts.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MarketController : ControllerBase
    {
        [HttpGet("instruments")]
        public IEnumerable<InstrumentListView> GetInstrumentList()
        {
            DataLogic dl = new DataLogic();
            List<InstrumentListView> instrumentListView = dl.GetInstrumentList();
            return instrumentListView;
        }
        [HttpGet("indicators")]
        public IEnumerable<IndicatorOperatorView> GetIndicatorList()
        {
            DataLogic dl = new DataLogic();
            List<IndicatorOperatorView> indcatorListView = dl.GetIndicatorList();
            return indcatorListView;
        }

        //Dictionary<Indicator Id, Dictionary<PropertyId, LisT<PropertyoptionId1, propertyoptionvalue1>>>
        //[HttpGet("indicatorpropertyoptionvalues")]
        //public IEnumerable<int, Dictionary<string, List<string, string>> GetIndicatorPropertyOptions()
        //{
        //    DataLogic dl = new DataLogic();
        //    return dl.GetIndicatorPropertyOptions(-1);
        //}
        [HttpGet("candletimeframes")]
        public IEnumerable<CandleTimeFramesView> GetCandleTimeFrameList()
        {
            DataLogic dl = new DataLogic();
            List<CandleTimeFramesView> candleTimeFramesView = dl.GetCandleTimeFrameList();
            return candleTimeFramesView;
        }

        // GET api/<MarketController>/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/<MarketController>
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/<MarketController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<MarketController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
