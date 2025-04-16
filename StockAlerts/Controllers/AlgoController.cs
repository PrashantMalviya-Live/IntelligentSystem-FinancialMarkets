using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace StockAlerts.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AlgoController : ControllerBase
    {
        // GET: api/<AlgoController>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/<AlgoController>/5
        //[HttpGet("user/{id}")]
        //public List<Algos> GetActiveAlgos(int id)
        //{
        //    StockBreakout[] stockBreakouts = [
        //        new(){ Id=1, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(0,5, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
        //        new(){ Id=2, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(0,15, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
        //        new(){ Id=3, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(0,30, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
        //        new(){ Id=4, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(1,0, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
        //        new(){ Id=5, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(2,0,0), PercentageRetracement=0.3m, Completed = false, Success = true },

        //        new(){ Id=6, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(0,5, 0), PercentageRetracement=0.7m, Completed = true, Success = false },
        //        new(){ Id=7, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(0,15, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
        //        new(){ Id=8, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(0,30, 0), PercentageRetracement=0.5m, Completed = true, Success = true },
        //        new(){ Id=9, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(1,0, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
        //        new(){ Id=10, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(2,0,0), PercentageRetracement=0.2m, Completed = false, Success = false },

        //        new(){ Id=11, InstrumentToken=256265, TradingSymbol="NIFTYBANK", TimeFrame=new TimeSpan(0,5, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
        //        new(){ Id=12, InstrumentToken=256265, TradingSymbol="NIFTYBANK", TimeFrame=new TimeSpan(0,15, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
        //        new(){ Id=13, InstrumentToken=256265, TradingSymbol="NIFTYBANK", TimeFrame=new TimeSpan(0,30, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
        //        new(){ Id=14, InstrumentToken=256265, TradingSymbol="NIFTYBANK", TimeFrame=new TimeSpan(1,0, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
        //        new(){ Id=15, InstrumentToken=256265, TradingSymbol="NIFTYBANK", TimeFrame=new TimeSpan(0,5, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
        //        new(){ Id=16, InstrumentToken=256265, TradingSymbol="NIFTYBANK", TimeFrame=new TimeSpan(0,15, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
        //        new(){ Id=17, InstrumentToken=256265, TradingSymbol="NIFTYBANK", TimeFrame=new TimeSpan(0,30, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
        //        new(){ Id=18, InstrumentToken=256265, TradingSymbol="NIFTYBANK", TimeFrame=new TimeSpan(1,0, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
        //    ];
        //    return stockBreakouts.ToList();
        //}

        // GET api/<AlgoController>/5
        [HttpGet("{id}")]
        public List<StockBreakout> Get(int id)
        {
            StockBreakout[] stockBreakouts = [
                new(){ Id=1, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(0,5, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
                new(){ Id=2, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(0,15, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
                new(){ Id=3, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(0,30, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
                new(){ Id=4, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(1,0, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
                new(){ Id=5, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(2,0,0), PercentageRetracement=0.3m, Completed = false, Success = true },

                new(){ Id=6, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(0,5, 0), PercentageRetracement=0.7m, Completed = true, Success = false },
                new(){ Id=7, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(0,15, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
                new(){ Id=8, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(0,30, 0), PercentageRetracement=0.5m, Completed = true, Success = true },
                new(){ Id=9, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(1,0, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
                new(){ Id=10, InstrumentToken=260105, TradingSymbol="NIFTY", TimeFrame=new TimeSpan(2,0,0), PercentageRetracement=0.2m, Completed = false, Success = false },

                new(){ Id=11, InstrumentToken=256265, TradingSymbol="NIFTYBANK", TimeFrame=new TimeSpan(0,5, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
                new(){ Id=12, InstrumentToken=256265, TradingSymbol="NIFTYBANK", TimeFrame=new TimeSpan(0,15, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
                new(){ Id=13, InstrumentToken=256265, TradingSymbol="NIFTYBANK", TimeFrame=new TimeSpan(0,30, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
                new(){ Id=14, InstrumentToken=256265, TradingSymbol="NIFTYBANK", TimeFrame=new TimeSpan(1,0, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
                new(){ Id=15, InstrumentToken=256265, TradingSymbol="NIFTYBANK", TimeFrame=new TimeSpan(0,5, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
                new(){ Id=16, InstrumentToken=256265, TradingSymbol="NIFTYBANK", TimeFrame=new TimeSpan(0,15, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
                new(){ Id=17, InstrumentToken=256265, TradingSymbol="NIFTYBANK", TimeFrame=new TimeSpan(0,30, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
                new(){ Id=18, InstrumentToken=256265, TradingSymbol="NIFTYBANK", TimeFrame=new TimeSpan(1,0, 0), PercentageRetracement=0.3m, Completed = true, Success = true },
            ];
            return stockBreakouts.ToList();
        }

        // POST api/<AlgoController>
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/<AlgoController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<AlgoController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
    public class StockBreakout
    {
        public int Id { get; set; }
        public uint InstrumentToken { get; set; }
        public required string TradingSymbol { get; set; }
        public required TimeSpan TimeFrame { get; set; }
        public decimal PercentageRetracement { get; set; }
        public bool Completed { get; set; }
        public bool Success { get; set; }
    }
}
