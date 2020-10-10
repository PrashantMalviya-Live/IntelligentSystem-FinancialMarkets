using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KiteConnect;
using GlobalLayer;
namespace Algorithms.Utilities
{
    class Login
    {
        // Initialize key and secret of your app
        public static string MyAPIKey = "af61rvtidnnnyp8p";
        static string MySecret = "if1ur4umqitbi8kotw95iyuhuinlcj0i";
        // persist these data in settings or db or file
        public static string MyAccessToken = "Li1C0n5KByQ8pl4BHbTn7yvW37h65A4P";
        public static string CurrentUser = "Prashant Malviya";
        public static string RequestToken = "";

        public static bool ZerodhaLogin(string requestToken = "")
        {
            Global.kite = new Kite(MyAPIKey, Debug: true);

            // For handling 403 errors
            Global.kite.SetSessionExpiryHook(OnTokenExpire);

            // Initializes the login flow

            try
            {
                initSession(requestToken);
                Global.kite.SetAccessToken(MyAccessToken);
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
        private static void initSession(string requestToken = "")
        {
            //Console.WriteLine("Goto " + Global.kite.GetLoginURL());
            //Console.WriteLine("Enter request token: ");

            //string requestToken = Console.ReadLine();
          //  if (requestToken != "")
           // {
          //      RequestToken = requestToken;
           // }
           // User user = Global.kite.GenerateSession(RequestToken, MySecret);

            //CurrentUser = user.UserName;
            //Console.WriteLine(Utils.JsonSerialize(user));


            //Global.kite.

            //MyAccessToken = user.AccessToken;
            //MyPublicToken = user.PublicToken;
        }
        private static void OnTokenExpire()
        {
            Logger.LogWrite("Need to login again");
        }
    }
}
