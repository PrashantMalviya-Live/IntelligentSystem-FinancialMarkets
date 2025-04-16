using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KiteConnect;
using GlobalLayer;
using System.Net.Http;
using System.Configuration;
using DataAccess;
using System.Data;
using FyersConnect;

namespace BrokerConnectWrapper
{
    public class FyConnect
    {
        // Initialize key and secret of your app
        public static string UserAPIkey;
        // persist these data in settings or db or file
        public static string UserAccessToken;
        public static string CurrentUser;
        public static string RequestToken;

        public static bool Login(User user = null)
        {
            if (user == null)
            {
                MarketDAO dao = new MarketDAO();
                DataSet dsUser = dao.GetActiveUser(2);
                user = new User(dsUser.Tables[0]);
            }
            ZObjects.fy = new Fy(user.APIKey);
            UserAccessToken = user.AccessToken;
            UserAPIkey = user.APIKey;
            // Initializes the login flow

            try
            {
                ZObjects.fy.SetAccessToken(user.AccessToken);
#if Market
                Logger.LogWrite("User logged In");
#endif
                return true;
            }
            catch (Exception e)
            {
                // Cannot continue without proper authentication
                Logger.LogWrite(e.Message);
                // Environment.Exit(0);
            }
            return false;
        }
        
        private static void OnTokenExpire()
        {
            Logger.LogWrite("Need to login again");
        }


        ////Need to implement Zerodha login
        //private static string Response(string loginUrl)
        //{
        //    string requestToken = String.Empty;
        //    HttpClient client = new HttpClient();
        //    //client.BaseAddress = new Uri(loginUrl);


        //    // Usage
        //    HttpResponseMessage response = client.GetAsync(loginUrl).Result;
        //    if (response.IsSuccessStatusCode)
        //    {

        //        // var dto = response.Content.ReadAsAsync<ImportResultDTO>().Result;

        //        string[] list = response.RequestMessage.ToString().Split('?');

        //        string[] parameters = list[1].Split("&".ToCharArray());
        //        foreach (string str in parameters)
        //        {
        //            string[] values = str.Split("=".ToCharArray());
        //            if (values[0].Equals("sess_id"))
        //            {
        //                requestToken = values[1].Split(',')[0].TrimEnd('\'');
        //                // LoginUser(Login.ZerodhaLogin(requestToken));
        //            }
        //        }
        //    }
        //    else
        //    {
        //        Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
        //    }
        //    return requestToken;
        //}


    }
}
