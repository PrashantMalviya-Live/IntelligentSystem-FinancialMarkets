using Microsoft.AspNetCore.Mvc;
using GlobalLayer;
using Microsoft.AspNetCore.Mvc;
using Algorithms.Utilities;
using KotakConnect;
using BrokerConnectWrapper;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace StockAlerts.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        // GET: api/<AccountController>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }


        //[HttpGet("BrokerLogins/{userid}")]
        //public IEnumerable<BrokerLoginParams> LoadBrokerLogins()
        //{
        //    try
        //    {

        //        Utility.LoadKotakTokens();

        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
        //        return StatusCode(500);
        //    }
        //}


        // GET api/<AccountController>/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/<AccountController>
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/<AccountController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<AccountController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
