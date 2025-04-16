using DataAccess;
using FyersCSharpSDK;
using GlobalLayer;
using Google.Protobuf.WellKnownTypes;
using HyperSyncLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Net;
using System.Threading;
using ZMQFacade;


namespace FyersConnect
{
    public class Fy
    {
        private string _apiKey;
        private string? _accessToken;
        private FyersSocket _client;
        private string _clientID = "FODBVFKV77-100";
        private string _secretKey = "MXK9IYY7NH";
        private string _redirectURI = "https://127.0.0.1:7227/dataprovider";
        private FYToken? _fyToken;
        public ZMQServer zmqServer;//, zmqServer2;
        private Storage storage;
        Methods t;
        public Fy(string APIKey, string? AccessToken = null)
        {
            _accessToken = AccessToken;
            _apiKey = APIKey;
            _fyToken = null;
        }

        public class FYToken
        {
            public string? RESPONSE_MESSAGE;
            public string? refresh_token;
            public string? TOKEN;
        }

        public async void generateAuthCode(string clientID, string secretKey, string redirectURI)
        {
            FyersClass fyersModel = FyersClass.Instance;
            fyersModel.GetGenerateCode(clientID, secretKey, redirectURI);
        }
        public  async Task<FYToken> generateAccesToken(string clientID, string secretKey, string redirectURI, string auth_code)
        {
            FyersClass fyersModel = FyersClass.Instance;
            string appHashId = FyersCSharpSDK.Utility.GenerateAppHashID(clientID, secretKey);

            JObject AuthTokenJobject = await fyersModel.GenerateToken(secretKey, redirectURI, auth_code, appHashId);


            return AuthTokenJobject.ToObject<FYToken>();

            //Console.WriteLine(AuthTokenJobject);


            /*
                ----------------------------------------------------------------------------------
                Sample Success Response               
                ----------------------------------------------------------------------------------          

                {
                  "TOKEN": "eyJ0eXAiOiJKV1QiLCJh.YYYYYYYYYYYYYY.M2cDSjLBzhsbzown4nK",
                  "refresh_token": "eyJ0eXAiOiJKXXXXXXXXXXXXX.OeACApKiMkEEeXxa",
                  "RESPONSE_MESSAGE": "SUCCESS"
                }
             */
        }

        public async void SetMarketData(List<string> scripList, ChannelModes channelModes)
        {
            FyersClass fyersModel = FyersClass.Instance;
            fyersModel.ClientId = _clientID;
            fyersModel.AccessToken = _accessToken;
            t = new Methods();

            scripList = new List<string>();
            scripList.Add("NSE:BHARATFORG-EQ");
            scripList.Add("NSE:NIFTY50-INDEX");

            _client = new FyersSocket();
            // channelModel = ChannelModes.FULL or ChannelModes.LITE
            await t.DataWebSocket(scripList, channelModes, _client);
        }
        public class Methods : FyersSocketDelegate
        {
            public ZMQServer zmqServer;//, zmqServer2;
            public Storage storage;

            public async Task DataWebSocket(List<string> scripList, ChannelModes channelModes, FyersSocket client)
            {
                client.ReconnectAttemptsCount = 1;
                client.webSocketDelegate = this;

                await client.Connect();
                client.ConnectHSM(channelModes);

                //List<string> scripList = new List<string>();
                //scripList.Add("NSE:BHARATFORG-EQ");
                //scripList.Add("NSE:NIFTY50-INDEX");

                client.SubscribeData(scripList);
            }
            public async Task UnSubscribeInstruments(List<string> scripList, FyersSocket client)
            {
                //client.webSocketDelegate = this;

                //await client.Connect();
                //client.ConnectHSM(ChannelModes.FULL);

                //List<string> scripList = new List<string>();
                //scripList.Add("NSE:BHARATFORG-EQ");

                //client.SubscribeData(scripList);

                //await Task.Delay(2000);

                client.UnSubscribeData(scripList);
                await client.Close();
            }
            public void OnClose(string status)
            {
                Console.WriteLine("Connection is closed " + status);
            }

            public void OnOpen(string status)
            {
                Console.WriteLine("Connection is open: " + status);
            }
            public void OnOrder(JObject orders)
            {
                Console.WriteLine("OnOrder---------" + orders.ToString());
            }

            public void OnTrade(JObject trades)
            {
                Console.WriteLine("OnTrade---------" + trades.ToString());
            }

            public void OnPosition(JObject positions)
            {
                Console.WriteLine("OnPosition---------" + positions.ToString());
            }

            public void OnIndex(JObject index)
            {
                //Console.WriteLine("OnIndex---------" + index.ToString());
                Console.WriteLine("OnIndex---------" + index["data"]);
            }

            public void OnScrips(JObject scrips)
            {
                Console.WriteLine("OnScrips---------" + scrips["data"]);
            }

            public void OnDepth(JObject depths)
            {
                Console.WriteLine("OnDepth---------" + depths["data"]);
            }
            public void OnError(JObject error)
            {
                Console.WriteLine("OnError------: " + error);
            }

            public void OnMessage(JObject response)
            {
                Console.WriteLine("OnMessage------: " + response);
            }
            public void PublishData(JToken data )
            {
                bool shortenedTick = false;
                List<Tick> ticks = new List<Tick>() { new Tick(data) };// TickDataSchema.ParseTicks(Data, shortenedTick);

                zmqServer.PublishAllTicks(ticks, shortenedTick);

                //Below code is commented to improve efficiency but it can be open too
                //if (_storeCandles)
                //{
                //    storage.StoreTimeCandles(ticks);
                //}
                //if (_storeTicks)
                //{
                shortenedTick = true;
                storage.StoreTicks(ticks, shortenedTick);
            }
        }
        /// <summary>
        /// Set the `AccessToken` received after a successful authentication.
        /// </summary>
        /// <param name="AccessToken">Access token for the session.</param>
        public void SetAccessToken(string AccessToken)
        {
            this._accessToken = AccessToken;
        }
        public  FYToken Login()
        {
            //generateAuthCode(_clientID, _secretKey, _redirectURI);
            string auth_code = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJhcGkubG9naW4uZnllcnMuaW4iLCJpYXQiOjE3MzE0MTY3MjcsImV4cCI6MTczMTQ0NjcyNywibmJmIjoxNzMxNDE2MTI3LCJhdWQiOlsieDowIiwieDoxIiwieDoyIiwiZDoxIiwieDoxIiwieDowIl0sInN1YiI6ImF1dGhfY29kZSIsImRpc3BsYXlfbmFtZSI6IlhQMTUzODYiLCJvbXMiOiJLMSIsImhzbV9rZXkiOm51bGwsIm5vbmNlIjoiIiwiYXBwX2lkIjoiRk9EQlZGS1Y3NyIsInV1aWQiOiJhNGM2MzI4YTRkNjM0ODZhOGMwNDRkZGU2YTVmZDJmNCIsImlwQWRkciI6IjI0MDE6NDkwMDo4ODlhOjk0Mzk6YTFiZDphYTYzOmE2YzE6MjNiLCAxNjIuMTU4LjIzNS4zOSIsInNjb3BlIjoiIn0.SrAAeWoLw5CDbacUYeaTKActRo95gTf5U1tJPV03Jmg";
            var fyTokenTask = generateAccesToken(_clientID, _secretKey, _redirectURI, auth_code);

            fyTokenTask.Wait();
            return fyTokenTask.Result;
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
            string symbol,
            DateTime FromDate,
            DateTime ToDate,
            string Interval
        )
        {
            FyersClass fyersModel = FyersClass.Instance;
            fyersModel.ClientId = _clientID;
            fyersModel.AccessToken = _accessToken;

            StockHistoryModel model = new StockHistoryModel();
            model.Symbol = symbol;// "NSE:SBIN-EQ";
            model.DateFormat = "0";
            model.ContFlag = 1;
            //model.RangeFrom = FromDate.ToString("yyyy-MM-dd");// "2021-01-01";
            //model.RangeTo = ToDate.ToString("yyyy-MM-dd");// "2021-01-02";
            model.RangeFrom = Convert.ToInt64((FromDate - new DateTime(1970, 1, 1)).TotalSeconds).ToString(); // "2021-01-01";
            model.RangeTo = Convert.ToInt64((ToDate - new DateTime(1970, 1, 1)).TotalSeconds).ToString();// "2021-01-02";
            model.Resolution = Interval;// "30";

            var stockHistoryTask = GetStockHistory(fyersModel, model);
            stockHistoryTask.Wait();

            List<Historical> historicals = new List<Historical>();
            if (stockHistoryTask.Result != null)
            {
                foreach (var item in stockHistoryTask.Result)
                    historicals.Add(new Historical(item));
            }
            return historicals;
        }
        public async Task<JArray> GetStockHistory(FyersClass stocks, StockHistoryModel model)
        {
            Tuple<JArray, JObject> stockTuple = await stocks.GetStockHistory(model);
            
            //Console.WriteLine(JsonConvert.SerializeObject(stockTuple));
            // The response is of tuple and Item1 will be the data and Item2 will be the error message
            if (stockTuple.Item2 == null)
            {
               // Console.WriteLine("stocks: " + JsonConvert.SerializeObject(stockTuple.Item1));
            }
            else
            {
                Console.WriteLine("ERROR Message : " + stockTuple.Item2);
            }

            return stockTuple.Item1;
        }

    }
}
