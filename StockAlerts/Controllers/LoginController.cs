using Algorithms.Utilities;
using Algos.Model;
using BrokerConnectWrapper;
using GlobalLayer;
using KiteConnect;
//using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace StockAlerts.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        //// GET: api/<LoginController>

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<BrokerLoginParams> GetLogin([FromQuery] string? request_token, [FromQuery] string? action, [FromQuery] string? status, [FromQuery] string? type)
        //public IActionResult Login([FromBody] LoginParams data)
        {
            ObjectResult result;
            try
            {
#if MARKET
                Login l = new Login();
                User activeUser = l.GetActiveUser(0);
                Kite kite = new Kite(activeUser.APIKey);

                if (request_token == null)
                {
                    string loginUrl = kite.GetLoginURL();

                    //TODO Commented for new UI
                    //result = new OkObjectResult(new { message = "401 Unauthorized", login = true, url = loginUrl });
                    //var zResponse = new LoginParams(){ action="1", request_token="11", status="22"};
                    var intermediateResponse = new BrokerLoginParams() { Status = "200 Ok", login = false, ClientId = "", url = loginUrl };

                    //result = new UnauthorizedObjectResult(new { Status = "401 Unauthorized", login = true, ClientId = "", url = loginUrl });
                    result = new OkObjectResult(intermediateResponse);
                    return result;
                }
                else
                {
                    //string request_token = data.request_token;
                    activeUser = kite.GenerateSession(request_token, activeUser.AppSecret);
                    ZConnect.Login(activeUser);
                    l.UpdateUser(activeUser);
                }
                var zResponse = new BrokerLoginParams() { Status = "200 OK", AccessToken = activeUser.AccessToken, login = true, ClientId = activeUser.UserId, ClientName = activeUser.UserShortName };

                //result = new OkObjectResult(new { StatusCode = 200, Status = "200 OK", accessToken = activeUser.AccessToken, clientId = activeUser.UserId, userName = activeUser.UserShortName });
                result = new OkObjectResult(zResponse);

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


//        [HttpPost]
//        public IActionResult Login([FromQuery] string? request_token, [FromQuery] string? action, [FromQuery] string? status, [FromQuery] string? type)
//        //public IActionResult Login([FromBody] LoginParams data)
//        {
//            ObjectResult result;
//            try
//            {
//#if MARKET
//                Login l = new Login();
//                User activeUser = l.GetActiveUser(0);
//                Kite kite = new Kite(activeUser.APIKey);

//                if (request_token == null)
//                {
//                    string loginUrl = kite.GetLoginURL();

//                    //TODO Commented for new UI
//                    //result = new OkObjectResult(new { message = "401 Unauthorized", login = true, url = loginUrl });

                    
//                    result = new UnauthorizedObjectResult(new { message = "401 Unauthorized", login = true, url = loginUrl });
//                    return result;
//                }
//                else
//                {
//                    //string request_token = data.request_token;
//                    activeUser = kite.GenerateSession(request_token, activeUser.AppSecret);
//                    ZConnect.Login(activeUser);
//                    l.UpdateUser(activeUser);
//                }

//                result = new OkObjectResult(new { message = "200 OK", userName = activeUser.UserShortName });
//#endif
//#if local
//                result = new OkObjectResult(new { message = "200 OK", userName = "Test" });
//#endif
//                return result;
//            }
//            catch (Exception ex)
//            {
//                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
//                return StatusCode(500);
//            }
//        }

        public class LoginParams
        {
            public string? request_token { get; set; }
            public string? action { get; set; }
            public string? status { get; set; }
        }
    }
}
