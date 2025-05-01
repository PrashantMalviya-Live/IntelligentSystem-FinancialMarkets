using GlobalLayer;
using KiteConnect;
using System;
using System.Data;
using System.Net.Http;
using DBAccess;

namespace BrokerConnectWrapper
{
    public class ZConnect
    {
        private readonly IRDSDAO _idAO;
        public ZConnect(IRDSDAO idAO = null)
        {
            _idAO = idAO;
        }

        // Initialize key and secret of your app
        public static string UserAPIkey = "af61rv11118p";
        //static string MySecret = "if1ur4umqitbi8kotw95iyuhuinlcj0i";
        // persist these data in settings or db or file
        public static string UserAccessToken = "fzzEEcjFuGC7mJ3tynyY845IT";
        public static string CurrentUser = "Prashant Malviya";
        public static string RequestToken = "";

        public bool Login(User user = null)
        {
            if(user == null)
            {
#if AWSMARKET
               
                DataSet dsUser = _idAO.GetActiveUser();

#else
                SQlDAO dao = new SQlDAO();
                DataSet dsUser = dao.GetActiveUser();
#endif


                user = new User(dsUser.Tables[0]);
            }
            ZObjects.kite = new Kite(user.APIKey, Debug: true);

            // For handling 403 errors
            ZObjects.kite.SetSessionExpiryHook(OnTokenExpire);

            UserAccessToken = user.AccessToken;
            UserAPIkey = user.APIKey;
            // Initializes the login flow

            try
            {
                ZObjects.kite.SetAccessToken(user.AccessToken);
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
