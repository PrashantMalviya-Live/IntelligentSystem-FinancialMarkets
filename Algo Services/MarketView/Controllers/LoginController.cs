using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GlobalLayer;
using Microsoft.AspNetCore.Mvc;
using Algorithms.Utilities;
using KiteConnect;
using ZConnectWrapper;
using Microsoft.AspNetCore.Cors;
using System.ServiceModel.Channels;

namespace MarketView.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
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
                User activeUser = l.GetActiveUser();
                Kite kite = new Kite(activeUser.APIKey);

                if (data.request_token == null)
                {
                    string loginUrl = kite.GetLoginURL();

                    result = new OkObjectResult(new { message = "401 Unauthorized", login = true, url = loginUrl });
                    return result;
                }
                else
                {
                    string request_token = data.request_token;
                    activeUser = kite.GenerateSession(request_token, activeUser.AppSecret);
                    ZConnect.Login(activeUser);
                    l.UpdateUser(activeUser);
                }

                result = new OkObjectResult(new { message = "200 OK", userName = activeUser.UserShortName });
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

        public class LoginParams
        {
            public string request_token { get; set; }
            public string action { get; set; }
            public string status { get; set; }
        }
       

        //public IActionResult Login([FromBody] object quaryParams)
        //{
        //    Login l = new Login();
        //    try
        //    {

        //        User activeUser = l.GetActiveUser();
        //        Kite kite = new Kite(activeUser.APIKey);
        //        OkObjectResult result;
        //        if (request_token == null)
        //        {
        //            string loginUrl = kite.GetLoginURL();

        //            result = new OkObjectResult(new { message = "401 Unauthorized", login = true, url = loginUrl });
        //            return result;
        //        }
        //        else
        //        {
        //            activeUser = kite.GenerateSession(request_token, activeUser.AppSecret);
        //            ZConnect.Login(activeUser);
        //            l.UpdateUser(activeUser);
        //        }
        //        result = new OkObjectResult(new { message = "200 OK" });
        //        Response.Redirect("http://localhost:4200/");
        //        return result;
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
        //        return StatusCode(500);
        //    }
        //}
    }
}
