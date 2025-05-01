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
using System.Text.Json;
using DBAccess;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace StockAlerts.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class KotakLoginController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string okey = "ACTIVE_STRTrade_OPTIONS";
        private const string ckey = "ACTIVE_CALTrade_OPTIONS";
        private const string userKey = "ACTIVE_USER";

        private IMemoryCache _cache;
        private readonly IRDSDAO _rdsDAO;
        public KotakLoginController(IMemoryCache cache, IHttpClientFactory httpClientFactory, IRDSDAO rdsDAO)
        {
            this._cache = cache;
            this._httpClientFactory = httpClientFactory;
            _rdsDAO = rdsDAO;
        }

        //// GET: api/<LoginController>
        [HttpPost]
        //public IActionResult Login([FromQuery] string request_token, [FromQuery] string action, [FromQuery] string status)
        public IActionResult Login([FromBody] LoginParams data)
        {
            ObjectResult result;
            try
            {
#if MARKET || AWSMARKET
                

                //Kotak kotak = new Kotak(activeUser.APIKey, data.accessToken);
                //kotak.Login(data.userid, data.pwd);

               // ZObjects.kotak = new Kotak(activeUser.APIKey, data.accessToken, activeUser.RootServer);
                ZObjects.kotak ??= new Kotak();

                User activeUser = _cache.Get<User>(userKey);

                Login l = new Login(_rdsDAO);
                if (activeUser == null)
                {
                    
                    activeUser = l.GetActiveUser(1, data.userid);
                    activeUser.AccessToken = data.accessToken;

                    var httpClient = KoConnect.ConfigureHttpClient2(activeUser, _httpClientFactory.CreateClient());
                    string sid, vToken;

                    string mobilenumber = string.Empty;
                    ZObjects.kotak.GenerateInitialToken(mobilenumber, data.pwd, httpClient, out sid, out vToken);

                    activeUser.SID = sid;
                    activeUser.vToken = vToken;
                    _cache.Set(userKey, activeUser);

                    result = new ObjectResult(JsonSerializer.Serialize(new KotakLoginResponse() { statusCode = 418, message = "418 TP", userName = "" }));
                }
                else
                {
                    var httpClient = KoConnect.ConfigureHttpClient2(activeUser, _httpClientFactory.CreateClient());
                    string hsServerId;
                    string sessionToken = ZObjects.kotak.GenerateSessionToken(activeUser.SID, activeUser.vToken, data.otp, httpClient, out hsServerId);
                    activeUser.SessionToken = sessionToken;
                    activeUser.HsServerId = hsServerId;
                    activeUser.ApplicationUserId = data.applicationUserId;
                    l.UpdateUser(activeUser);

                    result = new OkObjectResult(new KotakLoginResponse() { statusCode = 200, message = "200 OK", userName = "" });
                }
                //activeAlgoObjects.Add(paTrader);
                //_cache.Set(key, activeAlgoObjects);

                //ZObjects.kotak.Login(data.userid, data.pwd, httpClient, out sid, out hsServerId, out vToken);

                //string sessionToken = ZObjects.kotak.Login(data.userid, data.pwd, httpClient, out sid, out hsServerId, out vToken);
                ////string token = kotak.GenerateOTT(data.userid, data.pwd);
                
                

                
#else
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

        //[HttpGet]
        //public IActionResult LoadTokens()
        //{
        //    try
        //    {
        //        Utility.LoadKotakTokens();

        //        return Ok(StatusCode(200));
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
        //        return StatusCode(500);
        //    }
        //}


        public class LoginParams
        {
            public string userid { get; set; }
            public string pwd { get; set; }

            public string? otp { get; set; }
            public string accessToken { get; set; }

            public string applicationUserId { get; set; }
        }
    }


}
