using Algorithms.Utilities;
using BrokerConnectWrapper;
using DBAccess;
using GlobalLayer;
using KiteConnect;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace LocalDBData
{
    public class ZLogin
    {
        private readonly IRDSDAO _rdsDAO;
        public ZLogin(IRDSDAO rdsDAO)
        {
            _rdsDAO = rdsDAO;
        }
        public bool Login()//LoginParams data)
        {
            try
            {
                Login l = new Login(_rdsDAO);
                User activeUser = l.GetActiveUser(0);
                Kite kite = new Kite(activeUser.APIKey);

                //if (data.request_token == null)
                //{
                string loginUrl = kite.GetLoginURL();

                WebClient client = new WebClient();

                // Add a user agent header in case the
                // requested URI contains a query.

                client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = loginUrl,
                    UseShellExecute = true
                });


                //    result = new OkObjectResult(new { message = "401 Unauthorized", login = true, url = loginUrl });
                //    return result;
                //}
                //else
                //{
                string request_token = "OHpH5BBFo3eKZWnSuuKscW2rRZ800L4P";
                activeUser = kite.GenerateSession(request_token, activeUser.AppSecret);
                //ZConnect.Login(activeUser);
                //l.UpdateUser(activeUser);
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

        public class LoginParams
        {
            public string request_token { get; set; }
            public string action { get; set; }
            public string status { get; set; }
        }
    }
}
