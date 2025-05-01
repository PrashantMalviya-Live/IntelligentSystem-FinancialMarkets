using Algorithms.Utilities;
using Algos.Model;
using BrokerConnectWrapper;
using GlobalLayer;
using FyersConnect;
//using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using KiteConnect;

namespace StockAlerts.Controllers
{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class FyersLoginController : ControllerBase
//    {
//        //// GET: api/<LoginController>

//        [HttpGet]
//        [ProducesResponseType(StatusCodes.Status200OK)]
//        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
//        public ActionResult<BrokerLoginParams> GetLogin(string redirectUri)
//        {
//            ObjectResult result;
//            try
//            {
//#if MARKET || AWSMARKET
//                Login l = new Login();
//                User activeUser = l.GetActiveUser(2);
//                Fy fy = new Fy(activeUser.APIKey);

//                //fy.generateAuthCode(activeUser.UserId, activeUser.APIKey, redirectUri);
//                string auth_code = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJhcGkubG9naW4uZnllcnMuaW4iLCJpYXQiOjE3MzE0MTY3MjcsImV4cCI6MTczMTQ0NjcyNywibmJmIjoxNzMxNDE2MTI3LCJhdWQiOlsieDowIiwieDoxIiwieDoyIiwiZDoxIiwieDoxIiwieDowIl0sInN1YiI6ImF1dGhfY29kZSIsImRpc3BsYXlfbmFtZSI6IlhQMTUzODYiLCJvbXMiOiJLMSIsImhzbV9rZXkiOm51bGwsIm5vbmNlIjoiIiwiYXBwX2lkIjoiRk9EQlZGS1Y3NyIsInV1aWQiOiJhNGM2MzI4YTRkNjM0ODZhOGMwNDRkZGU2YTVmZDJmNCIsImlwQWRkciI6IjI0MDE6NDkwMDo4ODlhOjk0Mzk6YTFiZDphYTYzOmE2YzE6MjNiLCAxNjIuMTU4LjIzNS4zOSIsInNjb3BlIjoiIn0.SrAAeWoLw5CDbacUYeaTKActRo95gTf5U1tJPV03Jmg";
//                var fyTokenTask = fy.generateAccesToken(activeUser.UserId, activeUser.APIKey, redirectUri, auth_code);
//                fyTokenTask.Wait();
//                activeUser.AccessToken = fyTokenTask.Result.TOKEN;
//                l.UpdateUser(activeUser);

//                var zResponse = new BrokerLoginParams() { Status = "200 OK", AccessToken = activeUser.AccessToken, login = true, ClientId = activeUser.UserId, ClientName = activeUser.UserShortName };

//                //result = new OkObjectResult(new { StatusCode = 200, Status = "200 OK", accessToken = activeUser.AccessToken, clientId = activeUser.UserId, userName = activeUser.UserShortName });
//                result = new OkObjectResult(zResponse);

//#else

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


//        //        [HttpPost]
//        //        public IActionResult Login([FromQuery] string? request_token, [FromQuery] string? action, [FromQuery] string? status, [FromQuery] string? type)
//        //        //public IActionResult Login([FromBody] LoginParams data)
//        //        {
//        //            ObjectResult result;
//        //            try
//        //            {
//        //#if MARKET || AWSMARKET
//        //                Login l = new Login();
//        //                User activeUser = l.GetActiveUser(0);
//        //                Kite kite = new Kite(activeUser.APIKey);

//        //                if (request_token == null)
//        //                {
//        //                    string loginUrl = kite.GetLoginURL();

//        //                    //TODO Commented for new UI
//        //                    //result = new OkObjectResult(new { message = "401 Unauthorized", login = true, url = loginUrl });


//        //                    result = new UnauthorizedObjectResult(new { message = "401 Unauthorized", login = true, url = loginUrl });
//        //                    return result;
//        //                }
//        //                else
//        //                {
//        //                    //string request_token = data.request_token;
//        //                    activeUser = kite.GenerateSession(request_token, activeUser.AppSecret);
//        //                    ZConnect.Login(activeUser);
//        //                    l.UpdateUser(activeUser);
//        //                }

//        //                result = new OkObjectResult(new { message = "200 OK", userName = activeUser.UserShortName });
//        //#endif
//        //#if local
//        //                result = new OkObjectResult(new { message = "200 OK", userName = "Test" });
//        //#endif
//        //                return result;
//        //            }
//        //            catch (Exception ex)
//        //            {
//        //                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
//        //                return StatusCode(500);
//        //            }
//        //        }

//    }
}
