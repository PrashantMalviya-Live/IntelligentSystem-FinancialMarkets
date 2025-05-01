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
using DBAccess;

namespace BrokerConnectWrapper
{
    /// <summary>
    /// RequestToken is same as Kotak's access token, and this gets expired 
    /// AccessToken is same as Kotak's session token.
    /// </summary>
    public class KoConnect
    {
        // Initialize key and secret of your app
       // public static string ConsumerAPIkey = "tpayXGqA2my6N8OXRdWYo1ynvoka";
        //static string MySecret = "if1ur4umqitbi8kotw95iyuhuinlcj0i";
        // persist these data in settings or db or file
        //public static string KotakAccessToken = "9a97f879-05d1-3598-af4b-1e94e5f7f7f1";
        public static string CurrentUser = "Prashant Malviya";
        public static string RequestToken = "";

        private readonly IRDSDAO _irdsDAO;

        public KoConnect(IRDSDAO irdsDAO = null)
        {
            _irdsDAO = irdsDAO;
        }

        public User GetUser(string userId)
        {
            User user = null;
                if (userId != "")
                {
                    DataSet dsUser = _irdsDAO.GetActiveUser(1, userId);
                    user = new User(dsUser.Tables[0]);
                }
                else
                {
                    DataSet dsUser = _irdsDAO.GetActiveUser(1);
                    user = new User(dsUser.Tables[0]);
                }
            return user;
        }
        public User GetUserByApplicationUserId(string userId, int brokerId)
        {
            DataSet dsUser = _irdsDAO.GetUserByApplicationUserId(userId, brokerId);
            return new User(dsUser.Tables[0]);
        }

        public bool Login(User user = null, string userId = "")
        {
            if (user == null)
            {
                if (userId != "")
                {
                    DataSet dsUser = _irdsDAO.GetActiveUser(1, userId);
                    user = new User(dsUser.Tables[0]);
                }
                else
                {
                    DataSet dsUser = _irdsDAO.GetActiveUser(1);
                    user = new User(dsUser.Tables[0]);
                }
            }

            ZObjects.kotak ??= new Kotak();
            //if (ZObjects.kotak == null)
            //{
            //    ZObjects.kotak = new Kotak(user.ConsumerKey, user.AccessToken);
            //}
            //ZObjects.kotak = new KotakConnect.Kotak(user.APIKey, KotakAccessToken);
            //SessionAccessToken = ZObjects.kotak.Sess;
            //UserAPIkey = user.APIKey;
            //// Initializes the login flow

            try
            {
                ZObjects.kotak.SetSessionToken(user.SessionToken, user.RootServer, 
                    user.ConsumerKey, user.AccessToken);
                Logger.LogWrite("User logged In");
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

        public static HttpClient ConfigureHttpClient2(User user, HttpClient httpClient)
        {
            httpClient.BaseAddress = new Uri(user.RootServer);
            httpClient.DefaultRequestHeaders.Add("accept", "*/*");
            //httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
            //httpClient.DefaultRequestHeaders.Add("consumerKey", user.ConsumerKey);
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + user.AccessToken);
            //httpClient.DefaultRequestHeaders.Add("Auth", user.SessionToken);
            //httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return httpClient;
        }

        public static HttpClient ConfigureHttpClient(User user, HttpClient httpClient)
        {
            httpClient.BaseAddress = new Uri(user.RootServer);
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + user.AccessToken);
            httpClient.DefaultRequestHeaders.Add("Auth", user.SessionToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            //httpClient.DefaultRequestHeaders.Add("Content-Type", "application/x-www-form-urlencoded");

            httpClient.DefaultRequestHeaders.Add("neo-fin-key", "neotradeapi");
            httpClient.DefaultRequestHeaders.Add("Sid",  user.SID );

            return httpClient;
        }
       


        //public static bool zerodhalogin(string _accesstoken = "")
        //{
        //    //if access token is not available retrieve from database
        //    if (_accesstoken == "" && zobjects.kite == null)
        //    {
        //        marketdao _irdsDAO = new marketdao();
        //        _accesstoken = _irdsDAO.getactiveuser();
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
        private static void OnTokenExpire()
        {
            Logger.LogWrite("Need to login again");
        }


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
