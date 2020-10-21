using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using GlobalLayer;
using Global.Web;
using Algorithms.Utilities;
using System.Text;
using System.Text.Json;
// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace MarketView.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        // GET: api/<HomeController>
        [HttpGet]
        public IEnumerable<BInstumentView> Get()
        {
            //ZConnect.ZerodhaLogin();
            
            DataLogic dl = new DataLogic();
            List<Instrument> bInstruments = dl.RetrieveBaseInstruments();

            //return JsonSerializer.Serialize(bInstruments);
            //return bInstruments.Select(x => new BInstumentView { x.InstrumentToken, x.TradingSymbol }).ToList();

            //List<BInstumentView> bInstumentViews = new List<BInstumentView>();
            //foreach (Instrument inst in bInstruments)
            //{
            //    BInstumentView bi = new BInstumentView() {InstrumentToken = inst.InstrumentToken, TradingSymbol= inst.TradingSymbol };
            //    bInstumentViews.Add(bi);
            //}

            //return bInstumentViews;
            //return JsonSerializer.Serialize(bInstumentViews);
            return (from n in bInstruments select new BInstumentView {InstrumentToken = n.InstrumentToken, TradingSymbol = n.TradingSymbol.Trim(' ') }).ToList();
        }

        // GET: api/<HomeController>
        [HttpGet]
        public IActionResult LoadTokens()
        {
            try
            {
                Utility.LoadTokens();

                 return Ok(StatusCode(200));
            }
            catch (Exception ex)
            {
                Logger.LogWrite(ex.StackTrace);
                return StatusCode(500);
            }
        }
    //}



        // GET api/<HomeController>/5
        [HttpGet("{token}")]
        public IEnumerable<string> OptionExpiries(uint token)
        {
            DataLogic dl = new DataLogic();

            List<string> expiryList = dl.RetrieveOptionExpiries(token);
            return expiryList;
        }


        // GET api/<HomeController>/5
        [HttpGet("{token}/{expiry}")]
        //[Route("GetOptions")]
        public IEnumerable<OptionView> GetOptions(uint token, string expiry)
        {
            //Retrieve expiry list ONLY here and fulllist at expiry click
            DataLogic dl = new DataLogic();
            List<Instrument> optionsList = dl.RetrieveOptions(token,
                Convert.ToDateTime(expiry));

            return (from n in optionsList select new OptionView { InstrumentToken = n.InstrumentToken, TradingSymbol = n.TradingSymbol.Trim(' '), Type = n.InstrumentType.Trim(' '), Strike = n.Strike }).ToList();
        }


        //// GET api/<HomeController>/5
        //[HttpGet("{token}/{expiry}")]
        //public IEnumerable<Instrument> GetCallOptions(uint token, string expiry)
        //{
        //    //Retrieve expiry list ONLY here and fulllist at expiry click
        //    DataLogic dl = new DataLogic();
        //    List<Instrument> optionsList = dl.RetrieveOptions(token,
        //        Convert.ToDateTime(expiry));

        //    return optionsList.Where(x => x.InstrumentType.ToLower().Trim(' ') == "ce");
        //}
        //// GET api/<HomeController>/5
        //[HttpGet("{token}/{expiry}")]
        //public IEnumerable<Instrument> GetPutOptions(uint token, string expiry)
        //{
        //    //Retrieve expiry list ONLY here and fulllist at expiry click
        //    DataLogic dl = new DataLogic();
        //    List<Instrument> optionsList = dl.RetrieveOptions(token,
        //        Convert.ToDateTime(expiry));

        //    return optionsList.Where(x => x.InstrumentType.ToLower().Trim(' ') == "pe");
        //}



        //// POST api/<HomeController>
        //[HttpPost]
        //public void Post([FromBody] string value)
        //{
        //}

        //// PUT api/<HomeController>/5
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody] string value)
        //{
        //}

        //// DELETE api/<HomeController>/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}
    }
}
