using Algorithms.Utilities;
using BrokerConnectWrapper;
using FyersConnect;
using GlobalLayer;
using KiteConnect;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LocalDBData
{
    internal class FyLogin
    {
        public static bool Login()//LoginParams data)
        {
            try
            {
                Login l = new Login();
                User activeUser = l.GetActiveUser(2);
                Fy fy = new Fy(activeUser.APIKey);

                fy.generateAuthCode(activeUser.UserId, activeUser.APIKey, activeUser.RootServer);
                string auth_code = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJhcGkubG9naW4uZnllcnMuaW4iLCJpYXQiOjE3MzIzMzc5OTksImV4cCI6MTczMjM2Nzk5OSwibmJmIjoxNzMyMzM3Mzk5LCJhdWQiOiJbXCJ4OjBcIiwgXCJ4OjFcIiwgXCJ4OjJcIiwgXCJkOjFcIiwgXCJ4OjFcIiwgXCJ4OjBcIl0iLCJzdWIiOiJhdXRoX2NvZGUiLCJkaXNwbGF5X25hbWUiOiJYUDE1Mzg2Iiwib21zIjoiSzEiLCJoc21fa2V5IjoiMDhmYTc0YmM5NGIxZGYwZDQ5NGMxZmU0ZmU3ZWZmNjk3MzZkMDMyYWExNmNlYjQyZjc1YWM1YTciLCJub25jZSI6IiIsImFwcF9pZCI6IkZPREJWRktWNzciLCJ1dWlkIjoiNDgzZmEzODQ0YTg2NDAwZWIwMzNmNmY3OWQ3ZDhmMTMiLCJpcEFkZHIiOiIwLjAuMC4wIiwic2NvcGUiOiIifQ.jCAySLvVGtqxPTnj2SiW5P6-b0Keoa3pSom7iTmUj30";
                var fyTokenTask = fy.generateAccesToken(activeUser.UserId, activeUser.APIKey, activeUser.RootServer, auth_code);

                fyTokenTask.Wait();
                var token = fyTokenTask.Result;
                activeUser.AccessToken = token.TOKEN;

                //WebClient client = new WebClient();

                //// Add a user agent header in case the
                //// requested URI contains a query.

                //client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                //System.Diagnostics.Process.Start(new ProcessStartInfo
                //{
                //    FileName = loginUrl,
                //    UseShellExecute = true
                //});

                l.UpdateUser(activeUser);
                
                // }

                // result = new OkObjectResult(new { message = "200 OK", userName = activeUser.UserShortName });
                //          result = new OkObjectResult(new { message = "200 OK", userName = "Test" });
                // return result;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                return false;// StatusCode(500);
            }
        }
    }
}
