using GlobalLayer;
using System;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text.Json;
using NetMQ;
using System.Net;
using System.Threading;
using Google.Protobuf.WellKnownTypes;
using QuestDB;
using System.IO;
using System.Reactive;
using System.Net.Sockets;

namespace DeltaExchangeConnect
{
    public class DeltaExchange
    {
        private string _baseUrl = "https://api.india.delta.exchange";
        //Testnet(Demo Account) - https://cdn-ind.testnet.deltaex.org
        private string _apiKey;
        private string _apiSecret;
        private bool _enableLogging;
        private int _timeout;
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
        public DeltaExchange(string APIKey, string APISecret = null, string Root = null, bool Debug = false, int Timeout = 7000)
        {
            _apiSecret = APISecret;
            _apiKey = APIKey;
            if (!String.IsNullOrEmpty(Root)) this._baseUrl = Root;
            _enableLogging = Debug;

            _timeout = Timeout;
        }
        private string GenerateSignature(string secret, string message)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
        public List<Asset> GetAllAssets(HttpClient httpClient = null)
        {
            string url = $"{_baseUrl}/v2/assets";

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var responseTask = httpClient.GetAsync(url);
            responseTask.Wait();
            var response = responseTask.Result;

            var responseBodyTask = response.Content.ReadAsStringAsync();
            responseBodyTask.Wait();

            string responseBody = responseBodyTask.Result;

            var apiResponse = ApiResponse<List<Asset>>.FromJson(responseBody);

            return apiResponse.Result;
        }

        public List<IndexData> GetAllIndices(HttpClient httpClient = null)
        {
            string url = $"{_baseUrl}/v2/indices";

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var responseTask = httpClient.GetAsync(url);
            responseTask.Wait();
            var response = responseTask.Result;

            var responseBodyTask = response.Content.ReadAsStringAsync();
            responseBodyTask.Wait();
            string responseBody = responseBodyTask.Result;

            var apiResponse = ApiResponse<List<IndexData>>.FromJson(responseBody);

            return apiResponse.Result;
        }
        public List<Product> GetAllProducts(HttpClient httpClient = null)
        {
            string url = $"{_baseUrl}/v2/products";

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var responseTask = httpClient.GetAsync(url);
            responseTask.Wait();
            var response = responseTask.Result;

            var responseBodyTask = response.Content.ReadAsStringAsync();
            responseBodyTask.Wait();
            string responseBody = responseBodyTask.Result;

            var apiResponse = ApiResponse<List<Product>>.FromJson(responseBody);

            return apiResponse.Result;
        }

        public Product GetProductBySymbolAsync(string symbol, HttpClient client = null)
        {
            string url = $"{_baseUrl}/v2/products/{symbol}";
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
           
            var responseTask = client.GetAsync(url);
            responseTask.Wait();
            var response = responseTask.Result;

            var responseBodyTask = response.Content.ReadAsStringAsync();
            responseBodyTask.Wait();
            string responseBody = responseBodyTask.Result;

            var apiResponse = ApiResponse<Product>.FromJson(responseBody);

            return apiResponse.Result;
        }
        public async Task<CryptoPosition> GetCurrentPosition(int? product_id, string? underlying_asset_symbol, HttpClient client = null)
        {
            string url = $"{_baseUrl}/v2/positions";

            string method = "GET";
            
            string path = "/v2/positions";

            string queryString="?";
            if (product_id != null)
            {
                queryString += $"product_id={product_id}";
            }
            if (underlying_asset_symbol != null)
            {
                queryString += $"underlying_asset_symbol={underlying_asset_symbol}";
            }
            string payload = "";
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string signatureData = method + timestamp + path + queryString + payload;
            string signature = GenerateSignature(_apiSecret, signatureData);

            
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("api-key", _apiKey);
            client.DefaultRequestHeaders.Add("signature", signature);
            client.DefaultRequestHeaders.Add("timestamp", timestamp);

            var queryParams = System.Web.HttpUtility.ParseQueryString(string.Empty);
            if (product_id.HasValue)
                queryParams["product_id"] = product_id.Value.ToString();
            if (!string.IsNullOrEmpty(underlying_asset_symbol))
                queryParams["underlying_asset_symbol"] = underlying_asset_symbol;

            string requestUrl = queryParams.Count > 0 ? $"{url}?{queryParams}" : url;

            var responseTask = client.GetAsync(requestUrl);
            responseTask.Wait();
            
            HttpResponseMessage response = responseTask.Result;
            response.EnsureSuccessStatusCode();


            string responseBody = await response.Content.ReadAsStringAsync();

            var apiResponse = ApiResponse<CryptoPosition>.FromJson(responseBody);

            return apiResponse.Result;
        }
        /// <summary>
        /// A comma-separated list of contract types to filter the tickers. 
        /// Example values include futures, perpetual_futures, call_options, put_options, 
        /// interest_rate_swaps, move_options, spreads, turbo_call_options, turbo_put_options, and spot.
        /// </summary>
        /// <param name="products"></param>
        /// <returns></returns>
        public List<ProductTicker> GetTickers(string products = "", HttpClient client = null)
        {
            string url = $"{_baseUrl}/v2/tickers/{products}";
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var responseTask = client.GetAsync(url);
            responseTask.Wait();
            var response = responseTask.Result;

            var responseBodyTask = response.Content.ReadAsStringAsync();
            responseBodyTask.Wait();
            string responseBody = responseBodyTask.Result;

            var apiResponse = ApiResponse<List<ProductTicker>>.FromJson(responseBody);

            return apiResponse.Result;
        }
        /// <summary>
        /// The symbol of the product for which the ticker data is requested (e.g., BTCUSD, ETHUSD).
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public List<ProductTicker> GetTickersForProduct(string symbol, HttpClient client = null)
        {
            string url = $"{_baseUrl}/v2/tickers/{symbol}";
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            var responseTask = client.GetAsync(url);
            responseTask.Wait();
            var response = responseTask.Result;

            var responseBodyTask = response.Content.ReadAsStringAsync();
            responseBodyTask.Wait();
            string responseBody = responseBodyTask.Result;

            var apiResponse = ApiResponse<List<ProductTicker>>.FromJson(responseBody);
            
            return apiResponse.Result;
        }

        private async Task GetOpenOrders()
        {
            string method = "GET";
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string path = "/v2/orders";
            string queryString = "?product_id=1&state=open";
            string payload = "";
            string signatureData = method + timestamp + path + queryString + payload;
            string signature = GenerateSignature(_apiSecret, signatureData);

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("api-key", _apiKey);
                client.DefaultRequestHeaders.Add("timestamp", timestamp);
                client.DefaultRequestHeaders.Add("signature", signature);
                client.DefaultRequestHeaders.Add("User-Agent", "csharp-rest-client");
                client.DefaultRequestHeaders.Add("Content-Type", "application/json");

                HttpResponseMessage response = await client.GetAsync(_baseUrl + path + queryString);
                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Open Orders: " + responseContent);
            }
        }


        /*
         var orderRequest = new CreateOrderRequest
        {
            ProductId = 27,
            ProductSymbol = "BTCUSD",
            LimitPrice = "59000",
            Size = 10,
            Side = "buy",
            OrderType = "limit_order",
            StopOrderType = "stop_loss_order",
            StopPrice = "56000",
            TrailAmount = "50",
            StopTriggerMethod = "last_traded_price",
            BracketStopLossLimitPrice = "57000",
            BracketStopLossPrice = "56000",
            BracketTrailAmount = "50",
            BracketTakeProfitLimitPrice = "62000",
            BracketTakeProfitPrice = "61000",
            TimeInForce = "gtc",
            Mmp = "disabled",
            PostOnly = false,
            ReduceOnly = false,
            ClientOrderId = "34521712",
            CancelOrdersAccepted = false
        };
        
        string responseString = await SendPostRequest(orderRequest);
        Console.WriteLine(responseString);
         

        side	buy
        side	sell
        order_type	limit_order
        order_type	market_order
        stop_order_type	stop_loss_order
        reduce_only	false
        reduce_only	true
        state	open
        state	pending
        state	closed
        state	cancelled
         */

        public CryptoOrder PlaceNewOrder(CreateOrderRequest orderRequest, HttpClient httpClient = null)
        {
            string method = "POST";
            
            string path = "/v2/orders";
            string queryString = "";


            //orderRequest = new CreateOrderRequest()
            //{
            //    OrderType = Constants.DELTAEXCHANGE_ORDER_TYPE_LIMIT,
            //    Size = 3,
            //    Side = "buy",
            //    LimitPrice = "0.0005",
            //    ProductId = 16
            //};

            string jsonContent = orderRequest.ToJson();
            var requestContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            string timestamp =  DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            StringBuilder sb = new StringBuilder();
            sb.Append(method);
            sb.Append(timestamp);
            sb.Append(path);
            sb.Append(queryString);
            sb.Append(jsonContent);

            string signatureData = sb.ToString();

            string signature = GenerateSignature(_apiSecret, signatureData);

            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
            httpClient.DefaultRequestHeaders.Add("timestamp", timestamp);
            httpClient.DefaultRequestHeaders.Add("signature", signature);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "csharp-rest-client");
            //httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");

            var responseTask =  httpClient.PostAsync(_baseUrl + path, requestContent);
            responseTask.Wait();
            var response = responseTask.Result;
            response.EnsureSuccessStatusCode();

            var responseBodyTask = response.Content.ReadAsStringAsync();
            responseBodyTask.Wait();

            string responseBody = responseBodyTask.Result;

            
            var apiResponse = ApiResponse<CryptoOrder>.FromJson(responseBody);
            
            return apiResponse.Result;
        }

        //public static string GetNetworkTimeUnix()
        //{
        //    const string ntpServer = "pool.ntp.org";
        //    var ntpData = new byte[48];

        //    ntpData[0] = 0x1B; // Set protocol version

        //    var addresses = Dns.GetHostEntry(ntpServer).AddressList;
        //    var ipEndPoint = new IPEndPoint(addresses[0], 123);
        //    using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
        //    {
        //        socket.Connect(ipEndPoint);
        //        socket.Send(ntpData);
        //        socket.Receive(ntpData);
        //        socket.Close();
        //    }

        //    // Extract the seconds and fractions
        //    ulong intPart = BitConverter.ToUInt32(ntpData, 40);
        //    ulong fracPart = BitConverter.ToUInt32(ntpData, 44);

        //    intPart = SwapEndianness(intPart);
        //    fracPart = SwapEndianness(fracPart);

        //    var milliseconds = (intPart * 1000) + ((fracPart * 1000) / 0x100000000L);
        //    var networkDateTime = (new DateTime(1900, 1, 1)).AddMilliseconds((long)milliseconds);

        //    // Return Unix time in seconds
        //    return ((DateTimeOffset)networkDateTime).ToUnixTimeSeconds().ToString();
        //}

        //static uint SwapEndianness(ulong x)
        //{
        //    return (uint)(((x & 0x000000ff) << 24) + ((x & 0x0000ff00) << 8) +
        //                  ((x & 0x00ff0000) >> 8) + ((x & 0xff000000) >> 24));
        //}
        public static string GetUnixTimeFromNtp()
        {
            // NTP Server address (you can change to a different one)
            const string ntpServer = "pool.ntp.org";
            var address = Dns.GetHostEntry(ntpServer);
            var endPoint = new IPEndPoint(address.AddressList[0], 123);

            var ntpData = new byte[48];
            ntpData[0] = 0x1B; // NTP request header byte

            // Send request and receive response
            using (var udpClient = new UdpClient())
            {
                udpClient.Send(ntpData, ntpData.Length, endPoint);
                ntpData = udpClient.Receive(ref endPoint);
            }

            // NTP timestamp is in the last 4 bytes (43-40)
            ulong intPart = (ulong)((ntpData[43] << 24) | (ntpData[42] << 16) | (ntpData[41] << 8) | ntpData[40]);
            ulong unixTimeStart = 2208988800UL; // NTP starts from Jan 1, 1900, Unix starts from Jan 1, 1970

            // Convert NTP to Unix time (subtract NTP epoch start time)
            ulong unixTime = intPart - unixTimeStart;
            return unixTime.ToString();
        }


        /*
         side	buy
        side	sell
        order_type	limit_order
        order_type	market_order
        time_in_force	gtc
        time_in_force	ioc
        mmp	disabled
        mmp	mmp1
        mmp	mmp2
        mmp	mmp3
        mmp	mmp4
        mmp	mmp5
        post_only	true
        post_only	false
         */
        public async Task<string> BatchCreateOrder(BatchCreateOrdersRequest batchOrderRequest)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("api-key", "****");
                client.DefaultRequestHeaders.Add("signature", "****");
                client.DefaultRequestHeaders.Add("timestamp", "****");

                string jsonContent = JsonSerializer.Serialize(batchOrderRequest);
                var requestContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync("https://api.india.delta.exchange/v2/batch_orders", requestContent);

                return await response.Content.ReadAsStringAsync();
            }
        }

        private async Task PlaceNewOrder(string orderType, string size, string transactionType, string limitPrice, string productID)
        {
            string method = "POST";
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string path = "/v2/orders";
            string queryString = "";
            //string payload = "{\"order_type\":\"limit_order\",\"size\":3,\"side\":\"buy\",\"limit_price\":\"0.0005\",\"product_id\":16}";
            //string payload = "{\"order_type\":\"limit_order\",\"size\":3,\"side\":\"buy\",\"limit_price\":\"0.0005\",\"product_id\":16}";
            //string signatureData = method + timestamp + path + queryString + payload;

            StringBuilder sb = new StringBuilder();
            StringBuilder pl = new StringBuilder();
            
            string pt = orderType == Constants.ORDER_TYPE_MARKET ? "market_order" : orderType == Constants.ORDER_TYPE_LIMIT ? "limit_order" : orderType == Constants.ORDER_TYPE_SL ? "stoploss_order" : "stoplossmarket_order";

            pl.Append("{");
            pl.AppendFormat("\"order_type\":\"{0}\", \"size\":{1},\"side\":\"{2}\", \"limit_price\":\"{3}\", \"product_id\":{4}",
                pt, size, transactionType, limitPrice, productID);
            sb.Append("}");

            string payload = pl.ToString();

            sb.Append(method);
            sb.Append(timestamp);
            sb.Append(path);
            sb.Append(queryString);
            sb.Append(payload);

            string signatureData = sb.ToString();

            string signature = GenerateSignature(_apiSecret, signatureData);

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("api-key", _apiKey);
                client.DefaultRequestHeaders.Add("timestamp", timestamp);
                client.DefaultRequestHeaders.Add("signature", signature);
                client.DefaultRequestHeaders.Add("User-Agent", "csharp-rest-client");
                client.DefaultRequestHeaders.Add("Content-Type", "application/json");

                HttpContent content = new StringContent(payload, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(_baseUrl + path, content);
                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine("New Order Response: " + responseContent);
            }
        }
    }
    
   
}