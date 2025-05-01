using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeltaExchangeConnect;
using GlobalLayer;
using BrokerConnectWrapper;
using DataAccess;
using System.Timers;
using ZMQFacade;
using System.Runtime.CompilerServices;
using Timer = System.Timers.Timer;
//using KiteConnectTicker;
namespace CryptoDataService
{
   public class CryptoData
    {
        //public static readonly string NIFTY_TOKEN = "256265";
        //public static readonly string BANK_NIFTY_TOKEN = "260105";
        public static async Task PublishData()
        {
            //DEConnect.Login();


            ZObjects.deltaExchangeTicker = new DeltaExchangeTicker(APIKey: DEConnect.UserAPIkey, APISecret: DEConnect.UserAPISecret, null);//State:zSessionState.Current);

            ZObjects.deltaExchangeTicker.OnTick += OnTick;
            ZObjects.deltaExchangeTicker.OnReconnect += OnReconnect;
            ZObjects.deltaExchangeTicker.OnNoReconnect += OnNoReconnect;
            ZObjects.deltaExchangeTicker.OnError += OnError;
            ZObjects.deltaExchangeTicker.OnClose += OnClose;
            ZObjects.deltaExchangeTicker.OnConnect += OnConnect;
            // ticker.OnOrderUpdate += OnOrderUpdate;

            ZObjects.deltaExchangeTicker.EnableReconnect(Interval: 5, Retries: 50000);
            await ZObjects.deltaExchangeTicker.Connect();

            
            //This timer helps to change subscription tokens incase market has moved.
            //InitTimer();

            SubscribeTokens(null, null);
        }
        public static void InitTimer()
        {
            Timer t = new Timer(3600000);
            t.Start();
            t.Elapsed += SubscribeTokens;
        }

        
        private static void SubscribeTokens(object sender, System.Timers.ElapsedEventArgs e)
        {
            // Dictionary<string, LTP> btokenPrices = ZObjects.kite.GetLTP(new string[] { Constants.NIFTY_TOKEN, Constants.BANK_NIFTY_TOKEN, Constants.FINNIFTY_TOKEN, Constants.MIDCPNIFTY_TOKEN });

            //Pull list of instruments to be subscribed
            //DataAccess.SQlDAO dao = new SQlDAO();
            //UInt32[] instrumentTokens = dao.GetInstrumentListToSubscribe(btokenPrices[NIFTY_TOKEN].LastPrice, btokenPrices[BANK_NIFTY_TOKEN].LastPrice);

            //GlobalObjects.InstrumentTokenSymbolCollection = dao.GetInstrumentListToSubscribe(btokenPrices[Constants.NIFTY_TOKEN].LastPrice, 
            //    btokenPrices[Constants.BANK_NIFTY_TOKEN].LastPrice, btokenPrices[Constants.FINNIFTY_TOKEN].LastPrice, btokenPrices[Constants.MIDCPNIFTY_TOKEN].LastPrice);
            //ZObjects.ticker.Subscribe(Tokens: GlobalObjects.InstrumentTokenSymbolCollection.Keys.ToArray());
            //ZObjects.ticker.SetMode(Tokens: GlobalObjects.InstrumentTokenSymbolCollection.Keys.ToArray(), Mode: Constants.MODE_FULL);

            //Next expiry call and put

            DateTime nextExpiry = DateTime.Today.AddDays(1);


            //var btcTicker =  ZObjects.deltaExchange.GetTickersForProduct("BTCUSD", client: new HttpClient());
            //btcTicker.Wait();
            //var btcTickers = btcTicker.Result;

            //decimal btcSpotPrice = Convert.ToDecimal(btcTickers[0].SpotPrice);

            Dictionary<string, List<string>> channelAndSymbolsTobeSubscribed = new Dictionary<string, List<string>>();

            decimal btcSpotPrice = 83000;

            btcSpotPrice = Math.Round(btcSpotPrice / 1000, 0, MidpointRounding.ToEven) * 1000;

            decimal btcOptionStrikesRange = 8000;

            List<string> symbolsTobeSubscribed = new List<string>();

            symbolsTobeSubscribed.Add("BTCUSD");
            
            decimal startingStrike = btcSpotPrice - btcOptionStrikesRange;
            decimal endingStrike = btcSpotPrice + btcOptionStrikesRange;
            decimal strikePrice = startingStrike;

            while (strikePrice <= endingStrike)
            {
                symbolsTobeSubscribed.Add($"C-BTC-{strikePrice}-{nextExpiry.ToString("ddMMyy")}");
                symbolsTobeSubscribed.Add($"P-BTC-{strikePrice}-{nextExpiry.ToString("ddMMyy")}");

                strikePrice = strikePrice + 200;
            }



            //List<string> symbols = new List<string>() { "BTCUSD", "put_options", "futures" };
            channelAndSymbolsTobeSubscribed.Add("l1_orderbook", symbolsTobeSubscribed);
            //ZObjects.deltaExchangeTicker.Subscribe("l1_orderbook", symbolsTobeSubscribed);


            //symbolsTobeSubscribed = new List<string>();
            //symbolsTobeSubscribed.Add("BTCUSD");

            //startingStrike = btcSpotPrice - btcOptionStrikesRange;
            //startingStrike = btcSpotPrice + btcOptionStrikesRange;
            //strikePrice = startingStrike;

            //while (strikePrice <= endingStrike)
            //{
            //    symbolsTobeSubscribed.Add($"C-BTC-{strikePrice}-{nextExpiry.ToString("ddMMyy")}");
            //    symbolsTobeSubscribed.Add($"P-BTC-{strikePrice}-{nextExpiry.ToString("ddMMyy")}");

            //    strikePrice = strikePrice + 200;
            //}

            
            //while (strikePrice <= endingStrike)
            //{
            //    symbolsTobeSubscribed.Add($"C-BTC-{strikePrice}-{todayExpiry.ToString("ddMMyy")}");
            //    symbolsTobeSubscribed.Add($"P-BTC-{strikePrice}-{todayExpiry.ToString("ddMMyy")}");

            //    strikePrice = strikePrice + 200;
            //}

            //channelAndSymbolsTobeSubscribed.Add("all_trades", symbolsTobeSubscribed);

            ZObjects.deltaExchangeTicker.SubscribedChannelsAndSymbols = channelAndSymbolsTobeSubscribed;

            ZObjects.deltaExchangeTicker.Subscribe(channelAndSymbolsTobeSubscribed);

            //ZObjects.deltaExchangeTicker.Subscribe("all_trades", symbolsTobeSubscribed);
            //ZObjects.deltaExchangeTicker.Subscribe("l1_orderbook", "[\"BTC-030325\"]");

            //"put_options", "futures"
        }
        private static void OnConnect()
        {
            Logger.LogWrite("Connected ticker");
        }

        private static void OnClose()
        {
            Logger.LogWrite("Closed ticker");
        }

        private static void OnError(string Message)
        {
            Logger.LogWrite("Error: " + Message);
        }

        private static void OnNoReconnect()
        {
            Logger.LogWrite("Ticker not reconnecting");
        }

        private static void OnReconnect()
        {
            Logger.LogWrite("Reconnecting Ticker");
        }
        public static String GetTimestamp(DateTime value)
        {
            return value.ToString("yyyyMMddHHmmssffff");
        }
        private static void OnTick(Tick[] Ticks, Object state)
        {
            //Check if OI data is comming properly.Store that as well
            //publish ticks

        }
    }
}
