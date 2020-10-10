using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KiteConnect;
using GlobalLayer;
using System.Net.Http;
using System.Configuration;
namespace ZConnectWrapper
{
    public class ZConnect
    {
        // Initialize key and secret of your app
        public static string MyAPIKey = "af61rvtidnnnyp8p";
        //static string MySecret = "if1ur4umqitbi8kotw95iyuhuinlcj0i";
        // persist these data in settings or db or file
        public static string MyAccessToken = "UzC9W0dzS1IgVmKHFhB6yn5cA7CQUW3r";
        public static string CurrentUser = "Prashant Malviya";
        public static string RequestToken = "";

        public static bool ZerodhaLogin(string requestToken = "")
        {
            ZObjects.kite = new Kite(MyAPIKey, Debug: true);

            // For handling 403 errors
            ZObjects.kite.SetSessionExpiryHook(OnTokenExpire);

            // Initializes the login flow

            try
            {
                InitSession();
                ZObjects.kite.SetAccessToken(MyAccessToken);
            }
            catch (Exception e)
            {
                return false;
                // Cannot continue without proper authentication
                //Logger.LogWrite(e.Message);
                // Environment.Exit(0);
            }

            return true;
        }
        private static void InitSession()
        {
            //string loginUrl = ZObjects.kite.GetLoginURL();
            //string apiRequestToken = Response(loginUrl);

            //Console.WriteLine("Goto " + Global.kite.GetLoginURL());
            //Console.WriteLine("Enter request token: ");

            //string requestToken = Console.ReadLine();
            //  if (requestToken != "")
            // {
            //      RequestToken = requestToken;
            // }

            //apiRequestToken = "vG9OYZYLtLCYlfROu1oSCogM46yextWn";
            //User user = ZObjects.kite.GenerateSession(apiRequestToken, MySecret);

            //CurrentUser = user.UserName;
            //Console.WriteLine(Utils.JsonSerialize(user));

            //MyAccessToken =  //ConfigurationManager.AppSettings["appsecret"];

            //MyAccessToken = "XUO8MEj980hRixFV2PWjtyl5jB73I1Ip";//user.AccessToken;
            //MyPublicToken = user.PublicToken;
        }
        private static void OnTokenExpire()
        {
            Logger.LogWrite("Need to login again");
        }

        //public void LoginUser()
        //{
        //    string loginUrl = ZObjects.kite.GetLoginURL();
        //    string apiRequestToken = Response(loginUrl);

        //    ZerodhaLogin(apiRequestToken);

        //    //if (apiRequestToken != "")
        //    //{
        //    //    User user = GlobalObjects.kite.GenerateSession(apiRequestToken, MySecret);
        //    //    apiAccessToken = user.AccessToken;
        //    //    GlobalObjects.kite.SetAccessToken(apiAccessToken);
        //    //    //MyPublicToken = user.PublicToken;
        //    //}
        //}


        //Need to implement Zerodha login
        private static string Response(string loginUrl)
        {
            string requestToken = String.Empty;
            HttpClient client = new HttpClient();
            //client.BaseAddress = new Uri(loginUrl);


            // Usage
            HttpResponseMessage response = client.GetAsync(loginUrl).Result;
            if (response.IsSuccessStatusCode)
            {

                // var dto = response.Content.ReadAsAsync<ImportResultDTO>().Result;

                string[] list = response.RequestMessage.ToString().Split('?');

                string[] parameters = list[1].Split("&".ToCharArray());
                foreach (string str in parameters)
                {
                    string[] values = str.Split("=".ToCharArray());
                    if (values[0].Equals("sess_id"))
                    {
                        requestToken = values[1].Split(',')[0].TrimEnd('\'');
                        // LoginUser(Login.ZerodhaLogin(requestToken));
                    }
                }
            }
            else
            {
                Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
            }
            return requestToken;
        }

        
    }
}
