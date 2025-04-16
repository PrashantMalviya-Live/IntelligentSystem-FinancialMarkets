using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KiteConnect;
using KotakConnect;
using GlobalLayer;
using System.Net.Http;
using System.Configuration;
using DataAccess;
using System.Data;
using System.Net.Http.Headers;

namespace BrokerConnectWrapper
{
    /// <summary>
    /// </summary>
    public class DEConnect
    {
        // Initialize key and secret of your app
        // public static string ConsumerAPIkey = "tpayXGqA2my6N8OXRdWYo1ynvoka";
        //static string MySecret = "if1ur4umqitbi8kotw95iyuhuinlcj0i";
        // persist these data in settings or db or file
        //public static string KotakAccessToken = "9a97f879-05d1-3598-af4b-1e94e5f7f7f1";
        public static string UserAPIkey = "af61rvtidnnnyp8p";
        //static string MySecret = "if1ur4umqitbi8kotw95iyuhuinlcj0i";
        // persist these data in settings or db or file
        public static string UserAPISecret = "fzzEEcjFuGC7mJ3tyR4wPiN0nyY845IT";
        public static string CurrentUser = "Prashant Malviya";
        public static string RequestToken = "";

        public static User GetUser(string userId)
        {
            User user = null;
            if (userId != "")
            {
                MarketDAO dao = new MarketDAO();
                DataSet dsUser = dao.GetActiveUser(3, userId);
                user = new User(dsUser.Tables[0]);
            }
            else
            {
                MarketDAO dao = new MarketDAO();
                DataSet dsUser = dao.GetActiveUser(3);
                user = new User(dsUser.Tables[0]);
            }
            return user;
        }
        public static User GetUserByApplicationUserId(string userId, int brokerId)
        {
            MarketDAO dao = new MarketDAO();
            DataSet dsUser = dao.GetUserByApplicationUserId(userId, brokerId);
            return new User(dsUser.Tables[0]);
        }

        public static bool Login(User user = null)
        {
            if (user == null)
            {
                MarketDAO dao = new MarketDAO();
                DataSet dsUser = dao.GetActiveUser(brokerId:3);
                user = new User(dsUser.Tables[0]);
            }
            ZObjects.deltaExchange = new DeltaExchangeConnect.DeltaExchange(user.APIKey, user.AppSecret, Debug: true);

            //// For handling 403 errors
            //ZObjects.kite.SetSessionExpiryHook(OnTokenExpire);

            UserAPISecret = user.AppSecret;
            UserAPIkey = user.APIKey;
            //// Initializes the login flow

            try
            {
                //ZObjects.kite.SetAccessToken(user.AccessToken);
#if Market
                Logger.LogWrite("User logged In");
#endif
                return true;
            }
            catch (Exception e)
            {
#if Market
                // Cannot continue without proper authentication
                Logger.LogWrite(e.Message);
#endif
                // Environment.Exit(0);
            }
            return false;
        }

       
        //public static HttpClient ConfigureHttpClient2(User user, HttpClient httpClient)
        //{
        //    httpClient.BaseAddress = new Uri(user.RootServer);
        //    httpClient.DefaultRequestHeaders.Add("accept", "*/*");
        //    //httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
        //    //httpClient.DefaultRequestHeaders.Add("consumerKey", user.ConsumerKey);
        //    httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + user.AccessToken);
        //    //httpClient.DefaultRequestHeaders.Add("Auth", user.SessionToken);
        //    //httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        //    return httpClient;
        //}

        //public static HttpClient ConfigureHttpClient(User user, HttpClient httpClient)
        //{
        //    httpClient.BaseAddress = new Uri(user.RootServer);
        //    httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + user.AccessToken);
        //    httpClient.DefaultRequestHeaders.Add("Auth", user.SessionToken);
        //    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        //    //httpClient.DefaultRequestHeaders.Add("Content-Type", "application/x-www-form-urlencoded");

        //    httpClient.DefaultRequestHeaders.Add("neo-fin-key", "neotradeapi");
        //    httpClient.DefaultRequestHeaders.Add("Sid", user.SID);

        //    return httpClient;
        //}



        //public static bool zerodhalogin(string _accesstoken = "")
        //{
        //    //if access token is not available retrieve from database
        //    if (_accesstoken == "" && zobjects.kite == null)
        //    {
        //        marketdao dao = new marketdao();
        //        _accesstoken = dao.getactiveuser();
        //    }
        //    zobjects.kite = new kite(myapikey, debug: true);

        //    // for handling 403 errors
        //    zobjects.kite.setsessionexpiryhook(ontokenexpire);

        //    // initializes the login flow

        //    try
        //    {
        //        zobjects.kite.setaccesstoken(_accesstoken);
        //    }
        //    catch (exception e)
        //    {
        //        return false;
        //        // cannot continue without proper authentication
        //        //logger.logwrite(e.message);
        //        // environment.exit(0);
        //    }

        //    return true;
        //}
        //private static void OnTokenExpire()
        //{
        //    Logger.LogWrite("Need to login again");
        //}


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
