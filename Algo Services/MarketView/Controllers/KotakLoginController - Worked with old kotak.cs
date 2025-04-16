using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GlobalLayer;
using Microsoft.AspNetCore.Mvc;
using Algorithms.Utilities;
using KotakConnect;
using BrokerConnectWrapper;
using Microsoft.AspNetCore.Cors;
using System.ServiceModel.Channels;
using System.Net.Http;
using Microsoft.Extensions.Caching.Memory;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace MarketView.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class KotakLoginController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string okey = "ACTIVE_STRTrade_OPTIONS";
        private const string ckey = "ACTIVE_CALTrade_OPTIONS";

        private IMemoryCache _cache;
        public KotakLoginController(IMemoryCache cache, IHttpClientFactory httpClientFactory)
        {
            this._cache = cache;
            this._httpClientFactory = httpClientFactory;
        }

        //// GET: api/<LoginController>
        [HttpPost]
        //public IActionResult Login([FromQuery] string request_token, [FromQuery] string action, [FromQuery] string status)
        public IActionResult Login([FromBody] LoginParams data)
        {
            OkObjectResult result;
            try
            {
#if market
                Login l = new Login();

                User activeUser = l.GetActiveUser(1, data.userid);

                //Kotak kotak = new Kotak(activeUser.APIKey, data.accessToken);
                //kotak.Login(data.userid, data.pwd);

               // ZObjects.kotak = new Kotak(activeUser.APIKey, data.accessToken, activeUser.RootServer);
                ZObjects.kotak ??= new Kotak();
                activeUser.AccessToken = data.accessToken;
                var httpClient = KoConnect.ConfigureHttpClient(activeUser, _httpClientFactory.CreateClient());
                string sessionToken = ZObjects.kotak.Login(data.userid, data.pwd, httpClient);
                //string token = kotak.GenerateOTT(data.userid, data.pwd);
                
                activeUser.SessionToken = sessionToken;

                l.UpdateUser(activeUser);

                result = new OkObjectResult(new { message = "200 OK", userName = "" });
#endif
#if local
                result = new OkObjectResult(new { message = "200 OK", userName = "Test" });

#endif
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                return StatusCode(500);
            }
        }

        [HttpGet]
        public IActionResult LoadTokens()
        {
            try
            {
                Utility.LoadKotakTokens();

                return Ok(StatusCode(200));
            }
            catch (Exception ex)
            {
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                return StatusCode(500);
            }
        }

        public class LoginParams
        {
            public string userid { get; set; }
            public string pwd { get; set; }

            public string accessToken { get; set; }
        }
    }


}
