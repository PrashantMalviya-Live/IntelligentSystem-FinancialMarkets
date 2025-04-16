using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KiteConnect;
using GlobalLayer;
using BrokerConnectWrapper;
using DataAccess;
using System.Timers;
using ZMQFacade;
using System.Runtime.CompilerServices;
//using KiteConnectTicker;
namespace MarketDataService
{
   public class MarketData
    {
        //public static readonly string NIFTY_TOKEN = "256265";
        //public static readonly string BANK_NIFTY_TOKEN = "260105";
        public static void PublishData()
        {
            ZConnect.Login();
            ZObjects.ticker = new Ticker(ZConnect.UserAPIkey, ZConnect.UserAccessToken, null);//State:zSessionState.Current);

            ZObjects.ticker.OnTick += OnTick;
            ZObjects.ticker.OnReconnect += OnReconnect;
            ZObjects.ticker.OnNoReconnect += OnNoReconnect;
            ZObjects.ticker.OnError += OnError;
            ZObjects.ticker.OnClose += OnClose;
            ZObjects.ticker.OnConnect += OnConnect;
            // ticker.OnOrderUpdate += OnOrderUpdate;

            ZObjects.ticker.EnableReconnect(Interval: 5, Retries: 50);
            ZObjects.ticker.Connect();

            
            InitTimer();
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
            Dictionary<string, LTP> btokenPrices = ZObjects.kite.GetLTP(new string[] { Constants.NIFTY_TOKEN, Constants.BANK_NIFTY_TOKEN, Constants.FINNIFTY_TOKEN, Constants.MIDCPNIFTY_TOKEN });

            //Pull list of instruments to be subscribed
            DataAccess.MarketDAO dao = new MarketDAO();
            //UInt32[] instrumentTokens = dao.GetInstrumentListToSubscribe(btokenPrices[NIFTY_TOKEN].LastPrice, btokenPrices[BANK_NIFTY_TOKEN].LastPrice);

            GlobalObjects.InstrumentTokenSymbolCollection = dao.GetInstrumentListToSubscribe(btokenPrices[Constants.NIFTY_TOKEN].LastPrice, 
                btokenPrices[Constants.BANK_NIFTY_TOKEN].LastPrice, btokenPrices[Constants.FINNIFTY_TOKEN].LastPrice, btokenPrices[Constants.MIDCPNIFTY_TOKEN].LastPrice);
            ZObjects.ticker.Subscribe(Tokens: GlobalObjects.InstrumentTokenSymbolCollection.Keys.ToArray());
            ZObjects.ticker.SetMode(Tokens: GlobalObjects.InstrumentTokenSymbolCollection.Keys.ToArray(), Mode: Constants.MODE_FULL);
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
