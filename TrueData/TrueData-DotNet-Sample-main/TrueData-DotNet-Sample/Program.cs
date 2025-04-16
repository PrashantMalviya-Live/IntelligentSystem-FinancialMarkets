using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TrueData_DotNet;

namespace TrueData_DotNet_Sample
{
    class Program
    {
        static TDWebSocket tDWebSocket;
        static TDHistory tDHistory;
        static void Main(string[] args)
        {
            connectWebSocketRT();
            //connectRESTHistory();

        }
        private static void connectRESTHistory()
        {
            tDHistory = new TDHistory("user-name", "user-password");
            tDHistory.login();

            //for (int i = 0; i < 360; i++)
            //{
            string csvResponse = tDHistory.GetBarHistory("CRUDEOIL-I", DateTime.Now.AddDays(0).AddHours(0).AddMinutes(-3), DateTime.Now, true, Constants.Interval_3Min);
            Console.WriteLine(DateTime.Now.AddDays(0).AddHours(0).AddMinutes(-3));
            Console.WriteLine("History 1min for Reliance");
            Console.WriteLine(csvResponse);
            //    Thread.Sleep(1000);
            //}
            string csvTickData = tDHistory.GetTickHistory("ACC", false, DateTime.Now.AddHours(-12), DateTime.Now, true);
            Console.WriteLine("History Tick for ACC");
            Console.WriteLine(csvTickData);

            //for (int i = 0; i < 360; i++)
            //{
            //    string csvLastNBar = tDHistory.GetLastNBars("CRUDEOIL-I", 1, true,"3min");
            //    Console.WriteLine("Last bar of 3min  ");
            //    Console.WriteLine(csvLastNBar);
            //    Thread.Sleep(1000);
            //}
            string csvLastNTicks = tDHistory.GetLastNTicks("HDFC", true, 100, true, "tick");
            Console.WriteLine("Last 100 tick of HDFC");
            Console.WriteLine(csvLastNTicks);

            string strLTP = tDHistory.GetLTP("HDFC", true, true);
            Console.WriteLine("LTP HDFC");
            Console.WriteLine(strLTP);

            string csvBhavCopy = tDHistory.GetBhavCopy("EQ", DateTime.Today, true);
            Console.WriteLine("Today's Bhavcopy");
            Console.WriteLine(csvBhavCopy);

            string csvGainers = tDHistory.GetTopGainers("NSEEQ", true, 20);
            Console.WriteLine("Today's Gainers");
            Console.WriteLine(csvGainers);

            string csvLosers = tDHistory.GetTopLosers("CASH", true, 20);
            Console.WriteLine("Today's Losers");
            Console.WriteLine(csvLosers);

            string csvCorpAction = tDHistory.GetCorporateActions("AARTIIND", true);
            Console.WriteLine("Corporate Actions");
            Console.WriteLine(csvCorpAction);
            Console.Read();
        }

        private static void connectWebSocketRT()
        {
            tDWebSocket = new TDWebSocket("user-name", "user-pwd", "wss://push.truedata.in", 8082);

            tDWebSocket.OnConnect += TDWebSocket_OnConnect;
            tDWebSocket.OnDataArrive += TDWebSocket_OnDataArrive;
            tDWebSocket.OnClose += TDWebSocket_OnClose;
            tDWebSocket.OnError += TDWebSocket_OnError;
            //new Thread(() => {
            //    Thread.Sleep(5000);
            //    tDWebSocket.GetMarketStatus();
            //    tDWebSocket.UnSubscribe(new string[] { "NIFTY-I", "RELIANCE", "HDFC", "HDFCBANK", "ICICIBANK", "ZEEL", "MINDTREE", "NIFTY 50", "CRUDEOIL-I" });
            //    tDWebSocket.Logout();
            //}).Start();

            tDWebSocket.ConnectAsync();

        }

        private static void TDWebSocket_OnError(object sender, EventErrorArgs e)
        {
            Console.WriteLine(e.ErrorMsg);
            Console.Read();
        }

        private static void TDWebSocket_OnClose(object sender, EventArgs e)
        {
            Console.WriteLine("Disconnected");
            Console.Read();
        }

        private static void TDWebSocket_OnDataArrive(object sender, EventDataArgs e)
        {
            //Console.WriteLine(e.JsonMsg);
            if (e.JsonMsg.Contains("bidask"))
            {
                BidAsk b = new BidAsk();
                b = JsonConvert.DeserializeObject<BidAsk>(e.JsonMsg);
                Console.WriteLine(b.SymbolId + "-" + b.Timestamp + "-" + b.Bid + "-" + b.Ask + "-" + b.BidQty);
            }
            else if (e.JsonMsg.Contains("trade"))
            {
                SnapData t = new SnapData();
                t = JsonConvert.DeserializeObject<SnapData>(e.JsonMsg);
                Console.WriteLine(t.SymbolId + "-" + t.Timestamp + "-" + t.LTP + "-" + t.Volume + "-" + t.Open + "-" + t.High + "-" + t.Low + "-" + t.PrevClose);
            }
            else if (e.JsonMsg.Contains("HeartBeat"))
            {
                HeartBeat hb = new HeartBeat();
                hb = JsonConvert.DeserializeObject<HeartBeat>(e.JsonMsg);
                Console.WriteLine("Heartbeat " + hb.timestamp + "-" + hb.message);
            }
            else if (e.JsonMsg.Contains("TrueData"))
            {
                tDWebSocket.Subscribe(new string[] { "NIFTY-I", "RELIANCE", "HDFC", "HDFCBANK", "ICICIBANK", "ZEEL", "MINDTREE", "NIFTY 50", "CRUDEOIL-I" });
            }
            else
            {
                Console.WriteLine(e.JsonMsg);
            }
        }

        private static void TDWebSocket_OnConnect(object sender, EventArgs e)
        {
            //Console.WriteLine("Connected");
            
            //tDWebSocket.Send("{\"method\":\"addsymbol\",\"symbols\":[\"NIFTY-I\",\"RELIANCE\",\"HDFC\",\"HDFCBANK\",\"ICICIBANK\",\"ZEEL\",\"MINDTREE\",\"NIFTY 50\",\"CRUDEOIL-I\"]}");

        }

    }
    class BidAsk
    {
        [JsonIgnore]
        public int SymbolId { get; set; }
        [JsonIgnore]
        public DateTime Timestamp { get; set; }
        [JsonIgnore]
        public float Bid { get; set; }
        [JsonIgnore]
        public int BidQty { get; set; }
        [JsonIgnore]
        public float Ask { get; set; }
        [JsonIgnore]
        public float AskQty { get; set; }
        private string[] bidask1;
        [JsonProperty]
        public string[] bidask
        {
            get { return bidask1; }
            set
            {
                bidask1 = value;
                SymbolId = int.Parse(bidask1[0]);
                Timestamp = DateTime.Parse(bidask1[1]);
                Bid = float.Parse(bidask1[2]);
                BidQty = int.Parse(bidask1[3]);
                Ask = float.Parse(bidask1[4]);
                AskQty = int.Parse(bidask1[5]);
            }
        }


    }
    class SnapData
    {
        [JsonIgnore]
        public int SymbolId { get; set; }
        [JsonIgnore]
        public DateTime Timestamp { get; set; }
        [JsonIgnore]
        public float LTP { get; set; }
        [JsonIgnore]
        public float ATP { get; set; }
        [JsonIgnore]
        public int Volume { get; set; }
        [JsonIgnore]
        public long TotalVolume { get; set; }
        [JsonIgnore]
        public float Open { get; set; }
        [JsonIgnore]
        public float High { get; set; }
        [JsonIgnore]
        public float Low { get; set; }
        [JsonIgnore]
        public float PrevClose { get; set; }
        [JsonIgnore]
        public int OI { get; set; }
        [JsonIgnore]
        public int PrevOI { get; set; }
        [JsonIgnore]
        public double TurnOver { get; set; }
        [JsonIgnore]
        public string OHL { get; set; }
        [JsonIgnore]
        public int sequenceno { get; set; }
        [JsonIgnore]
        public float Bid { get; set; }
        [JsonIgnore]
        public int BidQty { get; set; }
        [JsonIgnore]
        public float Ask { get; set; }
        [JsonIgnore]
        public float AskQty { get; set; }
        private string[] tradelocal;
        [JsonProperty]
        public string[] trade
        {
            get { return tradelocal; }
            set
            {
                tradelocal = value;
                SymbolId = int.Parse(tradelocal[0]);
                Timestamp = DateTime.Parse(tradelocal[1]);
                LTP = float.Parse(tradelocal[2]);
                Volume = int.Parse(tradelocal[3]);
                ATP = float.Parse(tradelocal[4]);
                TotalVolume = long.Parse(tradelocal[5]);
                Open = float.Parse(tradelocal[6]);
                High = float.Parse(tradelocal[7]);
                Low = float.Parse(tradelocal[8]);
                PrevClose = float.Parse(tradelocal[9]);
                OI = int.Parse(tradelocal[10]);
                PrevOI = int.Parse(tradelocal[11]);
            }
        }
    }
    public class HeartBeat
    {
        public bool success { get; set; }
        public string message { get; set; }
        public DateTime timestamp { get; set; }
    }
}
