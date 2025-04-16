using Algorithms.Utilities;
using Algos.Utilities.Views;
using BrokerConnectWrapper;
using DataAccess;
using FyersConnect;
using GlobalLayer;
//using KafkaFacade;
using KiteConnect;
using LocalDBData.Test;
using Newtonsoft.Json.Linq;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

using System.Net.Http;
using System.IO;
using System.Threading;
using System.Text.Json;
using Newtonsoft.Json;
using System.Net.Http.Json;
using static Google.Apis.Requests.BatchRequest;
using Algorithms.Algorithms;


namespace LocalDBData
{
    public class CryptoTickDataStreamer// : IObservable<Tick>
    {

        DateTime? prevTickTime, currentTickTime;
        uint counter = 0;
        //public static Dictionary<UInt32, Queue<Tick>> LiveTickData;
        //public static List<Tick> LiveTicks;
        SortedList<DateTime, Tick> LiveTicks = new SortedList<DateTime, Tick>();
        SortedList<DateTime, Tick> LocalTicks = new SortedList<DateTime, Tick>();
        private static readonly HttpClient client = new HttpClient();
        //DataAccess.QuestDB qsDB = new QuestDB();
        List<Tick> ticks;

        //Kafka
        //Dictionary<uint, TopicPartition> instrumentPartitions = new Dictionary<uint, TopicPartition>();

        int partitionCount = 0;

        public CryptoTickDataStreamer()
        {
            //Subscriber list
            //observers = new List<IObserver<Tick[]>>();
        }



        public async Task<long> BeginStreaming()
        {
            try
            {
                LoadDataFromDatabase();
                //RetrieveHistoricalTicksFromKite();
                //RetrieveHistoricalTicksFromFyers();
                //LoadHistoricalDataFromKiteToDatabase();
                //LoadHistoricalDataFromFyersToDatabase();
                return 1;
            }
            catch (Exception ex)
            {
                return -1;
            }
        }
        public async Task<long> LoadDataFromDatabase()
        {
            DEConnect.Login();

            DateTime fromDate = Convert.ToDateTime("2025-01-01");
            DateTime toDate = Convert.ToDateTime("2025-01-31");

            //BNF
            decimal fromStrike = 74000;
            decimal toStrike = 116000;
            uint btoken = 260105; //257801;

            bool futuresData = true;
            bool optionsData = true;



            //InitialRangeBreakoutTest testAlgo = new InitialRangeBreakoutTest();
            //PAWithLevelsTest testAlgo = new PAWithLevelsTest();
            //StockMomentumTest testAlgo = new StockMomentumTest();
            //CandleWickScalpingOptionsTest testAlgo = new CandleWickScalpingOptionsTest();
            //CandleWickScalpingTest testAlgo = new CandleWickScalpingTest();

            //ActiveBuyStrangleWithVariableQtyTest testAlgo = new ActiveBuyStrangleWithVariableQtyTest();
            //OptionBuyOnEMACrossTimeVolumeTest testAlgo = new OptionBuyOnEMACrossTimeVolumeTest();
            //StraddleWithEachLegSLTest testAlgo = new StraddleWithEachLegSLTest();
            //EMAScalpingKBTest testAlgo = new EMAScalpingKBTest();
            //MultipleEMAPriceActionTest testAlgo = new MultipleEMAPriceActionTest();
            //ManageStrangleDeltaTest testAlgo = new ManageStrangleDeltaTest();
            //StopAndReverseTest testAlgo = new StopAndReverseTest();
            //TJ4Test testAlgo = new TJ4Test();
            //OptionSellonHTTest testAlgo = new OptionSellonHTTest();
            //MultiTimeFrameSellOnHTTest testAlgo = new MultiTimeFrameSellOnHTTest();
            //TJ3Test testAlgo = new TJ3Test();
            //TJ5Test testAlgo = new TJ5Test();
            //BCTest testAlgo = new BCTest();

            //Latest
            //RangeBreakoutCandleTest testAlgo = new RangeBreakoutCandleTest();
            //StockTrendTest testAlgo = new StockTrendTest();

            //OptionSellOnEMATest testAlgo = new OptionSellOnEMATest();
            //PriceVolumeRateOfChangeTest testAlgo = new PriceVolumeRateOfChangeTest();
            //AlertTest testAlgo = new AlertTest();

            //CryptoActiveBuyStrangleWithVariableQtyTest testAlgo = new CryptoActiveBuyStrangleWithVariableQtyTest();

            //PriceDirectionalFutureOptionsTest testAlgo = new PriceDirectionalFutureOptionsTest();

            CryptoRBCTest testAlgo = new CryptoRBCTest();

            //DirectionalWithStraddleShiftTest testAlgo = new DirectionalWithStraddleShiftTest();
            //CalendarSpreadValueScalpingTest testAlgo = new CalendarSpreadValueScalpingTest();
            //ExpiryTradeTest testAlgo = new ExpiryTradeTest();
            //StraddleExpiryTradeTest testAlgo = new StraddleExpiryTradeTest();
            //ExpiryTradeICTest testAlgo = new ExpiryTradeICTest();
            //ManageStrangleWithLevelsTest testAlgo = new ManageStrangleWithLevelsTest();

            DateTime currentDate = fromDate;
            //while (currentDate.Date <= toDate.Date)
            //


                // PriceActionInput inputs = new PriceActionInput() { BToken = Convert.ToUInt32(btoken), CTF = 1, CurrentDate = currentDate, Qty = 1 };
                //OptionBuyOnEMACrossInputs inputs = new OptionBuyOnEMACrossInputs()
                //{
                //    BToken = btoken,
                //    CTF = 5,
                //    LEMA = 13,
                //    Qty = 1,
                //    SEMA = 5,
                //    SL = 20,
                //    TP = 50
                //};

                //StraddleInput inputs = new StraddleInput()
                //{
                //    BToken = btoken,
                //    Qty = 1,
                //    CTF = 15,
                //    Expiry = expiry,
                //    TP = 50,
                //    SL = 2000,
                //    UID = "PM27031981",
                //    SPI = 25,
                //    IntraDay = true,
                //    TR = 2,
                //    SS = true
                //};
                //StrangleWithDeltaandLevelInputs inputs = new StrangleWithDeltaandLevelInputs()
                //{
                //    BToken = Convert.ToUInt32(btoken),
                //    CTF = 15,
                //    Expiry = expiry,
                //    CurrentDate = currentDate,
                //    IDelta = 0.17m,
                //    IQty = 2,
                //    MaxDelta = 0.27m,
                //    MinDelta = 0.10m,
                //    MaxQty = 4,
                //    StepQty = 1,
                //    L1 = 44500,
                //    L2 = 44100,
                //    U1 = 45100,
                //    U2 = 45600,
                //    TP = 100000,
                //    SL = 5000,
                //    UID = "PM27031981",
                //    IntraDay = false
                //};

                //OptionIVSpreadInput inputs = new OptionIVSpreadInput()
                //{
                //    BToken = Convert.ToUInt32(btoken),
                //    OpenVar = 10,
                //    CloseVar = 10,
                //    Expiry1 = expiry,
                //    Expiry2 = expiry2,
                //    StepQty = 1,
                //    MaxQty = 10,
                //    Straddle = false,
                //    TO = 30
                //};


                //OptionTradeInputs inputs = new OptionTradeInputs()
                //{
                //    BToken = 27,
                //    Expiry = currentDate.AddDays(1),
                //    IQty = 20,
                //    SQty = 10,
                //    MQty = 50,
                //    uid = "PMDEUID",
                //    TP = 10000,
                //    SL = 2000,
                //};


                //10 lot size 

                //OptionExpiryStrangleInput inputs = new OptionExpiryStrangleInput()
                //{
                //    BToken = Convert.ToUInt32(btoken),
                //    Expiry = expiry,
                //    IDFBI = 400,
                //    IQty = 4,
                //    MDFBI = 200,
                //    MPTT = 7,
                //    MQty = 10,
                //    SL = 30000,
                //    SQty = 2,
                //    TP = 600000,
                //    UID = "PM27031981",
                //    SPI = 100

                //};

                //PriceActionInput inputs = new PriceActionInput()
                //{
                //    BToken = Convert.ToUInt32(btoken),
                //    TP = 81,
                //    SL = 51,
                //    CTF = 5,
                //    CurrentDate = currentDate,
                //    Qty = 20,
                //    Expiry = currentDate.AddDays(1),
                //    UID = "PM27031981"
                //};
                CryptoPriceActionInputs inputs = new CryptoPriceActionInputs()
                {
                    BSymbol = "BTCUSD",
                    TP = 81,
                    SL = 51,
                    Qty = 20,
                    Expiry = currentDate.AddDays(1),
                    uid = "PMDEUID"
                };
#if !LOCAL

                testAlgo.Execute(inputs);
                //testAlgo.Execute();
                testAlgo.StopTrade(false);


#endif

                //Task<long> allDone = RetrieveTicksFromDB(btoken, expiry, expiry2, fromStrike, toStrike, futuresData, 
                //    optionsData, currentDate, testAlgo, sqlConnection);

                //Below is Original calling method.
                //Task<long> allDone = RetrieveTicksFromDB(btoken, expiry, fromStrike, toStrike, futuresData,
                //   optionsData, currentDate, testAlgo, sqlConnection);
                
                //Below method is for alerts only
                Task allDone = RetrieveCandleSticksFromDB(inputs.BSymbol, inputs.Expiry, fromStrike, toStrike, 3, fromDate, toDate, testAlgo);
                allDone.Wait();

                //currentDate = currentDate.AddDays(1);
            //}
            return 1;
        }
        public class Column
        {
            public string Name { get; set; }
            public string Type { get; set; }
        }

        public class DataResponse
        {
            public string Query { get; set; }
            public List<Column> Columns { get; set; }
            public long Timestamp { get; set; }
            public List<List<object>> Dataset { get; set; }
        }

        private async Task RetrieveCandleSticksFromDB(string bsymbols, DateTime expiry, decimal fromStrike, decimal toStrike, int candleTimeFrameinMins,
            DateTime fromDate, DateTime toDate, ICryptoTest testAlgo)
        {
            string startingDateTime = fromDate.Date.AddHours(2).ToString("yyyy-MM-dd HH:mm:ss");// "2024-03-10T00:00:00Z";
            string endingDateTime = toDate.AddDays(31).AddHours(17).ToString("yyyy-MM-dd HH:mm:ss");// "2024-03-10T12:00:00Z";
            string[] productSymbols = { "BTCUSD" };//GetSymbols(expiry, fromStrike, toStrike).ToArray();// { "BTCUSD", "ETHUSD", "XRPUSD" }; // Example products



            // Convert array to SQL IN ('BTCUSD', 'ETHUSD', 'XRPUSD')
            string productSymbolsFilter = string.Join(",", productSymbols.Select(s => $"'{s}'"));

            string query = $@"
                  WITH intervals AS (
                    SELECT 
                      timestamp_floor('{candleTimeFrameinMins}m', timestamp) AS interval_start,
                      product_symbol,
                      first(price) as open_price,
                      max(price) as high_price,
                      min(price) as low_price,
                      last(price) as close_price
                    FROM HistoricalData
                    WHERE product_symbol in ({productSymbolsFilter})
                    AND timestamp BETWEEN '{startingDateTime}' AND '{endingDateTime}' --dateadd('m', -45, now())
                    GROUP BY timestamp_floor('{candleTimeFrameinMins}m', timestamp), product_symbol
                  )
                  SELECT * FROM intervals
                  ORDER BY interval_start ASC;
            ";

            string url = "http://localhost:9000/exec?query=" + Uri.EscapeDataString(query);
            string username = "admin";  // Change to your QuestDB username
            string password = "quest";  // Change to your QuestDB password

            try
            {
                using (HttpClient client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan }) // No timeout for streaming
                {
                    // Encode username and password for Basic Auth
                    string auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

                    using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        string result = await response.Content.ReadAsStringAsync();

                        DataResponse dataResponse = JsonConvert.DeserializeObject<DataResponse>(result);

                        // Convert dataset into list of OrderBookEntry objects
                        List<CandleStick> orderBookEntries = new List<CandleStick>();
                        foreach (var row in dataResponse.Dataset)
                        {
                            CandleStick entry = new CandleStick
                            {
                                Symbol = row[1].ToString(),
                                OpenPrice = Convert.ToDecimal(row[2]),
                                HighPrice = Convert.ToDecimal(row[3]),
                                LowPrice = Convert.ToDecimal(row[4]),
                                ClosePrice = Convert.ToDecimal(row[5]),
                                Timestamp = DateTimeOffset.Parse(row[0].ToString()).ToUnixTimeMilliseconds() * 1000,
                                StartTime = DateTimeOffset.Parse(row[0].ToString()).ToUnixTimeMilliseconds() * 1000,
                                State = CandleStates.Finished
                            };

                            orderBookEntries.Add(entry);
                        }
                        foreach (var entry in orderBookEntries)
                        {
                            ;
                            testAlgo.OnNext("candleStick", System.Text.Json.JsonSerializer.Serialize(entry));// JsonConvert.SerializeObject(entry));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private async Task RetrieveTicksFromDB(uint btoken, DateTime expiry, decimal fromStrike, decimal toStrike,
            DateTime currentDate, ICryptoTest testAlgo)
        {
            string startingDateTime = currentDate.Date.AddHours(2) .ToString("yyyy-MM-dd HH:mm:ss");// "2024-03-10T00:00:00Z";
            string endingDateTime = currentDate.AddDays(1).AddHours(17).ToString("yyyy-MM-dd HH:mm:ss");// "2024-03-10T12:00:00Z";
            string[] productSymbols = GetSymbols(expiry, fromStrike, toStrike).ToArray();// { "BTCUSD", "ETHUSD", "XRPUSD" }; // Example products



            // Convert array to SQL IN ('BTCUSD', 'ETHUSD', 'XRPUSD')
            string productSymbolsFilter = string.Join(",", productSymbols.Select(s => $"'{s}'"));

            string query = $@"
                SELECT product_symbol as symbol, price as best_ask, price as best_bid, size as ask_qty, size as bid_qty, 'l1_orderbook' as ""type"", timestamp
                FROM HistoricalData Where product_symbol in ({productSymbolsFilter}) AND timestamp BETWEEN '{startingDateTime}' AND '{endingDateTime}'
                ORDER BY timestamp
            ";

            string url = "http://localhost:9000/exec?query=" + Uri.EscapeDataString(query);
            string username = "admin";  // Change to your QuestDB username
            string password = "quest";  // Change to your QuestDB password

            try
            {
                using (HttpClient client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan }) // No timeout for streaming
                {
                    // Encode username and password for Basic Auth
                    string auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

                    using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        string result = await response.Content.ReadAsStringAsync();

                        DataResponse dataResponse = JsonConvert.DeserializeObject<DataResponse>(result);

                        // Convert dataset into list of OrderBookEntry objects
                        List<L1Orderbook> orderBookEntries = new List<L1Orderbook>();
                        foreach (var row in dataResponse.Dataset)
                        {
                            L1Orderbook entry = new L1Orderbook
                            {
                                Symbol = row[0].ToString(),
                                BestAsk = Convert.ToString(row[1]),
                                BestBid = Convert.ToString(row[2]),
                                AskQuantity = row[3].ToString(),
                                BidQuantity = row[4].ToString(),
                                Type = row[5].ToString(),
                                Timestamp = DateTimeOffset.Parse(row[6].ToString()).ToUnixTimeMilliseconds()*1000,
                            };

                            orderBookEntries.Add(entry);
                        }
                        foreach (var entry in orderBookEntries)
                        {
                            testAlgo.OnNext("l1_orderbook", JsonConvert.SerializeObject(entry));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private List<string> GetSymbols(DateTime nextExpiry,decimal fromStrike, decimal toStrike)
        {
            Dictionary<string, List<string>> channelAndSymbolsTobeSubscribed = new Dictionary<string, List<string>>();
            List<string> symbolsTobeSubscribed = new List<string>();
            symbolsTobeSubscribed.Add("BTCUSD");
            decimal strikePrice = fromStrike;

            while (strikePrice <= toStrike)
            {
                symbolsTobeSubscribed.Add($"C-BTC-{strikePrice}-{nextExpiry.ToString("ddMMyy")}");
                symbolsTobeSubscribed.Add($"P-BTC-{strikePrice}-{nextExpiry.ToString("ddMMyy")}");

                strikePrice = strikePrice + 200;
            }

            return symbolsTobeSubscribed;
        }
        
        /// <summary>
       

        public double ConvertToUnixTimestamp(DateTime? date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date.Value.ToUniversalTime() - origin;
            return Math.Floor(diff.TotalSeconds);
        }
     
    }

}
