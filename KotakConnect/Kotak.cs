using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Collections;
using GlobalLayer;
using System.Net.Http;
using System.Web;
using System.Threading;
using System.Net.Http.Headers;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;
using System.Text;
using System.Threading.Tasks;

namespace KotakConnect
{
    /// <summary>
    /// The API client class. In production, you may initialize a single instance of this class per `APIKey`.
    /// </summary>
    public class Kotak
    {
        // Default root API endpoint. It's possible to
        // override this by passing the `Root` parameter during initialisation.
        private string _root = "https://ctradeapi.kotaksecurities.com/apim"; //"https://tradeapi.kotaksecurities.com/apim";
        private string _login = "https://sbx.kotaksecurities.com/apim/session/1.0/session/login/userid";
        //private string _root = "https://tradeapi.kotaksecurities.com/apim/session/1.0/session/login/userid";

        private string _apiKey;
        private string _consumerKey = "tpayXGqA2my6N8OXRdWYo1ynvoka"; //"48E9HgzAnwblbf0i5FBOPmstr6ca"; // sandbox: "UqtkUfmbeLi3UPdnMS6rvfKlhB4a"; // production: "tpayXGqA2my6N8OXRdWYo1ynvoka";
        private string _accessToken = "dead6a06-113e-3ef6-bb46-897401304140";// Production: "bf4e6cd5-a29b-339c-b6be-bca6626bbcf5";
        private string _secret = "RxPgDzSOUEWVQeXxjlqN1SHq4bIa";//"HLMr5U4TPvrEVRWK1zHfRDeDj68a";
        private string _sessionToken;
        private string _appId = "DefaultApplication";
        private string _ipAddress = "127.0.0.1";
        private string _ott;
        private bool _enableLogging;
        private WebProxy _proxy;
        private int _timeout;
        private Action _sessionHook;
        private static readonly HttpClient client = new HttpClient();
        //private Cache cache = new Cache();

        private SemaphoreSlim semaphore;

        public string KotakAccessToken
        {
            get { return _accessToken; }
        }
        public string UserSessionToken
        {
            get { return _sessionToken; }
        }
        public string ConsumerKey
        {
            get { return _consumerKey; }
        }
        private readonly Dictionary<string, string> _routes = new Dictionary<string, string>
        {
            ["parameters"] = "/parameters",
            ["api.token"] = "/session/token",
            ["api.refresh"] = "/session/refresh_token",

            //["login.ott"] = "/session/1.0/session/login/userid",
            ["login.ott"] = "/session/1.0/session/userid",
            ["login.sessionToken"] = "/session/1.0/session/2FA/accesscode",

            ["instrument.margins"] = "/margins/{segment}",

            ["user.profile"] = "/user/profile",
            ["user.margins"] = "/user/margins",
            ["user.segment_margins"] = "/user/margins/{segment}",

            ["orders"] = "/orders/{order_id}",
            ["trades"] = "/trades",
            ["orders.history"] = "/orders/{order_id}",

            //["session.ott"] = "/session/1.0/session/login/userid",
            ["session.itoken"] = "/login/1.0/login/v2/validate",
            ["session.otp"] = "/login/1.0/login/otp/generate",
            
            ["session.token"] = "/login/1.0/login/v2/validate",

            //["orders.place"] = "/orders/1.0/order/{product}",
            //["orders.modify"] = "/orders/1.0/order/{product}",
            //["orders.cancel"] = "/orders/1.0/order/{product}/{orderId}",
            //["reports.orderhistory"] = "/reports/1.0/orders",

            //https://gw-napi.kotaksecurities.com/Orders/2.0/quick/order/rule/ms/place?sId=server3

            ["orders.place"] = "/Orders/2.0/quick/order/rule/ms/place?sId={server}",

            ///https://gw-napi.kotaksecurities.com/Orders/2.0/quick/order/vr/modify?sId=server1
            ["orders.modify"] = "/Orders/2.0/quick/order/vr/modify?sId={server}",

            //https://gw-napi.kotaksecurities.com/Orders/2.0/quick/order/cancel?sId=serverId

            ["orders.cancel"] = "/Orders/2.0/quick/order/cancel?sId={server}",


            //https://gw-napi.kotaksecurities.com/Orders/2.0/quick/order/history?sId=server1
            ["reports.orderhistory"] = "/Orders/2.0/quick/order/history?sId={server}",

            ///Parameters to be passed to order Book :

            // Authorization - Access Token(Format to pass access token is (Bearer { Access Token }))
            //Sid - session Id(Obtained in response body of Login API)
            //Auth - Session Token(Obtained in response body of Login API)
            //Neo - fin - key - neotradeapi(pass this value as default value)
            //sId - hsServerId(Obtained in response body of Login API)
            //"/reports/1.0/orders/{orderId}",

            ["scripmaster.download"] = "/scripmaster/1.1/download",

            //GETOrder Book
            //https://gw-napi.kotaksecurities.com/Orders/2.0/quick/user/orders?sId=server1
            //["reports.orderhistory"] = "/reports/1.0/orders",

            //["orders.trades"] = "/orders/{order_id}/trades",

            //["gtt"] = "/gtt/triggers",
            //["gtt.place"] = "/gtt/triggers",
            //["gtt.info"] = "/gtt/triggers/{id}",
            //["gtt.modify"] = "/gtt/triggers/{id}",
            //["gtt.delete"] = "/gtt/triggers/{id}",

            ["portfolio.positions"] = "/Orders/2.0/quick/user/positions?sId={server}",
            //["portfolio.holdings"] = "/portfolio/holdings",
            //["portfolio.positions.modify"] = "/portfolio/positions",

            //["market.instruments.all"] = "/instruments",
            //["market.instruments"] = "/instruments/{exchange}",
            //["market.quote"] = "/quote",
            //["market.ohlc"] = "/quote/ohlc",
            //["market.ltp"] = "/quote/ltp",
            //["market.historical"] = "/instruments/historical/{instrument_token}/{interval}",
            //["market.trigger_range"] = "/instruments/trigger_range/{transaction_type}",

            //["mutualfunds.orders"] = "/mf/orders",
            //["mutualfunds.order"] = "/mf/orders/{order_id}",
            //["mutualfunds.orders.place"] = "/mf/orders",
            //["mutualfunds.cancel_order"] = "/mf/orders/{order_id}",

            //["mutualfunds.sips"] = "/mf/sips",
            //["mutualfunds.sips.place"] = "/mf/sips",
            //["mutualfunds.cancel_sips"] = "/mf/sips/{sip_id}",
            //["mutualfunds.sips.modify"] = "/mf/sips/{sip_id}",
            //["mutualfunds.sip"] = "/mf/sips/{sip_id}",

            //["mutualfunds.instruments"] = "/mf/instruments",
            //["mutualfunds.holdings"] = "/mf/holdings"
        };

        /// <summary>
        /// Initialize a new Kite Connect client instance.
        /// </summary>
        /// <param name="APIKey">API Key issued to you</param>
        /// <param name="AccessToken">The token obtained after the login flow in exchange for the `RequestToken` . 
        /// Pre-login, this will default to None,but once you have obtained it, you should persist it in a database or session to pass 
        /// to the Kite Connect class initialisation for subsequent requests.</param>
        /// <param name="Root">API end point root. Unless you explicitly want to send API requests to a non-default endpoint, this can be ignored.</param>
        /// <param name="Debug">If set to True, will serialise and print requests and responses to stdout.</param>
        /// <param name="Timeout">Time in milliseconds for which  the API client will wait for a request to complete before it fails</param>
        /// <param name="Proxy">To set proxy for http request. Should be an object of WebProxy.</param>
        /// <param name="Pool">Number of connections to server. Client will reuse the connections if they are alive.</param>
        //public Kotak(string APIKey, string AccessToken, string RootServer = null, string SessonToken = null, string Root = null, 
        public Kotak(string Root = null,
            bool Debug = false, int Timeout = 7000, WebProxy Proxy = null, int Pool = 2)
        {
            //_accessToken = AccessToken;
            //_apiKey = APIKey;
            //_root = RootServer;
            //if(SessonToken != null)
            //{
            //    _sessionToken = SessonToken;
            //}
            if (!String.IsNullOrEmpty(Root)) this._root = Root;
            _enableLogging = Debug;

            _timeout = Timeout;
            _proxy = Proxy;

            //ServicePointManager.DefaultConnectionLimit = Pool;
            ServicePointManager.DefaultConnectionLimit = 20;
            ServicePointManager.Expect100Continue = false;

            semaphore = new SemaphoreSlim(10);
        }
        public void SetSessionToken(string sessionToken, string server, string consumerKey, string accessToken)
        {
            _sessionToken = sessionToken;
            _root = server;
            _accessToken = accessToken;
            _consumerKey = consumerKey;
        }

        public void GenerateInitialToken(string mobilenumber, string password, HttpClient httpClient, out string sid, out string vToken)
        {
            //mobile id
            mobilenumber = "+919650084883";
            vToken = GenerateVToken(mobilenumber, password, out sid, httpClient);

            //In Parameters you need to pass USER ID
            //Steps to get User ID:
            //Select the token you have received in response on 1st Step: to generate view token
            //Go to any only JWT token decoder and put the view token
            //In decoded Payload you will find a variable : "sub"
            //This sub variable will contain your user id
            //Copy this user Id and put in parameters

            string userid = vToken;
            userid = "264298b2-ab60-4780-a218-85f842879189";
            GenerateOTP(userid, httpClient);

            ////OTP comes to your email
            //string otp = "5224";// "put otp here";

            ////          "sendEmail": true,
            ////"isWhitelisted": true

            //httpClient = ConfigureHttpClient2(sid, vToken, httpClient);
            //_sessionToken = GenerateSession(userid, otp, out hsServerId, httpClient:httpClient);
            //return _sessionToken;
        }
        public string GenerateSessionToken(string sid, string vToken, string otp, HttpClient httpClient, out string hsServerId)
        {
            //OTP comes to your email
            //string otp = "5224";// "put otp here";
            string userid = "264298b2-ab60-4780-a218-85f842879189";
            //          "sendEmail": true,
            //"isWhitelisted": true

            httpClient = ConfigureHttpClient2(sid, vToken, httpClient);
            _sessionToken = GenerateSession(userid, otp, out hsServerId, httpClient: httpClient);
            return _sessionToken;
        }

        public static HttpClient ConfigureHttpClient2(string sid, string vToken, HttpClient httpClient)
        {
            httpClient.DefaultRequestHeaders.Add("sid", sid);
            httpClient.DefaultRequestHeaders.Add("Auth", vToken);

            return httpClient;
        }

        /// <summary>
        /// Enabling logging prints HTTP request and response summaries to console
        /// </summary>
        /// <param name="enableLogging">Set to true to enable logging</param>
        public void EnableLogging(bool enableLogging)
        {
            _enableLogging = enableLogging;
        }

        /// <summary>
        /// Set a callback hook for session (`TokenException` -- timeout, expiry etc.) errors.
		/// An `AccessToken` (login session) can become invalid for a number of
        /// reasons, but it doesn't make sense for the client to
		/// try and catch it during every API call.
        /// A callback method that handles session errors
        /// can be set here and when the client encounters
        /// a token error at any point, it'll be called.
        /// This callback, for instance, can log the user out of the UI,
		/// clear session cookies, or initiate a fresh login.
        /// </summary>
        /// <param name="Method">Action to be invoked when session becomes invalid.</param>
        public void SetSessionExpiryHook(Action Method)
        {
            _sessionHook = Method;
        }

        /// <summary>
        /// Set the `AccessToken` received after a successful authentication.
        /// </summary>
        /// <param name="AccessToken">Access token for the session.</param>
        public void SetAccessToken(string AccessToken)
        {
            this._accessToken = AccessToken;
        }

        /// <summary>
        /// Get the remote login url to which a user should be redirected to initiate the login flow.
        /// </summary>
        /// <returns>Login url to authenticate the user.</returns>
        public string GetLoginURL()
        {
            return String.Format("{0}?api_key={1}&v=3", _login, _apiKey);
        }

        /// <summary>
        /// The first step to logging in is to generate the One-Time Token, which will later be required 
        /// for the 2-factor authentication function. For this you must input your user ID and password 
        /// associated with your Kotak securities account as data, along with your access token, consumer key,
        /// IP address, and app ID as header inputs (latter two being optional inputs for clients):
        /// </summary>
        /// <param name="RequestToken">Token obtained from the GET paramers after a successful login redirect.</param>
        /// <param name="AppSecret">API secret issued with the API key.</param>
        /// <returns>User structure with tokens and profile data</returns>
        public string GenerateVToken(string mobilenumber, string password, out string sessionid, HttpClient httpClient = null)
        {
            
            var param = new Dictionary<string, dynamic>
            {
                {"mobilenumber", mobilenumber},
                {"password", password}
            };


            var response = Post("session.itoken", param, httpClient);

            sessionid = response["data"]["sid"];

            //accessCodeTask.Wait();
            return response["data"]["token"]; // new User(userData);

            //var accessCodeTask = Post("session.ott", param, httpClient);
            //accessCodeTask.Wait();
            //return accessCodeTask.Result["Success"]["oneTimeToken"]; // new User(userData);
        }
        /// <summary>
        /// The first step to logging in is to generate the One-Time Token, which will later be required 
        /// for the 2-factor authentication function. For this you must input your user ID and password 
        /// associated with your Kotak securities account as data, along with your access token, consumer key,
        /// IP address, and app ID as header inputs (latter two being optional inputs for clients):
        /// </summary>
        /// <param name="RequestToken">Token obtained from the GET paramers after a successful login redirect.</param>
        /// <param name="AppSecret">API secret issued with the API key.</param>
        /// <returns>User structure with tokens and profile data</returns>
        public void GenerateOTP(string userid, HttpClient httpClient = null)
        {

            var param = new Dictionary<string, dynamic>
            {
                {"userId", userid},
                //{"userid", userId},
                {"sendEmail", true},
                {"isWhitelisted", true}
            };

            var accessCode = Post("session.otp", param, httpClient);



            //accessCodeTask.Wait();
            //return accessCode["Success"]["data"]["token"]; // new User(userData);

            //var accessCodeTask = Post("session.ott", param, httpClient);
            //accessCodeTask.Wait();
            //return accessCodeTask.Result["Success"]["oneTimeToken"]; // new User(userData);
        }

        /// <summary>
        /// Do the token exchange with the `RequestToken` obtained after the login flow,
		/// and retrieve the `AccessToken` required for all subsequent requests.The
        /// response contains not just the `AccessToken`, but metadata for
        /// the user who has authenticated.
        /// </summary>
        /// <param name="RequestToken">Token obtained from the GET paramers after a successful login redirect.</param>
        /// <param name="AppSecret">API secret issued with the API key.</param>
        /// <returns>User structure with tokens and profile data</returns>
        public string GenerateSession(string userId, string otp, out string hsServerId, HttpClient httpClient = null)
        {
            var param = new Dictionary<string, dynamic>
            {
                {"userid", userId},
                {"otp", otp}
            };
           // _ott = accessCode;

            var userData = Post("session.token", param, httpClient);

            hsServerId = userData["data"]["hsServerId"];

            return userData["data"]["token"];

            //var userDataTask = Post("session.token", param, httpClient);
            //userDataTask.Wait();
            //return userDataTask.Result["success"]["sessionToken"];
        }

        /// <summary>
        /// Kill the session by invalidating the access token
        /// </summary>
        /// <param name="AccessToken">Access token to invalidate. Default is the active access token.</param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> InvalidateAccessToken(string AccessToken = null)
        {
            var param = new Dictionary<string, dynamic>();

            Utils.AddIfNotNull(param, "api_key", _apiKey);
            Utils.AddIfNotNull(param, "access_token", AccessToken);

            return Delete("api.token", param);
        }

        /// <summary>
        /// Invalidates RefreshToken
        /// </summary>
        /// <param name="RefreshToken">RefreshToken to invalidate</param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> InvalidateRefreshToken(string RefreshToken)
        {
            var param = new Dictionary<string, dynamic>();

            Utils.AddIfNotNull(param, "api_key", _apiKey);
            Utils.AddIfNotNull(param, "refresh_token", RefreshToken);

            return Delete("api.token", param);
        }

        /// <summary>
        /// Renew AccessToken using RefreshToken
        /// </summary>
        /// <param name="RefreshToken">RefreshToken to renew the AccessToken.</param>
        /// <param name="AppSecret">API secret issued with the API key.</param>
        /// <returns>TokenRenewResponse that contains new AccessToken and RefreshToken.</returns>
        //public TokenSet RenewAccessToken(string RefreshToken, string AppSecret)
        //{
        //    var param = new Dictionary<string, dynamic>();

        //    string checksum = Utils.SHA256(_apiKey + RefreshToken + AppSecret);

        //    Utils.AddIfNotNull(param, "api_key", _apiKey);
        //    Utils.AddIfNotNull(param, "refresh_token", RefreshToken);
        //    Utils.AddIfNotNull(param, "checksum", checksum);

        //    return new TokenSet(Post("api.refresh", param));
        //}

        /// <summary>
        /// Gets currently logged in user details
        /// </summary>
        /// <returns>User profile</returns>
        public Profile GetProfile()
        {
            var profileData = Get("user.profile");

            return new Profile(profileData);
        }

        ///// <summary>
        ///// Margin data for intraday trading
        ///// </summary>
        ///// <param name="Segment">Tradingsymbols under this segment will be returned</param>
        ///// <returns>List of margins of intruments</returns>
        //public List<InstrumentMargin> GetInstrumentsMargins(string Segment)
        //{
        //    var instrumentsMarginsData = Get("instrument.margins", new Dictionary<string, dynamic> { { "segment", Segment } });

        //    List<InstrumentMargin> instrumentsMargins = new List<InstrumentMargin>();
        //    foreach (Dictionary<string, dynamic> item in instrumentsMarginsData["data"])
        //        instrumentsMargins.Add(new InstrumentMargin(item));

        //    return instrumentsMargins;
        //}

        /// <summary>
        /// Get account balance and cash margin details for all segments.
        /// </summary>
        /// <returns>User margin response with both equity and commodity margins.</returns>
        public UserMarginsResponse GetMargins()
        {
            var marginsData = Get("user.margins");
            return new UserMarginsResponse(marginsData["data"]);
        }

        /// <summary>
        /// Get account balance and cash margin details for a particular segment.
        /// </summary>
        /// <param name="Segment">Trading segment (eg: equity or commodity)</param>
        /// <returns>Margins for specified segment.</returns>
        public UserMargin GetMargins(string Segment)
        {
            var userMarginData = Get("user.segment_margins", new Dictionary<string, dynamic> { { "segment", Segment } });
            return new UserMargin(userMarginData["data"]);
        }

        /// <summary>
        /// Place an order
        /// </summary>
        /// <param name="Exchange">Name of the exchange</param>
        /// <param name="TradingSymbol">Tradingsymbol of the instrument</param>
        /// <param name="TransactionType">BUY or SELL</param>
        /// <param name="Quantity">Quantity to transact</param>
        /// <param name="Price">For LIMIT orders</param>
        /// <param name="Product">Margin product applied to the order (margin is blocked based on this)</param>
        /// <param name="OrderType">Order type (MARKET, LIMIT etc.)</param>
        /// <param name="Validity">Order validity</param>
        /// <param name="DisclosedQuantity">Quantity to disclose publicly (for equity trades)</param>
        /// <param name="TriggerPrice">For SL, SL-M etc.</param>
        /// <param name="SquareOffValue">Price difference at which the order should be squared off and profit booked (eg: Order price is 100. Profit target is 102. So squareoff = 2)</param>
        /// <param name="StoplossValue">Stoploss difference at which the order should be squared off (eg: Order price is 100. Stoploss target is 98. So stoploss = 2)</param>
        /// <param name="TrailingStoploss">Incremental value by which stoploss price changes when market moves in your favor by the same incremental value from the entry price (optional)</param>
        /// <param name="Variety">You can place orders of varieties; regular orders, after market orders, cover orders etc. </param>
        /// <param name="Tag">An optional tag to apply to an order to identify it (alphanumeric, max 8 chars)</param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> PlaceOrder(
            //string Exchange,

            uint InstrumentToken,
            string TransactionType,
            int Quantity,
            decimal? Price = null,
            string Product = null,
            string OrderType = null,
            string Validity = null,
            int? DisclosedQuantity = null,
            decimal? TriggerPrice = null,
            //decimal? SquareOffValue = null,
            //decimal? StoplossValue = null,
            //decimal? TrailingStoploss = null,
            string Variety = Constants.VARIETY_REGULAR,
            string Tag = "",
            string TradingSymbol = "",
            string HsServerId = "",
            HttpClient httpClient = null,
            User user = null)
        {
            var param = new Dictionary<string, dynamic>();
            param.Add("server", HsServerId);

            //jdata = { "am":"NO", "dq":"0","es":"nse_cm", "mp":"0", "pc":"CNC", "pf":"N", "pr":"0", "pt":"MKT", "qt":"1", "rt":"DAY", "tp":"0", "ts":"TCS-EQ", "tt":"B"}

            //            The jData contains parameters used for placing an order.
            //            am->After Market Order(Allowed Values->YES / NO)
            //dq->Disclosed quantity(Quantity to disclose publicly(for equity trades)) Ex: 5, 10 etc
            //mp->Market protection. 0 by default(Ex: 5.00, 8.00 etc)
            //pf->PosSqrFlg(Allowed Values->Y / N, No Need to change this keep this as N)
            //pr->The price to execute the order Ex:2000.00, 250.50, etc
            //tp->Trigger price(The price at which an order should be triggered for SL, SL - M[0 for other orders] Ex:2100.00, 300.90 etc)
            // es->exchange segment - Eg:nse_cm->NSE, bse_cm->BSE, nse_fo->NFO, bse_fo->BFO, cde_fo->CDS, bcs_fo->BCD, mcx_fo->MCX
            //pc->Product code(NRML->Normal, CNC->Cash and Carry, MIS->Margin Intraday Squareoff for futures and options, INTRADAY->INTRADAY, CO->Cover Order, BO->Bracket Order, PRIME->Custom product)
            //                qt->Quantity to transact(Ex:10, 20, etc)
            //rt->Order Duration(Allowed values - either DAY->Regular order or IOC->Immediate or Cancel)
            //tt->Transaction Type(Buy->B or Sell->S)
            //pt->Order Type(L->Limit, MKT->Market, SL->Stop loss limit, SL - M->Stop loss market)
            //ts->Exchange trading symbol of the instrument Ex:TCS - EQ, ACC - EQ, etc.



            //request.AddParameter("jData", "{\"am\":\"NO\", \"dq\":\"0\",\"es\":\"nse_cm\", \"mp\":\"0\", \"pc\":\"CNC\", \"pf\":\"N\", \"pr\":\"3000\", \"pt\":\"MKT\", \"qt\":\"1\", \"rt\":\"DAY\", \"tp\":\"0\", \"ts\":\"TCS-EQ\", \"tt\":\"B\"}");


            //sb.Append("{");
            //request.AddParameter("jData", "{\"am\":\"NO\", \"dq\":\"0\",\"es\":\"nse_cm\", \"mp\":\"0\", \"pc\":\"CNC\", \"pf\":\"N\", \"pr\":\"3000\", \"pt\":\"MKT\", \"qt\":\"1\", \"rt\":\"DAY\", \"tp\":\"0\", \"ts\":\"TCS-EQ\", \"tt\":\"B\"}");
            //param.Add("jData", "{\"am\":\"NO\", \"dq\":\"0\",\"es\":\"nse_cm\", \"mp\":\"0\", \"pc\":\"CNC\", \"pf\":\"N\", \"pr\":\"3000\", \"pt\":\"MKT\", \"qt\":\"1\", \"rt\":\"DAY\", \"tp\":\"0\", \"ts\":\"TCS-EQ\", \"tt\":\"B\"}");
            //param.Add("jData", sb.AppendFormat("{\"am\":\"NO\", \"dq\":\"{0}\",\"es\":\"nse_fo\", \"mp\":\"0\", \"pc\":\"CNC\", \"pf\":\"N\", \"pr\":\"{1}\", \"pt\":\"{2}\", \"qt\":\"{3}\", \"rt\":\"DAY\", \"tp\":\"{4}\", \"ts\":\"{5}\", \"tt\":\"{6}\"}", 
            //    Quantity.ToString(), (((OrderType == Constants.ORDER_TYPE_MARKET)) ? 0 : Price).ToString(), 
            //    OrderType == Constants.ORDER_TYPE_MARKET ? "MKT" : OrderType == Constants.ORDER_TYPE_LIMIT ? "L" : OrderType == Constants.ORDER_TYPE_SL ? "SL" : " SLM-M", Quantity.ToString(),
            //    TriggerPrice.ToString(), TradingSymbol, TransactionType.ToLower() == "buy" ? "B" : "S").ToString());

            StringBuilder sb = new StringBuilder();
            string tt = TransactionType.ToLower() == "buy" ? "B" : "S";
            string pt = OrderType == Constants.ORDER_TYPE_MARKET ? "MKT" : OrderType == Constants.ORDER_TYPE_LIMIT ? "L" : OrderType == Constants.ORDER_TYPE_SL ? "SL" : " SLM-M";

            sb.Append("{");
            sb.AppendFormat("\"am\":\"NO\", \"dq\":\"0\",\"es\":\"nse_fo\", \"mp\":\"0\", \"pc\":\"NRML\", \"pf\":\"N\", \"pr\":\"{0}\", \"pt\":\"{5}\", \"qt\":\"{1}\", \"rt\":\"DAY\", \"tp\":\"{2}\", \"ts\":\"{3}\", \"tt\":\"{4}\"",
                ((OrderType == Constants.ORDER_TYPE_MARKET) ? 0 : Price), Quantity, TriggerPrice, TradingSymbol, tt, pt).ToString();
            sb.Append("}");
            param.Add("jData", sb.ToString());

            //param.Add("jData", String.Format("{""am\":\"NO\", \"dq\":\"{0}\",\"es\":\"nse_fo\", \"mp\":\"0\", \"pc\":\"CNC\", \"pf\":\"N\", \"pr\":\"{1}\", \"pt\":\"{2}\", \"qt\":\"{3}\", " +
            //    "\"rt\":\"DAY\", \"tp\":\"{4}\", \"ts\":\"{5}\", \"tt\":\"{6}\"}",
            //    Quantity.ToString(), (((OrderType == Constants.ORDER_TYPE_MARKET)) ? 0 : Price).ToString(),
            //    OrderType == Constants.ORDER_TYPE_MARKET ? "MKT" : OrderType == Constants.ORDER_TYPE_LIMIT ? "L" : OrderType == Constants.ORDER_TYPE_SL ? "SL" : " SLM-M", Quantity.ToString(),
            //    TriggerPrice.ToString(), TradingSymbol, TransactionType.ToLower() == "buy" ? "B" : "S"));



            //Quantity.ToString(), (((OrderType == Constants.ORDER_TYPE_MARKET)) ? 0 : Price).ToString(),
            //OrderType == Constants.ORDER_TYPE_MARKET ? "MKT" : OrderType == Constants.ORDER_TYPE_LIMIT ? "L" : OrderType == Constants.ORDER_TYPE_SL ? "SL" : " SLM-M", Quantity.ToString(),
            //TriggerPrice.ToString(), TradingSymbol, TransactionType.ToLower() == "buy" ? "B" : "S");

            //param.Add("am", "NO");
            //param.Add("dq", Quantity.ToString());
            //param.Add("es", "nse_fo");
            //param.Add("pf", "N");
            //param.Add("mp", "0");
            //param.Add("pr", (((OrderType == Constants.ORDER_TYPE_MARKET)) ? 0 : Price).ToString());
            //param.Add("tp", TriggerPrice.ToString());
            ////NRML->Normal, CNC->Cash and Carry, MIS->Margin Intraday Squareoff for futures and options, INTRADAY->INTRADAY, CO->Cover Order, BO->Bracket Order, PRIME->Custom product
            //param.Add("pc", "NRML");// Product.ToLower());
            //param.Add("qt", Quantity.ToString());
            //param.Add("tt", TransactionType.ToLower() == "buy" ? "B" : "S");
            //param.Add("pt", OrderType == Constants.ORDER_TYPE_MARKET ? "MKT" : OrderType == Constants.ORDER_TYPE_LIMIT ? "L" : OrderType == Constants.ORDER_TYPE_SL ? "SL" : " SLM-M");
            //param.Add("ts", TradingSymbol);
            //param.Add("rt", "DAY");



            param.Add("HttpRequestHeaders-Sid", user.SID);
            param.Add("HttpRequestHeaders-Auth", user.SessionToken);
            param.Add("HttpRequestHeaders-Neo-fin-key", "neotradeapi");
            //param.Add("HttpRequestHeaders-Content-Type", "application/x-www-form-urlencoded");
            //param.Add("instrumentToken", InstrumentToken);
            //param.Add("transactionType", TransactionType);
            //param.Add("quantity", Quantity);
            ////param.Add("price", ((OrderType == Constants.ORDER_TYPE_MARKET) || (OrderType == Constants.ORDER_TYPE_SLM)) ? 0 : Price);
            //param.Add("price", ((OrderType == Constants.ORDER_TYPE_MARKET)) ? 0 : Price);
            //param.Add("product", Product.ToLower());
            //param.Add("validity", Validity);
            //param.Add("disclosedQuantity", DisclosedQuantity);
            //param.Add("triggerPrice", TriggerPrice);
            //param.Add("variety", Variety);
            //param.Add("tag", Tag);

            //Utils.AddIfNotNull(param, "instrumentToken", InstrumentToken);
            //Utils.AddIfNotNull(param, "transactionType", TransactionType);
            //Utils.AddIfNotNull(param, "quantity", Quantity);
            //Utils.AddIfNotNull(param, "price", OrderType == Constants.ORDER_TYPE_MARKET ? 0 : Price);
            //Utils.AddIfNotNull(param, "product", Product.ToLower());
            //Utils.AddIfNotNull(param, "validity", Validity);
            //Utils.AddIfNotNull(param, "disclosedQuantity", DisclosedQuantity);
            //Utils.AddIfNotNull(param, "triggerPrice", TriggerPrice);
            //Utils.AddIfNotNull(param, "variety", Variety);
            //Utils.AddIfNotNull(param, "tag", Tag);

            return Post("orders.place", param, httpClient);

            //return ExecuteOrder("orders.place", sb.ToString(), "application/x-www-form-urlencoded", RestSharp.Method.Post, httpClient: httpClient, user: user, param);
        }

        //private dynamic ExecuteOrder(string Route, string jData, string contentType, HttpMethod httpMethod, 
        //    HttpClient httpClient, User user, Dictionary<string, dynamic> Params)
        //{
        //    int counter = 0;
        //    Dictionary<string, dynamic> responseDictionary = null;
        //    while (true)
        //    {
                
        //        string url = httpClient.BaseAddress + _routes[Route];
        //        //url.Replace("{server}", serverId);

        //        if (url.Contains("{"))
        //        {
        //            var urlparams = Params.ToDictionary(entry => entry.Key, entry => entry.Value);
        //            foreach (KeyValuePair<string, dynamic> item in urlparams)
        //                if (url.Contains("{" + item.Key + "}"))
        //                {
        //                    url = url.Replace("{" + item.Key + "}", (string)item.Value);
        //                    Params.Remove(item.Key);
        //                }
        //        }

        //        HttpRequestMessage request = new HttpRequestMessage(httpMethod, httpClient.BaseAddress);

        //        //var contentdata = nvc;// new FormUrlEncodedContent(nvc);

        //        string data = null;
        //        if (Params != null)
        //        {
        //            data = "{" + String.Join(",", Params.Select(x => BuildParam(x.Key, x.Value))) + " }";
        //            //data.Replace("True", "true");
        //        }
        //        request.Content = new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded");

        //        //List<KeyValuePair<string, string>> postData = new List<KeyValuePair<string, string>>();
        //        //postData.Add(new KeyValuePair<string, string>("grant_type", "client_credentials"));
        //        //postData.Add(new KeyValuePair<string, string>("scope", "public"));

        //        //request.Content = new FormUrlEncodedContent(postData);
        //        HttpResponseMessage tokenResponse = client.PostAsync(baseUrl, new FormUrlEncodedContent(postData)).Result;


        //        Task<HttpResponseMessage> httpResponsetask = httpClient.SendAsync(request);
        //        httpResponsetask.Wait();
        //        httpResponse = httpResponsetask.Result;


        //        //var token = tokenResponse.Content.ReadAsStringAsync().Result;
        //        token = await tokenResponse.Content.ReadAsAsync<AccessTokenResponse>(new[] { new JsonMediaTypeFormatter() });



        //        request.Content = new StringContent()


        //        var client = new RestClient(url);
        //        var request = new RestRequest(new Uri(url), methodType);// Method.Post);

        //        request.AddHeader("Authorization", "Bearer " + user.AccessToken);
        //        request.AddHeader("Sid", user.SID);
        //        request.AddHeader("Auth", user.SessionToken);
        //        request.AddHeader("neo-fin-key", "neotradeapi");
        //        request.AddHeader("Content-Type", contentType);
        //        request.AddParameter("jData", jData);// "{\"am\":\"NO\", \"dq\":\"0\",\"es\":\"nse_fo\", \"mp\":\"0\", \"pc\":\"CNC\", \"pf\":\"N\", \"pr\":\"0\", \"pt\":\"MKT\", \"qt\":\"25\", \"rt\":\"DAY\", \"tp\":\"0\", \"ts\":\"BANKNIFTY22D0844000CE\", \"tt\":\"S\"}");
        //                                             //request.AddParameter("jData", "{\"am\":\"NO\", \"dq\":\"0\",\"es\":\"nse_cm\", \"mp\":\"0\", \"pc\":\"CNC\", \"pf\":\"N\", \"pr\":\"3000\", \"pt\":\"MKT\", \"qt\":\"1\", \"rt\":\"DAY\", \"tp\":\"0\", \"ts\":\"TCS-EQ\", \"tt\":\"B\"}");
        //        RestResponse httpResponse = client.Execute(request);

        //        HttpStatusCode statusCode = httpResponse.StatusCode;

        //        if (httpResponse.IsSuccessStatusCode)
        //        {
        //            //Task<string> contentStreamTask = httpResponse.Content.ReadAsStringAsync();
        //            //contentStreamTask.Wait();
        //            //string contentStream = contentStreamTask.Result;
        //            responseDictionary = Utils.JsonDeserialize(httpResponse.Content);
        //            break;
        //            //return responseDictionary;
        //        }
        //        else if ((statusCode == HttpStatusCode.TooManyRequests || statusCode == HttpStatusCode.ServiceUnavailable || statusCode == HttpStatusCode.InternalServerError) && (counter < 4))
        //        {
        //            Thread.Sleep(1);
        //            continue;
        //        }
        //        else
        //        {
        //            throw new Exception(statusCode.ToString());
        //        }
        //    }
        //    return responseDictionary;
        //}

        //private void private dynamic Request(string Route, string httpMethod, Dictionary<string, dynamic> Params = null, HttpClient httpClient = null)
        //{

        /// <summary>
        /// Modify an open order.
        /// </summary>
        /// <param name="OrderId">Id of the order to be modified</param>
        /// <param name="ParentOrderId">Id of the parent order (obtained from the /orders call) as BO is a multi-legged order</param>
        /// <param name="Exchange">Name of the exchange</param>
        /// <param name="TradingSymbol">Tradingsymbol of the instrument</param>
        /// <param name="TransactionType">BUY or SELL</param>
        /// <param name="Quantity">Quantity to transact</param>
        /// <param name="Price">For LIMIT orders</param>
        /// <param name="Product">Margin product applied to the order (margin is blocked based on this)</param>
        /// <param name="OrderType">Order type (MARKET, LIMIT etc.)</param>
        /// <param name="Validity">Order validity</param>
        /// <param name="DisclosedQuantity">Quantity to disclose publicly (for equity trades)</param>
        /// <param name="TriggerPrice">For SL, SL-M etc.</param>
        /// <param name="Variety">You can place orders of varieties; regular orders, after market orders, cover orders etc. </param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> ModifyOrder(
            string OrderId,
            //string ParentOrderId = null,
            //string Exchange = null,
            string TradingSymbol = null,
            string TransactionType = null,
            int Quantity = 25,
            decimal? Price = null,
            string Product = Constants.PRODUCT_MIS,
            //string OrderType = null,
            string Validity = Constants.VALIDITY_DAY,
            int DisclosedQuantity = 0,
            decimal? TriggerPrice = 0,
            string HsServerId = "",
            string token = "",
            //string Variety = Constants.VARIETY_REGULAR
            HttpClient httpClient = null,
            User user = null
            )
        {
            var param = new Dictionary<string, dynamic>();
            param.Add("server", HsServerId);
            //param.Add("orderId", OrderId);
            //param.Add("product", Product.ToLower());
            //param.Add("triggerPrice", TriggerPrice);
            //param.Add("quantity", Quantity);
            //param.Add("price", Price);
            //param.Add("validity", Validity);
            //param.Add("disclosedQuantity", DisclosedQuantity);

            //param.Add("server", HsServerId);
            StringBuilder sb = new StringBuilder();
            string tt = TransactionType.ToLower() == "buy" ? "B" : "S";


            //{"tk":"11536", "mp":"0", "pc":"NRML", "dd":"NA", "dq":"0", "vd":"DAY", "ts":"TCS-EQ", "tt":"B", "pr":"3001", "tp":"0", "qt":"10", "no":"220106000000185", "es":"nse_cm", "pt":"L"}

            sb.AppendFormat("\"tk\":\"{0}\", \"mp\":\"0\", \"pc\":\"NRML\", \"dd\":\"NA\", \"dq\":\"0\", \"vd\":\"DAY\", \"ts\":\"{1}\", \"tt\":\" {2} \", \"pr\":\"{3}\", \"tp\":\" 0 \", \"qt\":\"{4}\", \"no\":\"{5}\", \"es\":\"nse_cm\", \"pt\":\"L\"",
                token, TradingSymbol, tt, Price == null ? 0 : Price, Quantity, OrderId);

            //sb.AppendFormat("\"am\":\"NO\", \"dq\":\"0\",\"es\":\"nse_fo\", \"mp\":\"0\", \"pc\":\"CNC\", \"pf\":\"N\", \"pr\":\"{0}\", \"pt\":\"MKT\", \"qt\":\" {1} \", \"rt\":\"DAY\", \"tp\":\" {2} \", \"ts\":\"{3}\", \"tt\":\"{4}\"",
            //    ((OrderType == Constants.ORDER_TYPE_MARKET) ? 0 : Price), Quantity, TriggerPrice, TradingSymbol, tt).ToString();
            ////sb.Append("}");
            //param.Add("jData", sb);


            return Put("orders.modify", param, httpClient);

            //return ExecuteOrder("orders.modify", sb.ToString(), "application/x-www-form-urlencoded", RestSharp.Method.Post, httpClient, user, param);
        }

        /// <summary>
        /// Cancel an order
        /// </summary>
        /// <param name="OrderId">Id of the order to be cancelled</param>
        /// <param name="Variety">You can place orders of varieties; regular orders, after market orders, cover orders etc. </param>
        /// <param name="ParentOrderId">Id of the parent order (obtained from the /orders call) as BO is a multi-legged order</param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> CancelOrder(string OrderId, string Product = Constants.PRODUCT_SM, string ParentOrderId = null,
            HttpClient httpClient = null)
        {
            var param = new Dictionary<string, dynamic>();

            Utils.AddIfNotNull(param, "orderId", OrderId);
            Utils.AddIfNotNull(param, "product", Product.ToLower());
            //Utils.AddIfNotNull(param, "parent_order_id", ParentOrderId);
            //Utils.AddIfNotNull(param, "variety", Variety);

            //return Delete("orders.cancel", param);
            return Delete("orders.cancel", param, httpClient: httpClient);
        }

        /// <summary>
        /// Gets the collection of orders from the orderbook.
        /// </summary>
        /// <returns>List of orders.</returns>
        public List<Order> GetOrders()
        {
            var ordersData = Get("orders");

            List<Order> orders = new List<Order>();

            foreach (Dictionary<string, dynamic> item in ordersData["data"])
                orders.Add(new Order(item));

            return orders;
        }

        /// <summary>
        /// Gets information about given OrderId.
        /// </summary>
        /// <param name="OrderId">Unique order id</param>
        /// <returns>List of order objects.</returns>
        public List<KotakNeoOrder> GetOrderHistory(string OrderId, HttpClient httpClient, string HsServerId = "", User user = null)
        {
            var param = new Dictionary<string, dynamic>();
            param.Add("server", user.HsServerId);
            //param.Add("nOrdNo", OrderId);
            
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.AppendFormat("\"nOrdNo\":\"{0}\"", OrderId).ToString();
            sb.Append("}");
            param.Add("jData", sb.ToString());


            param.Add("HttpRequestHeaders-Sid", user.SID);
            param.Add("HttpRequestHeaders-Auth", user.SessionToken);
            param.Add("HttpRequestHeaders-Neo-fin-key", "neotradeapi");


            dynamic orderData = Post("reports.orderhistory", param, httpClient: httpClient);
            //orderDataTask.Wait();
            KotakNeoOrder kOrder = null;
            //dynamic orderData = orderDataTask.Result;
            List<KotakNeoOrder> orderhistory = new List<KotakNeoOrder>();

            //kOrder = new KotakOrder(orderData["data"]);

            dynamic oid;
            foreach (Dictionary<string, dynamic> item in orderData["data"])
            {
                if (item.TryGetValue("nOrdNo", out oid) && Convert.ToString(oid) == OrderId)
                {
                    kOrder = new KotakNeoOrder(item);
                    orderhistory.Add(kOrder);
                    //break;
                }
            }

            return orderhistory;

            //StringBuilder sb = new StringBuilder();
            ////{"tk":"11536", "mp":"0", "pc":"NRML", "dd":"NA", "dq":"0", "vd":"DAY", "ts":"TCS-EQ", "tt":"B", "pr":"3001", "tp":"0", "qt":"10", "no":"220106000000185", "es":"nse_cm", "pt":"L"}

            ////"jData", "{\"nOrdNo\":\"220621000000097\"}");

            //sb.AppendFormat("\"nOrdNo\":\"{0}\"", OrderId);

            //return ExecuteOrder("reports.orderhistory", sb.ToString(), "application/x-www-form-urlencoded", Method.Post, httpClient, user, param);

            ////var client = new RestClient("https://gw-napi.kotaksecurities.com/Orders/2.0/quick/order/history?sId=server1");
            ////client.Timeout = -1;
            ////var request = new RestRequest(Method.POST);

            ////request.AddParameter("jData", "{\"nOrdNo\":\"220621000000097\"}");
            ////IRestResponse response = client.Execute(request);
            ////Console.WriteLine(response.Content);
        }

        /// <summary>
        /// Retreive the list of trades executed (all or ones under a particular order).
        /// An order can be executed in tranches based on market conditions.
        /// These trades are individually recorded under an order.
        /// </summary>
        /// <param name="OrderId">is the ID of the order (optional) whose trades are to be retrieved. If no `OrderId` is specified, all trades for the day are returned.</param>
        /// <returns>List of trades of given order.</returns>
        public List<Trade> GetOrderTrades(string OrderId = null)
        {
            Dictionary<string, dynamic> tradesdata;
            if (!String.IsNullOrEmpty(OrderId))
            {
                var param = new Dictionary<string, dynamic>();
                param.Add("order_id", OrderId);
                tradesdata = Get("orders.trades", param);
            }
            else
                tradesdata = Get("trades");

            List<Trade> trades = new List<Trade>();

            foreach (Dictionary<string, dynamic> item in tradesdata["data"])
                trades.Add(new Trade(item));

            return trades;
        }

        /// <summary>
        /// Retrieve the list of positions.
        /// </summary>
        /// <returns>Day and net positions.</returns>
        public PositionResponse GetPositions()
        {
            var positionsdata = Get("portfolio.positions");
            return new PositionResponse(positionsdata["data"]);
        }
        public List<KotakPosition> GetPositions(HttpClient httpClient, User user)
        {
            var param = new Dictionary<string, dynamic>();
            param.Add("server", user.HsServerId);

            param.Add("HttpRequestHeaders-accept", "application/json");
            param.Add("HttpRequestHeaders-Sid", user.SID);
            param.Add("HttpRequestHeaders-Auth", user.SessionToken);
            param.Add("HttpRequestHeaders-Neo-fin-key", "neotradeapi");

            var positionsdata = Get("portfolio.positions", param, httpClient: httpClient);

            List<KotakPosition> positions = new List<KotakPosition>();

            foreach (Dictionary<string, dynamic> item in positionsdata["data"])
                positions.Add(new KotakPosition(item));


            return positions;
        }

        /// <summary>
        /// Retrieve the list of equity holdings.
        /// </summary>
        /// <returns>List of holdings.</returns>
        public List<Holding> GetHoldings()
        {
            var holdingsData = Get("portfolio.holdings");

            List<Holding> holdings = new List<Holding>();

            foreach (Dictionary<string, dynamic> item in holdingsData["data"])
                holdings.Add(new Holding(item));

            return holdings;
        }

        /// <summary>
        /// Modify an open position's product type.
        /// </summary>
        /// <param name="Exchange">Name of the exchange</param>
        /// <param name="TradingSymbol">Tradingsymbol of the instrument</param>
        /// <param name="TransactionType">BUY or SELL</param>
        /// <param name="PositionType">overnight or day</param>
        /// <param name="Quantity">Quantity to convert</param>
        /// <param name="OldProduct">Existing margin product of the position</param>
        /// <param name="NewProduct">Margin product to convert to</param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> ConvertPosition(
            string Exchange,
            string TradingSymbol,
            string TransactionType,
            string PositionType,
            int? Quantity,
            string OldProduct,
            string NewProduct)
        {
            var param = new Dictionary<string, dynamic>();

            Utils.AddIfNotNull(param, "exchange", Exchange);
            Utils.AddIfNotNull(param, "tradingsymbol", TradingSymbol);
            Utils.AddIfNotNull(param, "transaction_type", TransactionType);
            Utils.AddIfNotNull(param, "position_type", PositionType);
            Utils.AddIfNotNull(param, "quantity", Quantity.ToString());
            Utils.AddIfNotNull(param, "old_product", OldProduct);
            Utils.AddIfNotNull(param, "new_product", NewProduct);

            return Put("portfolio.positions.modify", param);
        }

        /// <summary>
        /// Retrieve the list of market instruments available to trade.
        /// Note that the results could be large, several hundred KBs in size,
		/// with tens of thousands of entries in the list.
        /// </summary>
        /// <param name="Exchange">Name of the exchange</param>
        /// <returns>List of instruments.</returns>
        public List<Instrument> GetInstruments(string Exchange = null)
        {
            var param = new Dictionary<string, dynamic>();

            List<Dictionary<string, dynamic>> instrumentsData;

            if (String.IsNullOrEmpty(Exchange))
                instrumentsData = Get("scripmaster.download", param);
            else
            {
                //param.Add("exchange", Exchange);
                instrumentsData = Get("scripmaster.download", param);
            }

            List<Instrument> instruments = new List<Instrument>();

            foreach (Dictionary<string, dynamic> item in instrumentsData)
                instruments.Add(new Instrument(item));

            return instruments;
        }

        /// <summary>
        /// Retrieve quote and market depth of upto 200 instruments
        /// </summary>
        /// <param name="InstrumentId">Indentification of instrument in the form of EXCHANGE:TRADINGSYMBOL (eg: NSE:INFY) or InstrumentToken (eg: 408065)</param>
        /// <returns>Dictionary of all Quote objects with keys as in InstrumentId</returns>
        public Dictionary<string, Quote> GetQuote(string[] InstrumentId)
        {
            var param = new Dictionary<string, dynamic>();
            param.Add("i", InstrumentId);
            Dictionary<string, dynamic> quoteData = Get("market.quote", param)["data"];

            Dictionary<string, Quote> quotes = new Dictionary<string, Quote>();
            foreach (string item in quoteData.Keys)
                quotes.Add(item, new Quote(quoteData[item]));

            return quotes;
        }

        /// <summary>
        /// Retrieve LTP and OHLC of upto 200 instruments
        /// </summary>
        /// <param name="InstrumentId">Indentification of instrument in the form of EXCHANGE:TRADINGSYMBOL (eg: NSE:INFY) or InstrumentToken (eg: 408065)</param>
        /// <returns>Dictionary of all OHLC objects with keys as in InstrumentId</returns>
        public Dictionary<string, OHLC> GetOHLC(string[] InstrumentId)
        {
            var param = new Dictionary<string, dynamic>();
            param.Add("i", InstrumentId);
            Dictionary<string, dynamic> ohlcData = Get("market.ohlc", param)["data"];

            Dictionary<string, OHLC> ohlcs = new Dictionary<string, OHLC>();
            foreach (string item in ohlcData.Keys)
                ohlcs.Add(item, new OHLC(ohlcData[item]));

            return ohlcs;
        }

        /// <summary>
        /// Retrieve LTP of upto 200 instruments
        /// </summary>
        /// <param name="InstrumentId">Indentification of instrument in the form of EXCHANGE:TRADINGSYMBOL (eg: NSE:INFY) or InstrumentToken (eg: 408065)</param>
        /// <returns>Dictionary with InstrumentId as key and LTP as value.</returns>
        public Dictionary<string, LTP> GetLTP(string[] InstrumentId)
        {
            var param = new Dictionary<string, dynamic>();
            param.Add("i", InstrumentId);
            Dictionary<string, dynamic> ltpData = Get("market.ltp", param)["data"];

            Dictionary<string, LTP> ltps = new Dictionary<string, LTP>();
            foreach (string item in ltpData.Keys)
                ltps.Add(item, new LTP(ltpData[item]));

            return ltps;
        }

        /// <summary>
        /// Retrieve historical data (candles) for an instrument.
        /// </summary>
        /// <param name="InstrumentToken">Identifier for the instrument whose historical records you want to fetch. This is obtained with the instrument list API.</param>
        /// <param name="FromDate">Date in format yyyy-MM-dd for fetching candles between two days. Date in format yyyy-MM-dd hh:mm:ss for fetching candles between two timestamps.</param>
        /// <param name="ToDate">Date in format yyyy-MM-dd for fetching candles between two days. Date in format yyyy-MM-dd hh:mm:ss for fetching candles between two timestamps.</param>
        /// <param name="Interval">The candle record interval. Possible values are: minute, day, 3minute, 5minute, 10minute, 15minute, 30minute, 60minute</param>
        /// <param name="Continuous">Pass true to get continous data of expired instruments.</param>
        /// <param name="OI">Pass true to get open interest data.</param>
        /// <returns>List of Historical objects.</returns>
        public List<Historical> GetHistoricalData(
            string InstrumentToken,
            DateTime FromDate,
            DateTime ToDate,
            string Interval,
            bool Continuous = false,
            bool OI = false)
        {
            var param = new Dictionary<string, dynamic>();

            param.Add("instrument_token", InstrumentToken);
            param.Add("from", FromDate.ToString("yyyy-MM-dd HH:mm:ss"));
            param.Add("to", ToDate.ToString("yyyy-MM-dd HH:mm:ss"));
            param.Add("interval", Interval);
            param.Add("continuous", Continuous ? "1" : "0");
            param.Add("oi", OI ? "1" : "0");

            var historicalData = Get("market.historical", param);

            List<Historical> historicals = new List<Historical>();

            foreach (ArrayList item in historicalData["data"]["candles"])
                historicals.Add(new Historical(item));

            return historicals;
        }

        /// <summary>
        /// Retrieve the buy/sell trigger range for Cover Orders.
        /// </summary>
        /// <param name="InstrumentId">Indentification of instrument in the form of EXCHANGE:TRADINGSYMBOL (eg: NSE:INFY) or InstrumentToken (eg: 408065)</param>
        /// <param name="TrasactionType">BUY or SELL</param>
        /// <returns>List of trigger ranges for given instrument ids for given transaction type.</returns>
        public Dictionary<string, TrigerRange> GetTriggerRange(string[] InstrumentId, string TrasactionType)
        {
            var param = new Dictionary<string, dynamic>();

            param.Add("i", InstrumentId);
            param.Add("transaction_type", TrasactionType.ToLower());

            var triggerdata = Get("market.trigger_range", param)["data"];

            Dictionary<string, TrigerRange> triggerRanges = new Dictionary<string, TrigerRange>();
            foreach (string item in triggerdata.Keys)
                triggerRanges.Add(item, new TrigerRange(triggerdata[item]));

            return triggerRanges;
        }

        #region GTT

        /// <summary>
        /// Retrieve the list of GTTs.
        /// </summary>
        /// <returns>List of GTTs.</returns>
        public List<GTT> GetGTTs()
        {
            var gttsdata = Get("gtt");

            List<GTT> gtts = new List<GTT>();

            foreach (Dictionary<string, dynamic> item in gttsdata["data"])
                gtts.Add(new GTT(item));

            return gtts;
        }


        /// <summary>
        /// Retrieve a single GTT
        /// </summary>
        /// <param name="GTTId">Id of the GTT</param>
        /// <returns>GTT info</returns>
        public GTT GetGTT(int GTTId)
        {
            var param = new Dictionary<string, dynamic>();
            param.Add("id", GTTId.ToString());

            var gttdata = Get("gtt.info", param);

            return new GTT(gttdata["data"]);
        }

        /// <summary>
        /// Place a GTT order
        /// </summary>
        /// <param name="gttParams">Contains the parameters for the GTT order</param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        //public Dictionary<string, dynamic> PlaceGTT(GTTParams gttParams)
        //{
        //    var condition = new Dictionary<string, dynamic>();
        //    condition.Add("exchange", gttParams.Exchange);
        //    condition.Add("tradingsymbol", gttParams.TradingSymbol);
        //    condition.Add("trigger_values", gttParams.TriggerPrices);
        //    condition.Add("last_price", gttParams.LastPrice);
        //    condition.Add("instrument_token", gttParams.InstrumentToken);

        //    var ordersParam = new List<Dictionary<string, dynamic>>();
        //    foreach (var o in gttParams.Orders)
        //    {
        //        var order = new Dictionary<string, dynamic>();
        //        order["exchange"] = gttParams.Exchange;
        //        order["tradingsymbol"] = gttParams.TradingSymbol;
        //        order["transaction_type"] = o.TransactionType;
        //        order["quantity"] = o.Quantity;
        //        order["price"] = o.Price;
        //        order["order_type"] = o.OrderType;
        //        order["product"] = o.Product;
        //        ordersParam.Add(order);
        //    }

        //    var parms = new Dictionary<string, dynamic>();
        //    parms.Add("condition", Utils.JsonSerialize(condition));
        //    parms.Add("orders", Utils.JsonSerialize(ordersParam));
        //    parms.Add("type", gttParams.TriggerType);

        //    return Post("gtt.place", parms);
        //}

        /// <summary>
        /// Modify a GTT order
        /// </summary>
        /// <param name="GTTId">Id of the GTT to be modified</param>
        /// <param name="gttParams">Contains the parameters for the GTT order</param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> ModifyGTT(int GTTId, GTTParams gttParams)
        {
            var condition = new Dictionary<string, dynamic>();
            condition.Add("exchange", gttParams.Exchange);
            condition.Add("tradingsymbol", gttParams.TradingSymbol);
            condition.Add("trigger_values", gttParams.TriggerPrices);
            condition.Add("last_price", gttParams.LastPrice);
            condition.Add("instrument_token", gttParams.InstrumentToken);

            var ordersParam = new List<Dictionary<string, dynamic>>();
            foreach (var o in gttParams.Orders)
            {
                var order = new Dictionary<string, dynamic>();
                order["exchange"] = gttParams.Exchange;
                order["tradingsymbol"] = gttParams.TradingSymbol;
                order["transaction_type"] = o.TransactionType;
                order["quantity"] = o.Quantity;
                order["price"] = o.Price;
                order["order_type"] = o.OrderType;
                order["product"] = o.Product;
                ordersParam.Add(order);
            }

            var parms = new Dictionary<string, dynamic>();
            parms.Add("condition", Utils.JsonSerialize(condition));
            parms.Add("orders", Utils.JsonSerialize(ordersParam));
            parms.Add("type", gttParams.TriggerType);
            parms.Add("id", GTTId.ToString());

            return Put("gtt.modify", parms);
        }

        /// <summary>
        /// Cancel a GTT order
        /// </summary>
        /// <param name="GTTId">Id of the GTT to be modified</param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> CancelGTT(int GTTId)
        {
            var parms = new Dictionary<string, dynamic>();
            parms.Add("id", GTTId.ToString());

            return Delete("gtt.delete", parms);
        }

        #endregion GTT


        #region MF Calls

        /// <summary>
        /// Gets the Mutual funds Instruments.
        /// </summary>
        /// <returns>The Mutual funds Instruments.</returns>
        public List<MFInstrument> GetMFInstruments()
        {
            var param = new Dictionary<string, dynamic>();

            List<Dictionary<string, dynamic>> instrumentsData;

            instrumentsData = Get("mutualfunds.instruments", param);

            List<MFInstrument> instruments = new List<MFInstrument>();

            foreach (Dictionary<string, dynamic> item in instrumentsData)
                instruments.Add(new MFInstrument(item));

            return instruments;
        }

        /// <summary>
        /// Gets all Mutual funds orders.
        /// </summary>
        /// <returns>The Mutual funds orders.</returns>
        public List<MFOrder> GetMFOrders()
        {
            var param = new Dictionary<string, dynamic>();

            Dictionary<string, dynamic> ordersData;
            ordersData = Get("mutualfunds.orders", param);

            List<MFOrder> orderlist = new List<MFOrder>();

            foreach (Dictionary<string, dynamic> item in ordersData["data"])
                orderlist.Add(new MFOrder(item));

            return orderlist;
        }

        /// <summary>
        /// Gets the Mutual funds order by OrderId.
        /// </summary>
        /// <returns>The Mutual funds order.</returns>
        /// <param name="OrderId">Order id.</param>
        public MFOrder GetMFOrders(String OrderId)
        {
            var param = new Dictionary<string, dynamic>();
            param.Add("order_id", OrderId);

            Dictionary<string, dynamic> orderData;
            orderData = Get("mutualfunds.order", param);

            return new MFOrder(orderData["data"]);
        }

        /// <summary>
        /// Places a Mutual funds order.
        /// </summary>
        /// <returns>JSON response as nested string dictionary.</returns>
        /// <param name="TradingSymbol">Tradingsymbol (ISIN) of the fund.</param>
        /// <param name="TransactionType">BUY or SELL.</param>
        /// <param name="Amount">Amount worth of units to purchase. Not applicable on SELLs.</param>
        /// <param name="Quantity">Quantity to SELL. Not applicable on BUYs. If the holding is less than minimum_redemption_quantity, all the units have to be sold.</param>
        /// <param name="Tag">An optional tag to apply to an order to identify it (alphanumeric, max 8 chars).</param>
        //public Dictionary<string, dynamic> PlaceMFOrder(
        //    string TradingSymbol,
        //    string TransactionType,
        //    decimal? Amount,
        //    decimal? Quantity = null,
        //    string Tag = "")
        //{
        //    var param = new Dictionary<string, dynamic>();

        //    Utils.AddIfNotNull(param, "tradingsymbol", TradingSymbol);
        //    Utils.AddIfNotNull(param, "transaction_type", TransactionType);
        //    Utils.AddIfNotNull(param, "amount", Amount.ToString());
        //    Utils.AddIfNotNull(param, "quantity", Quantity.ToString());
        //    Utils.AddIfNotNull(param, "tag", Tag);

        //    return Post("mutualfunds.orders.place", param);
        //}

        /// <summary>
        /// Cancels the Mutual funds order.
        /// </summary>
        /// <returns>JSON response as nested string dictionary.</returns>
        /// <param name="OrderId">Unique order id.</param>
        public Dictionary<string, dynamic> CancelMFOrder(String OrderId)
        {
            var param = new Dictionary<string, dynamic>();

            Utils.AddIfNotNull(param, "order_id", OrderId);

            return Delete("mutualfunds.cancel_order", param);
        }

        /// <summary>
        /// Gets all Mutual funds SIPs.
        /// </summary>
        /// <returns>The list of all Mutual funds SIPs.</returns>
        public List<MFSIP> GetMFSIPs()
        {
            var param = new Dictionary<string, dynamic>();

            Dictionary<string, dynamic> sipData;
            sipData = Get("mutualfunds.sips", param);

            List<MFSIP> siplist = new List<MFSIP>();

            foreach (Dictionary<string, dynamic> item in sipData["data"])
                siplist.Add(new MFSIP(item));

            return siplist;
        }

        /// <summary>
        /// Gets a single Mutual funds SIP by SIP id.
        /// </summary>
        /// <returns>The Mutual funds SIP.</returns>
        /// <param name="SIPID">SIP id.</param>
        public MFSIP GetMFSIPs(String SIPID)
        {
            var param = new Dictionary<string, dynamic>();
            param.Add("sip_id", SIPID);

            Dictionary<string, dynamic> sipData;
            sipData = Get("mutualfunds.sip", param);

            return new MFSIP(sipData["data"]);
        }

        /// <summary>
        /// Places a Mutual funds SIP order.
        /// </summary>
        /// <returns>JSON response as nested string dictionary.</returns>
        /// <param name="TradingSymbol">ISIN of the fund.</param>
        /// <param name="Amount">Amount worth of units to purchase. It should be equal to or greated than minimum_additional_purchase_amount and in multiple of purchase_amount_multiplier in the instrument master.</param>
        /// <param name="InitialAmount">Amount worth of units to purchase before the SIP starts. Should be equal to or greater than minimum_purchase_amount and in multiple of purchase_amount_multiplier. This is only considered if there have been no prior investments in the target fund.</param>
        /// <param name="Frequency">weekly, monthly, or quarterly.</param>
        /// <param name="InstalmentDay">If Frequency is monthly, the day of the month (1, 5, 10, 15, 20, 25) to trigger the order on.</param>
        /// <param name="Instalments">Number of instalments to trigger. If set to -1, instalments are triggered at fixed intervals until the SIP is cancelled.</param>
        /// <param name="Tag">An optional tag to apply to an order to identify it (alphanumeric, max 8 chars).</param>
        //public Dictionary<string, dynamic> PlaceMFSIP(
        //    string TradingSymbol,
        //    decimal? Amount,
        //    decimal? InitialAmount,
        //    string Frequency,
        //    int? InstalmentDay,
        //    int? Instalments,
        //    string Tag = "")
        //{
        //    var param = new Dictionary<string, dynamic>();

        //    Utils.AddIfNotNull(param, "tradingsymbol", TradingSymbol);
        //    Utils.AddIfNotNull(param, "initial_amount", InitialAmount.ToString());
        //    Utils.AddIfNotNull(param, "amount", Amount.ToString());
        //    Utils.AddIfNotNull(param, "frequency", Frequency);
        //    Utils.AddIfNotNull(param, "instalment_day", InstalmentDay.ToString());
        //    Utils.AddIfNotNull(param, "instalments", Instalments.ToString());

        //    return Post("mutualfunds.sips.place", param);
        //}

        /// <summary>
        /// Modifies the Mutual funds SIP.
        /// </summary>
        /// <returns>JSON response as nested string dictionary.</returns>
        /// <param name="SIPId">SIP id.</param>
        /// <param name="Amount">Amount worth of units to purchase. It should be equal to or greated than minimum_additional_purchase_amount and in multiple of purchase_amount_multiplier in the instrument master.</param>
        /// <param name="Frequency">weekly, monthly, or quarterly.</param>
        /// <param name="InstalmentDay">If Frequency is monthly, the day of the month (1, 5, 10, 15, 20, 25) to trigger the order on.</param>
        /// <param name="Instalments">Number of instalments to trigger. If set to -1, instalments are triggered idefinitely until the SIP is cancelled.</param>
        /// <param name="Status">Pause or unpause an SIP (active or paused).</param>
        public Dictionary<string, dynamic> ModifyMFSIP(
            string SIPId,
            decimal? Amount,
            string Frequency,
            int? InstalmentDay,
            int? Instalments,
            string Status)
        {
            var param = new Dictionary<string, dynamic>();

            Utils.AddIfNotNull(param, "status", Status);
            Utils.AddIfNotNull(param, "sip_id", SIPId);
            Utils.AddIfNotNull(param, "amount", Amount.ToString());
            Utils.AddIfNotNull(param, "frequency", Frequency.ToString());
            Utils.AddIfNotNull(param, "instalment_day", InstalmentDay.ToString());
            Utils.AddIfNotNull(param, "instalments", Instalments.ToString());

            return Put("mutualfunds.sips.modify", param);
        }

        /// <summary>
        /// Cancels the Mutual funds SIP.
        /// </summary>
        /// <returns>JSON response as nested string dictionary.</returns>
        /// <param name="SIPId">SIP id.</param>
		public Dictionary<string, dynamic> CancelMFSIP(String SIPId)
        {
            var param = new Dictionary<string, dynamic>();

            Utils.AddIfNotNull(param, "sip_id", SIPId);

            return Delete("mutualfunds.cancel_sips", param);
        }

        /// <summary>
        /// Gets the Mutual funds holdings.
        /// </summary>
        /// <returns>The list of all Mutual funds holdings.</returns>
        public List<MFHolding> GetMFHoldings()
        {
            var param = new Dictionary<string, dynamic>();

            Dictionary<string, dynamic> holdingsData;
            holdingsData = Get("mutualfunds.holdings", param);

            List<MFHolding> holdingslist = new List<MFHolding>();

            foreach (Dictionary<string, dynamic> item in holdingsData["data"])
                holdingslist.Add(new MFHolding(item));

            return holdingslist;
        }

        #endregion

        #region HTTP Functions

        /// <summary>
        /// Alias for sending a GET request.
        /// </summary>
        /// <param name="Route">URL route of API</param>
        /// <param name="Params">Additional paramerters</param>
        /// <returns>Varies according to API endpoint</returns>
        private dynamic Get(string Route, Dictionary<string, dynamic> Params = null, HttpClient httpClient = null)
        {
            return Request(Route, "GET", Params, httpClient);
        }

        /// <summary>
        /// Alias for sending a POST request.
        /// </summary>
        /// <param name="Route">URL route of API</param>
        /// <param name="Params">Additional paramerters</param>
        /// <returns>Varies according to API endpoint</returns>
        private dynamic Post(string Route, Dictionary<string, dynamic> Params = null, HttpClient httpClient = null)
        {
            return Request(Route, "POST", Params, httpClient);
        }

        /// <summary>
        /// Alias for sending a PUT request.
        /// </summary>
        /// <param name="Route">URL route of API</param>
        /// <param name="Params">Additional paramerters</param>
        /// <returns>Varies according to API endpoint</returns>
        private dynamic Put(string Route, Dictionary<string, dynamic> Params = null, HttpClient httpClient = null)
        {
            return Request(Route, "PUT", Params, httpClient);
        }

        /// <summary>
        /// Alias for sending a DELETE request.
        /// </summary>
        /// <param name="Route">URL route of API</param>
        /// <param name="Params">Additional paramerters</param>
        /// <returns>Varies according to API endpoint</returns>
        private dynamic Delete(string Route, Dictionary<string, dynamic> Params = null, HttpClient httpClient = null)
        {
            return Request(Route, "DELETE", Params, httpClient);
        }

        /// <summary>
        /// Adds extra headers to request
        /// </summary>
        /// <param name="Req">Request object to add headers</param>
        private void AddExtraHeaders(ref HttpWebRequest Req)
        {
            //var KiteAssembly = System.Reflection.Assembly.GetAssembly(typeof(Kotak));
            //if (KiteAssembly != null)
            //    Req.UserAgent = "KiteConnect.Net/" + KiteAssembly.GetName().Version;

            Req.Headers.Add("accept", "application/json");
            //Req.Headers.Add("Content-Type", "application/json");
            Req.Headers.Add("consumerKey", _consumerKey);

            if (_sessionToken == null)
            {
                Req.Headers.Add("appId", _appId);
                Req.Headers.Add("ip", _ipAddress);
                //Req.Headers.Add("oneTimeToken", _ott);
            }
            else
            {
                Req.Headers.Add("sessionToken", _sessionToken);
            }
            Req.Headers.Add("Authorization", "Bearer " + _accessToken);


            //if(Req.Method == "GET" && cache.IsCached(Req.RequestUri.AbsoluteUri))
            //{
            //    Req.Headers.Add("If-None-Match: " + cache.GetETag(Req.RequestUri.AbsoluteUri));
            //}

            //Req.Timeout = _timeout;
            //if (_proxy != null) Req.Proxy = _proxy;

            //if (_enableLogging)
            //{
            //    foreach (string header in Req.Headers.Keys)
            //    {
            //        Console.WriteLine("DEBUG: " + header + ": " + Req.Headers.GetValues(header)[0]);
            //    }
            //}
        }


        /// <summary>
        /// Make an HTTP request.
        /// </summary>
        /// <param name="Route">URL route of API</param>
        /// <param name="Method">Method of HTTP request</param>
        /// <param name="Params">Additional paramerters</param>
        /// <returns>Varies according to API endpoint</returns>
        private dynamic Request(string Route, string httpMethod, Dictionary<string, dynamic> Params = null, HttpClient httpClient = null)
        {
            //string url = _root + _routes[Route];
            string url = httpClient.BaseAddress.AbsoluteUri.TrimEnd('/') + _routes[Route];
            HttpRequestMessage httpRequest;
            Dictionary<string, string> httprequestheaders = null;
            if (Params.Keys.Any(x=>x.StartsWith("HttpRequestHeaders")))
            {
                httprequestheaders = new Dictionary<string, string>();
                for (int i=0;i<Params.Count;i++)
                {
                    var kvPair = Params.ElementAt(i);
                    if (kvPair.Key.StartsWith("HttpRequestHeaders"))
                    {
                        httprequestheaders.Add(kvPair.Key.Substring(kvPair.Key.IndexOf("-") + 1), kvPair.Value);
                        Params.Remove(kvPair.Key);
                        i = i - 1;
                    }
                }
            }

            if (url.Contains("{"))
            {
                var urlparams = Params.ToDictionary(entry => entry.Key, entry => entry.Value);

                foreach (KeyValuePair<string, dynamic> item in urlparams)
                    if (url.Contains("{" + item.Key + "}"))
                    {
                        url = url.Replace("{" + item.Key + "}", (string)item.Value);
                        Params.Remove(item.Key);
                    }
            }
            string data = null;
            if (Params != null)
            {
                data = "{" + String.Join(",", Params.Select(x => BuildParam(x.Key, x.Value))) + " }";
                //data.Replace('T', 't');
            }
            //ServicePointManager.DefaultConnectionLimit = 20;
            //ServicePointManager.Expect100Continue = false;
            
            HttpResponseMessage httpResponse;
            StringContent dataJson = null;
            //HttpWebResponse webResponse = null;
            Dictionary<string, dynamic> responseDictionary;
            int counter = 0;

            while (true)
            {
                httpRequest = new HttpRequestMessage();
                httpRequest.Method = new HttpMethod(httpMethod);

                if (httprequestheaders != null)
                {
                    foreach (var httpHeader in httprequestheaders)
                    {
                        httpRequest.Headers.Add(httpHeader.Key, httpHeader.Value);
                    }
                }
                //Commented as this is moved to httpclient object.

                //httpRequest.Headers.Add("accept", "application/json");
                //httpRequest.Headers.Add("consumerKey", _consumerKey);
                //httpRequest.Headers.Add("Authorization", "Bearer " + _accessToken);
                //httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));


                if (httpMethod == "POST" || httpMethod == "PUT")
                {
                    //httpRequest = new HttpRequestMessage(new HttpMethod(Method), url);

                    //httpRequest.Method = new HttpMethod(Method);

                    httpRequest.RequestUri = new Uri(url);
                    //httpRequest.Headers.Add("accept", "application/json");
                    //httpRequest.Headers.Add("consumerKey", _consumerKey);
                    //httpRequest.Headers.Add("Authorization", "Bearer " + _accessToken);
                    ////httpRequest.Headers.Add("Content-Type", "application/json");
                    //httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    //if (Route != "scripmaster.download")
                    //{
                    //}
                    if (Route == "session.token")
                    {
                       // httpRequest.Headers.Add("oneTimeToken", _ott);
                    }
                    if(Route == "")
                    {

                    }
                    if (Route == "orders.place" || Route == "orders.modify" || Route == "reports.orderhistory" || Route == "portfolio.positions")
                    {
                        data = null;
                        if (Params != null)
                        {

                            //foreach (var param in Params)
                            //{
                            //    data += param.Key + "=" + param.Value.ToString();
                            //    data = param.Value.ToString();
                            //}
                            //data = String.Join(",", Params.Select(x => BuildParam(x.Key, x.Value)));
                            Dictionary<string, string> body = new Dictionary<string, string>();
                            foreach (var param in Params)
                            {
                                body.Add(param.Key, param.Value.ToString());
                            }

                            //data.Replace("True", "true");
                            var contentdata = new FormUrlEncodedContent(body);

                            ////var content = await res.Content.ReadAsStringAsync();
                            ////Console.WriteLine(content);
                            //contentdata.Wait();
                            //var urlEncodedString = contentdata.Result;


                            //data = "jData"+ ":" + urlEncodedString;
                            httpRequest.Content = contentdata;// new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded");
                            //httpRequest.Content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                        }

                        //Commented as this is moved to httpclient object.
                        //httpRequest.Headers.Add("sessionToken", _sessionToken);

                        //var urlparams = Params.ToDictionary(entry => entry.Key, entry => entry.Value);

                        //Dictionary<string, string> body = new Dictionary<string, string>();
                        //var nvc = new List<KeyValuePair<string, string>>();

                        ////nvc.Add(new KeyValuePair<string, string>("Content-Type", "application/x-www-form-urlencoded"));
                        //foreach (KeyValuePair<string, dynamic> item in urlparams)
                        //{
                        //    //body.Add(item.Key, "\"" + item.Value.ToString() + "\"");
                        //    //body.Add(item.Key, item.Value.ToString());

                        //    nvc.Add(new KeyValuePair<string, string>(item.Key, item.Value.ToString()));
                        //}
                        ////var contentdata = new FormUrlEncodedContent(body);
                        //var contentdata = nvc;// new FormUrlEncodedContent(nvc);
                        
                        //httpRequest.Content.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded");
                    }
                    else
                    {
                        dataJson = new StringContent(data, Encoding.UTF8, Application.Json);
                        httpRequest.Content = dataJson;

                        //httpRequest.Headers.Add("ip", _ipAddress);
                        //httpRequest.Headers.Add("appId", _appId);

                        //httpRequest.Content.
                        //Encoding.UTF8, 
                        //            "application/json");//CONTENT-TYPE header
                    }

                    if (data != null)
                    {
                        //dataJson = new StringContent(data, Encoding.UTF8, Application.Json);
                        //httpRequest.Content = dataJson;
                        


                        //using (var streamWriter = new StreamWriter(httpRequest.Content.stre GetRequestStream()))
                        //{
                        //    streamWriter.Write(data);
                        //}
                    }
                }
                else
                {
                    if (data == null || data == "{ }")
                    {
                        //httpRequest = (HttpWebRequest)WebRequest.Create(url);
                        //httpRequest = new HttpRequestMessage(new HttpMethod(Method), url);
                        httpRequest.RequestUri = new Uri(url);
                    }
                    else
                    {
                        //httpRequest = (HttpWebRequest)WebRequest.Create(url + "?" + data);
                        httpRequest.RequestUri = new Uri(url + "?" + data);
                        //httpRequest = new HttpRequestMessage(new HttpMethod(Method), url + "?" + data);
                    }
                    //httpRequest.AllowAutoRedirect = true;

                    //httpRequest.Headers.Add("sessionToken", _sessionToken);
                }

               

                counter++;
                try
                {
                    //httpRequest.Proxy = null;
                    //webResponse = (HttpWebResponse)httpRequest.GetResponse();
                    semaphore.WaitAsync();


                    //var client = new RestClient(httpRequest.RequestUri);
                    //client.Timeout = -1;
                    //var request = new RestRequest(new Uri(url), Method.Post);
                    //request.AddHeader("Authorization", "Bearer eyJ4NXQiOiJNbUprWWpVMlpETmpNelpqTURBM05UZ3pObUUxTm1NNU1qTXpNR1kyWm1OaFpHUTFNakE1TmciLCJraWQiOiJaalJqTUdRek9URmhPV1EwTm1WallXWTNZemRtWkdOa1pUUmpaVEUxTlRnMFkyWTBZVEUyTlRCaVlURTRNak5tWkRVeE5qZ3pPVGM0TWpGbFkyWXpOUV9SUzI1NiIsImFsZyI6IlJTMjU2In0.eyJzdWIiOiJjbGllbnQxMTg4IiwiYXV0IjoiQVBQTElDQVRJT04iLCJhdWQiOiJQekIyZTh4eFhKelBnSHVqckZraXNkZjZ6cklhIiwibmJmIjoxNjcwMjI2MzU5LCJhenAiOiJQekIyZTh4eFhKelBnSHVqckZraXNkZjZ6cklhIiwic2NvcGUiOiJkZWZhdWx0IiwiaXNzIjoiaHR0cHM6XC9cL25hcGkua290YWtzZWN1cml0aWVzLmNvbTo0NDNcL29hdXRoMlwvdG9rZW4iLCJleHAiOjE2NzAyNTUxNTksImlhdCI6MTY3MDIyNjM1OSwianRpIjoiNTliYjIwMDUtOGNlYS00NjUyLThjOTEtNDFlMDUxMTczNTc4In0.AZTWXii5TNNy3qF0GAxEda9g-03oDWQwphyDFny3PRnhn5qwBIWxTfTFliuv-eu5WuZuWa-4-pZcaqHbbGqNhT1TIrVNvqr3d2W0KwyzY-FrzmmqFBud_tIiuAFigcBTc39-twMATnnCQkoe9PP690drHWZ-gQIHgRWESOX1yryG6d3ma-yQ8eAv8JXIT4dLQCRul3g9bPow84Hym3sBKVOpQETn12whTwsMF3JSIe9WtUqnenNZJi4BBEDSAF6vlYYbq7ZYlEKrv0wiC7590H7-GR6q9rE5U8Lf2SeXk536ZF2jQDVz7a9_P5AyFlXigsIGxRo77jSYweC5BRb_8g");
                    //request.AddHeader("Sid", "65a7c3f8-ae21-484d-b2aa-05629c04276d");
                    //request.AddHeader("Auth", "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzY29wZSI6WyJUcmFkZSJdLCJleHAiOjE2NzAyNjUwMDAsImp0aSI6IjdkMGQxZjg1LTJjNTUtNDNkOS05ZTk2LWU0Y2E1NjE1OTQwMyIsImlhdCI6MTY3MDIyNjU1NCwiaXNzIjoibG9naW4tc2VydmljZSIsInN1YiI6IjI2NDI5OGIyLWFiNjAtNDc4MC1hMjE4LTg1Zjg0Mjg3OTE4OSIsInVjYyI6IlpET0tOIiwicGFuIjoiQUxNUE0wMDUyQiIsImZldGNoY2FjaGluZ3J1bGUiOjAsImNhdGVnb3Jpc2F0aW9uIjoiIn0.ikpr1cbnL4ULWFxxS5i857dRM8mBBSf55W_6-6ekHiie3L75SbC97tK2-LjERss7LGdm_LlgSZSpqiIS-Fg-4-8nLix-OEuTSanet9ArTq86VxtUXsbGTeKM4lUbNTf4DIqpV3KMxmOyzWW39O4g14eAnVYXn4QM407gNXd7xBa-2KzGaEm7rwm7GOv4Vm8AFwRFFJaYe3Qm94Ss09kEQ4Iy70RBmyj5x4GsCBo8ePNuDdTQppySpbvrUblP-8hVIhmnQU5f1Jic1oBkv71oYBzMiBnVbMEYRl7wXQV7pm5R-nl-PYcgSWi2g6PRAilWe3a0Z84AGztArd7H5-q9jA");
                    //request.AddHeader("neo-fin-key", "neotradeapi");
                    //request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                    //request.AddParameter("jData", "{\"am\":\"NO\", \"dq\":\"0\",\"es\":\"nse_fo\", \"mp\":\"0\", \"pc\":\"CNC\", \"pf\":\"N\", \"pr\":\"0\", \"pt\":\"MKT\", \"qt\":\"25\", \"rt\":\"DAY\", \"tp\":\"0\", \"ts\":\"BANKNIFTY22D0844000CE\", \"tt\":\"S\"}");
                    ////request.AddParameter("jData", "{\"am\":\"NO\", \"dq\":\"0\",\"es\":\"nse_cm\", \"mp\":\"0\", \"pc\":\"CNC\", \"pf\":\"N\", \"pr\":\"3000\", \"pt\":\"MKT\", \"qt\":\"1\", \"rt\":\"DAY\", \"tp\":\"0\", \"ts\":\"TCS-EQ\", \"tt\":\"B\"}");
                    //RestResponse response = client.Execute(request);
                    //Console.WriteLine(response.Content);

                    //var client = new HttpClient();
                    //var request = new HttpRequestMessage(HttpMethod.Get, "https://gw-napi.kotaksecurities.com/Orders/2.0/quick/user/positions?sId=server5");
                    //request.Headers.Add("accept", "application/json");
                    //request.Headers.Add("Sid", "7fda0cef-1d69-4b47-ab6b-511094378f34");
                    //request.Headers.Add("Auth", "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzY29wZSI6WyJUcmFkZSJdLCJleHAiOjE3MjE1MDAyMDAsImp0aSI6IjM3ODcxZjk3LTJmNGUtNDMxOC1hOWU2LTVmMmNiNGJmM2MwOSIsImlhdCI6MTcyMTQyNTAwNCwiaXNzIjoibG9naW4tc2VydmljZSIsInN1YiI6IjI2NDI5OGIyLWFiNjAtNDc4MC1hMjE4LTg1Zjg0Mjg3OTE4OSIsInVjYyI6IlpET0tOIiwibmFwIjoiIiwieWNlIjoiZVlcXDYgJyM1XCJwXHUwMDAwXGZcdTAwMDd0XHUwMDAwXHUwMDEwYiIsImZldGNoY2FjaGluZ3J1bGUiOjAsImNhdGVnb3Jpc2F0aW9uIjoiIn0.N66Zw6e9CVLrPp7ktrM8fO6MdZ5buIGoOjh2bmGH1b77VEMeUpK6vL3Jw-pWZ0AUbMS-xXy3-Wun5i3Esfy3t573jTBtKG6L2Y1XmLpZlVLK-tYhhY2ca1w2mkT_oy66FuUlgH7rAk3Sjp6813-6CjxOixIRmivErhqW3q20ny77JDpGu_axHYU_mCO2_vP2jnzCnJDNUIhKMeGKEH7agywPxSRy8-cvrhIFJKrmJBRlHOl5c6RGBkdXarbdS5nEXvdcMJA-quXK2UfomGEuLRDkvBw1gHMSsr1EHvJyaFtrIZ0DZNQxBbvVxGhvU2tNlpipHLoR1-cU_YPHOEPfcw");
                    //request.Headers.Add("neo-fin-key", "neotradeapi");
                    //request.Headers.Add("Authorization", "Bearer eyJ4NXQiOiJNbUprWWpVMlpETmpNelpqTURBM05UZ3pObUUxTm1NNU1qTXpNR1kyWm1OaFpHUTFNakE1TmciLCJraWQiOiJaalJqTUdRek9URmhPV1EwTm1WallXWTNZemRtWkdOa1pUUmpaVEUxTlRnMFkyWTBZVEUyTlRCaVlURTRNak5tWkRVeE5qZ3pPVGM0TWpGbFkyWXpOUV9SUzI1NiIsImFsZyI6IlJTMjU2In0.eyJzdWIiOiJjbGllbnQxMTg4IiwiYXV0IjoiQVBQTElDQVRJT04iLCJhdWQiOiJQekIyZTh4eFhKelBnSHVqckZraXNkZjZ6cklhIiwibmJmIjoxNzIxNDI0ODc5LCJhenAiOiJQekIyZTh4eFhKelBnSHVqckZraXNkZjZ6cklhIiwic2NvcGUiOiJkZWZhdWx0IiwiaXNzIjoiaHR0cHM6XC9cL25hcGkua290YWtzZWN1cml0aWVzLmNvbTo0NDNcL29hdXRoMlwvdG9rZW4iLCJleHAiOjE3MjE0NTM2NzksImlhdCI6MTcyMTQyNDg3OSwianRpIjoiMTMxYWQ5MWItM2QzYi00ZjBhLWIzYjktOWI1NjE1ZTg1MTQ4In0.DRwI6WtJe1-BUXO87rFZ5ihX-KnIUwHCp4unFFXSj0sRV_-Idtc_dGpFMjLXX8pHY5iyy3rMDDESny_4fzIzTCROBMEAP6uVYWuqhVTvUC2kU1rfCegPX6farMyvRm4ELq4hDENJp-384TEKd3hLsI_t6eX9p5lqY4Q179rRQsyLbCOZ9OF3yiMO2pH3vQpNlnNNBqH1PV-CM_wvtddo_SV0YXYnHf2Sz7OsvmAgp_cYixHeDLV_-DfiY-z8Eay09moNUR9pRAzFiyGQ3N-3MHQ6n0-OQDKSmwIT8qe_MEWKIEOEgRdZ-zhcikt1I_pXbxKxpPM21YGx9Auf3Mc3xw");
                    //var responseTask =  client.SendAsync(request);
                    //responseTask.Wait();
                    //var response =  responseTask.Result;
                    //response.EnsureSuccessStatusCode();
                    //Console.WriteLine(response.Content.ReadAsStringAsync());


                    //var client = new RestClient(httpClient);
                    //var request = new RestRequest(new Uri(url), httpMethod == "POST" ? Method.Post : httpMethod == "GET" ? Method.Get: httpMethod == "PUT"? Method.Put: Method.Delete);
                    ////request.AddHeader("Authorization", "Bearer eyJ4NXQiOiJNbUprWWpVMlpETmpNelpqTURBM05UZ3pObUUxTm1NNU1qTXpNR1kyWm1OaFpHUTFNakE1TmciLCJraWQiOiJaalJqTUdRek9URmhPV1EwTm1WallXWTNZemRtWkdOa1pUUmpaVEUxTlRnMFkyWTBZVEUyTlRCaVlURTRNak5tWkRVeE5qZ3pPVGM0TWpGbFkyWXpOUV9SUzI1NiIsImFsZyI6IlJTMjU2In0.eyJzdWIiOiJjbGllbnQxMTg4IiwiYXV0IjoiQVBQTElDQVRJT04iLCJhdWQiOiJQekIyZTh4eFhKelBnSHVqckZraXNkZjZ6cklhIiwibmJmIjoxNjcwMjI2MzU5LCJhenAiOiJQekIyZTh4eFhKelBnSHVqckZraXNkZjZ6cklhIiwic2NvcGUiOiJkZWZhdWx0IiwiaXNzIjoiaHR0cHM6XC9cL25hcGkua290YWtzZWN1cml0aWVzLmNvbTo0NDNcL29hdXRoMlwvdG9rZW4iLCJleHAiOjE2NzAyNTUxNTksImlhdCI6MTY3MDIyNjM1OSwianRpIjoiNTliYjIwMDUtOGNlYS00NjUyLThjOTEtNDFlMDUxMTczNTc4In0.AZTWXii5TNNy3qF0GAxEda9g-03oDWQwphyDFny3PRnhn5qwBIWxTfTFliuv-eu5WuZuWa-4-pZcaqHbbGqNhT1TIrVNvqr3d2W0KwyzY-FrzmmqFBud_tIiuAFigcBTc39-twMATnnCQkoe9PP690drHWZ-gQIHgRWESOX1yryG6d3ma-yQ8eAv8JXIT4dLQCRul3g9bPow84Hym3sBKVOpQETn12whTwsMF3JSIe9WtUqnenNZJi4BBEDSAF6vlYYbq7ZYlEKrv0wiC7590H7-GR6q9rE5U8Lf2SeXk536ZF2jQDVz7a9_P5AyFlXigsIGxRo77jSYweC5BRb_8g");
                    ////request.AddHeader("Sid", "65a7c3f8-ae21-484d-b2aa-05629c04276d");
                    ////request.AddHeader("Auth", "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzY29wZSI6WyJUcmFkZSJdLCJleHAiOjE2NzAyNjUwMDAsImp0aSI6IjdkMGQxZjg1LTJjNTUtNDNkOS05ZTk2LWU0Y2E1NjE1OTQwMyIsImlhdCI6MTY3MDIyNjU1NCwiaXNzIjoibG9naW4tc2VydmljZSIsInN1YiI6IjI2NDI5OGIyLWFiNjAtNDc4MC1hMjE4LTg1Zjg0Mjg3OTE4OSIsInVjYyI6IlpET0tOIiwicGFuIjoiQUxNUE0wMDUyQiIsImZldGNoY2FjaGluZ3J1bGUiOjAsImNhdGVnb3Jpc2F0aW9uIjoiIn0.ikpr1cbnL4ULWFxxS5i857dRM8mBBSf55W_6-6ekHiie3L75SbC97tK2-LjERss7LGdm_LlgSZSpqiIS-Fg-4-8nLix-OEuTSanet9ArTq86VxtUXsbGTeKM4lUbNTf4DIqpV3KMxmOyzWW39O4g14eAnVYXn4QM407gNXd7xBa-2KzGaEm7rwm7GOv4Vm8AFwRFFJaYe3Qm94Ss09kEQ4Iy70RBmyj5x4GsCBo8ePNuDdTQppySpbvrUblP-8hVIhmnQU5f1Jic1oBkv71oYBzMiBnVbMEYRl7wXQV7pm5R-nl-PYcgSWi2g6PRAilWe3a0Z84AGztArd7H5-q9jA");
                    ////request.AddHeader("neo-fin-key", "neotradeapi");
                    //request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                    //request.AddParameter("jData", "{\"am\":\"NO\", \"dq\":\"0\",\"es\":\"nse_fo\", \"mp\":\"0\", \"pc\":\"CNC\", \"pf\":\"N\", \"pr\":\"0\", \"pt\":\"MKT\", \"qt\":\"25\", \"rt\":\"DAY\", \"tp\":\"0\", \"ts\":\"BANKNIFTY22D0844000CE\", \"tt\":\"S\"}");
                    ////request.AddParameter("jData", "{\"am\":\"NO\", \"dq\":\"0\",\"es\":\"nse_cm\", \"mp\":\"0\", \"pc\":\"CNC\", \"pf\":\"N\", \"pr\":\"3000\", \"pt\":\"MKT\", \"qt\":\"1\", \"rt\":\"DAY\", \"tp\":\"0\", \"ts\":\"TCS-EQ\", \"tt\":\"B\"}");
                    //RestResponse response = client.Execute(request);
                    //Console.WriteLine(response.Content);




                    Task<HttpResponseMessage> httpResponsetask = httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);// PostAsync(url, dataJson);
                    httpResponsetask.Wait();
                    httpResponse = httpResponsetask.Result;
                    semaphore.Release();
                    //var httpResponseMessage = httpClient.SendAsync(httpRequest);
                }
                //catch (WebException e)
                //{
                //    if (e.Response is null)
                //        throw e;

                //    webResponse = (HttpWebResponse)e.Response;
                //}
                catch (Exception ex)
                {
                    Logger.LogWrite(ex.Message);

                    throw ex;
                }

                //Logger.LogWrite(webResponse.StatusCode.ToString());

                HttpStatusCode statusCode = httpResponse.StatusCode;

                if (httpResponse.IsSuccessStatusCode)
                {
                    Task<string> contentStreamTask = httpResponse.Content.ReadAsStringAsync();
                    contentStreamTask.Wait();
                    string contentStream = contentStreamTask.Result;
                    responseDictionary = Utils.JsonDeserialize(contentStream);
                    httpResponse.Dispose();
                    break;
                    //return responseDictionary;
                }
                else if ((statusCode == HttpStatusCode.TooManyRequests || statusCode == HttpStatusCode.ServiceUnavailable || statusCode == HttpStatusCode.InternalServerError) && (counter < 4))
                {
                    httpResponse.Dispose();
                    Thread.Sleep(1);
                    continue;
                }
                else
                {
                    httpResponse.Dispose();
                    throw new Exception(statusCode.ToString());
                }

            }
                return responseDictionary;



                //using (Stream webStream = webResponse.GetResponseStream())
                //{
                //    using (StreamReader responseReader = new StreamReader(webStream))
                //    {
                //        string response = responseReader.ReadToEnd();
                //        HttpStatusCode status = ((HttpWebResponse)webResponse).StatusCode;

                //        if (webResponse.ContentType == "application/json")
                //        {
                //            Dictionary<string, dynamic> responseDictionary = Utils.JsonDeserialize(response);

                //            //                        ### HTTP response details
                //            //                      | Status code | Description | Response headers |
                //            //                      | -------------| -------------| ------------------|
                //            //                      **200 * * | Order placed successfully | -  |
                //            //                       **400 * * | Invalid or missing input parameters | -  |
                //            //                      **403 * * | Invalid session, please re-login to continue | -  |
                //            //                      **429 * * | Too many requests to the API | -  |
                //            //                         **500 * * | Unexpected error | -  |
                //            //                       **502 * * | Not able to communicate with OMS | -  |
                //            //                         **503 * * | Trade API service is unavailable | -  |
                //            //                    **504 * * | Gateway timeout, trade API is unreachable | -  |
                           

                //            if ((status == HttpStatusCode.TooManyRequests || status == HttpStatusCode.ServiceUnavailable || status == HttpStatusCode.InternalServerError) && (counter < 4))
                //            {
                //                Thread.Sleep(1);
                //                continue;
                //            }
                //            else if (status != HttpStatusCode.OK)
                //            {
                //                throw new Exception(webResponse.StatusCode.ToString());
                //            }
                //            webResponse.Close();
                //            return responseDictionary;
                //        }
                //        else if (webResponse.ContentType == "text/csv")
                //            return Utils.ParseCSV(response);
                //        else
                //            throw new DataException("Unexpected content type " + webResponse.ContentType + " " + response);
                //    }
                //}
            //}
        }

        #endregion

        /// <summary>
        /// Creates key=value with url encoded value
        /// </summary>
        /// <param name="Key">Key</param>
        /// <param name="Value">Value</param>
        /// <returns>Combined string</returns>
        public string BuildParam(string Key, dynamic Value)
        {
            if (Value is string)
            {
                //return "\"" + HttpUtility.UrlEncode(Key) + "\"" + ":" + "\"" + HttpUtility.UrlEncode((string)Value) + "\"";
                return "\"" + Key + "\"" + ":" + "\"" + (string)Value + "\"";
            }
            else
            {
                //return "\"" + Key + "\"" + ":" +  Value ;
                if(Value.GetType() == typeof(bool))
                {
                    return "\"" + Key + "\"" + ":" + Convert.ToString(Value).ToLower();
                }
                return "\"" + Key + "\"" + ":" + Convert.ToString(Value);
                //string[] values = (string[])Value;
                //return String.Join(",", values.Select(x => "\"" + HttpUtility.UrlEncode(Key) + "\"" + ":" + "\"" + HttpUtility.UrlEncode(x)) + "\"");
            }
        }

    }
    public class KotakLoginParam
    {
        public string userid { get; set; }
        public string pwd { get; set; }
        public string otp { get; set; }
        public string accessToken { get; set; }
    }
    public class KotakLoginResponse
    {
        public string userName { get; set; }
        public string message { get; set; }
        public int statusCode { get; set; }
    }
    //public class KotakPostOrderService
    //{
    //    private readonly HttpClient _httpClient;
    //    private string _accessToken;
    //    private string _sessionToken;

    //    public KotakPostOrderService(HttpClient httpClient)
    //    {
    //        _httpClient = httpClient;
    //        _httpClient.BaseAddress = new Uri("https://tradeapi.kotaksecurities.com/apim/orders/1.0/order/mis");

    //        _httpClient.DefaultRequestHeaders.Add("accept", "application/json");
    //        _httpClient.DefaultRequestHeaders.Add("consumerKey", "tpayXGqA2my6N8OXRdWYo1ynvoka");
    //        _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + ZObjects.kotak.KotakAccessToken);
    //        _httpClient.DefaultRequestHeaders.Add("sessionToken", ZObjects.kotak.UserSessionToken);
    //        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    //    }
    //}
    //public class KotakGetOrderService
    //{
    //    private readonly HttpClient _httpClient;
    //    private string _accessToken;
    //    private string _sessionToken;

    //    public KotakGetOrderService(HttpClient httpClient)
    //    {
    //        _httpClient = httpClient;
    //        _httpClient.BaseAddress = new Uri("https://tradeapi.kotaksecurities.com/apim/orders/");

    //        _httpClient.DefaultRequestHeaders.Add("accept", "application/json");
    //        _httpClient.DefaultRequestHeaders.Add("consumerKey", "tpayXGqA2my6N8OXRdWYo1ynvoka");
    //        _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _accessToken);
    //        _httpClient.DefaultRequestHeaders.Add("sessionToken", _sessionToken);
    //        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    //    }
    //}
}
