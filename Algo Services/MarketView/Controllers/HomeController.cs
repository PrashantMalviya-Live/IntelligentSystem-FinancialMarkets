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
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
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


    }
}
