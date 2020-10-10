using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Global;
using KiteConnect;
//using StorageAlgos;
using ZConnectWrapper;
using MarketDataTest;
using AdvanceAlgos.Algorithms;
using Algorithms.Indicators;
using System.IO;
using DataAccess;
using Apache.Ignite.Core;
//using NRedisTimeSeries;
using StackExchange.Redis;
//using InMemoryDB;

namespace TestWindowsApplication
{
    public partial class Form1 : Form
    {
       // private IIgnite _ignite;
        public Form1()//IIgnite ignite)
        {
           // _ignite = ignite;
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //KafkaServer.StartKafka();
            //Admin.CreateTopics();

            //TickDataStreamer dataStreamer = new TickDataStreamer();

            //ManagedStrangleDelta ms = new ManagedStrangleDelta();
            //ms.Subscribe(dataStreamer);

            //ManageStrangleValue ms = new ManageStrangleValue();
            //ms.Subscribe(dataStreamer);

            //ManageStrangleConstantWidth manageStrangleConstantWidth = new ManageStrangleConstantWidth();
            //manageStrangleConstantWidth.Subscribe(dataStreamer);
            //dataStreamer.BeginStreaming();
            //DataLogic_Candles dataLogic = new DataLogic_Candles();
            //dataLogic.StoreMarketData();
            ////ticker.Close();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            //ZObjects.ticker.Close();
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            //Instrument instrument = new Instrument();
            //instrument.Strike = 11500;
            //instrument.LastPrice = 19.8m;
            //instrument.InstrumentType = "PE";
            //instrument.Expiry = Convert.ToDateTime("25-04-2019 03:30:00 PM");
            //double delta = instrument.UpdateDelta(11594.45, 0.1, Convert.ToDateTime("22-04-2019 03:30:00 PM"));
        }

        private void btnJsonCompare_Click(object sender, EventArgs e)
        {
            string json1 = File.ReadAllText(@"D:\Prashant\Intelligent Software\2019\MarketData\Log\WriteTick.txt");
            string json2 = File.ReadAllText(@"D:\Prashant\Intelligent Software\2019\MarketData\Log\ReadTick.txt");
            
            var ticks1 = JsonSerializer.Deserialize(json1, typeof(Tick));
            var ticks2 = JsonSerializer.Deserialize(json2, typeof(Tick));

        }

        private void btnRedis_MouseClick(object sender, MouseEventArgs e)
        {

        }

        private void btnRedis_Click(object sender, EventArgs e)
        {
            //RedisDB.InsertData("data");

            //RedisDB.SimpleGetAsyncExample();
        }

        private void btnRedisServer_Click(object sender, EventArgs e)
        {
            //RedisDB.GetData();
            //RedisDB.SimpleCreate();
            //RedisDB.SimpleCreateAsync();

            //RedisDB.DateTimeAddAsync();
        }

        private void btnMemSQL_Click(object sender, EventArgs e)
        {

        }

        private void btnIgnite_Click(object sender, EventArgs e)
        {
            //IgniteConnector igniteConnector = new IgniteConnector();
            //igniteConnector.Put(_ignite);

            //igniteConnector.Get(_ignite);
        }

        private void btnTIBCO_Click(object sender, EventArgs e)
        {

        }

        private void btnRSI_Click(object sender, EventArgs e)
        {
            RelativeStrengthIndex rsi = new RelativeStrengthIndex();

            //            decimal[] prices = new decimal[] { 44.34m, 44.09m, 44.15m, 43.61m, 44.33m, 44.83m, 45.10m, 45.42m, 45.84m, 46.08m, 45.89m, 46.03m, 45.61m, 46.28m, 46.28m, 46.00m, 46.03m, 46.41m, 46.22m, 45.64m, 46.21m, 46.25m, 45.71m, 46.45m, 45.78m, 45.35m, 44.03m, 44.18m, 44.22m, 44.57m, 43.42m, 42.66m, 43.13m };

            decimal[] prices = new decimal[] { 44.34m, 0, 44.09m, 0, 44.15m, 0, 43.61m, 0, 44.33m, 0, 44.83m, 0, 45.10m, 0, 45.42m, 0, 45.84m, 0, 46.08m, 0, 45.89m, 0, 46.03m, 0, 45.61m, 0, 46.28m, 0, 46.28m, 0, 46.00m, 0, 46.03m, 0, 46.41m, 0, 46.22m, 0, 45.64m, 0, 46.21m, 0, 46.25m, 0, 45.71m, 0, 46.45m, 0, 45.78m, 0, 45.35m, 0, 44.03m, 0, 44.18m, 0, 44.22m, 0, 44.57m, 0, 43.42m, 0, 42.66m, 0, 43.13m };

            for (int i = 0; i < prices.Length; i++)
            {
                decimal price = prices[i];
                if (i % 2 == 0)
                    rsi.Process(price, true);
                else
                    rsi.Process(price, false);

                Console.WriteLine("Index{0}, Price:{1}, RSI:{2}", i, price, rsi.GetCurrentValue<decimal>());
            }

        }

        private void button3_Click(object sender, EventArgs e)
        {
            decimal r = Math.Round(0.27m * 20) / 20;
        }

        private void btnGrpc_Click(object sender, EventArgs e)
        {

        }
    }
}
