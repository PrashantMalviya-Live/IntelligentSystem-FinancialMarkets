namespace DataAnalyzer2
{
    using DataAccess;
    using LocalDBData;
    using InfluxDB;
    using BrokerConnectWrapper;
    using KiteConnect;
    using GlobalLayer;
    using Algorithms.Utils;
    using Algorithms.Utilities;
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }
        InfluxDB idb = new InfluxDB();
        private void btnGreeks_Click(object sender, EventArgs e)
        {
            MarketDAO marketDAO = new MarketDAO();
            StoreGreeks();
        }

        private void StoreGreeks()
        {
            ZConnect.Login();
            ZObjects.ticker = new Ticker(ZConnect.UserAPIkey, ZConnect.UserAccessToken, null);//State:zSessionState.Current);

            decimal fromStrike = 33500;
            decimal toStrike = 40000;
            DateTime fromDate = Convert.ToDateTime("2021-12-21");
            DateTime toDate = Convert.ToDateTime("2021-12-23");
            DateTime expiry = Convert.ToDateTime("2021-12-23");
            var dates = new List<DateTime>();
            //zmqServer = new ZMQServer();
            for (var dt = fromDate; dt <= toDate; dt = dt.AddDays(1))
            {
                dates.Add(dt);
            }
            DataAccess.MarketDAO dao = new MarketDAO();
            Option[] calls, puts;
            List<Historical> historicalPrices;
            foreach (var date in dates)
            {
                List<Historical> btokenPrices = ZObjects.kite.GetHistoricalData(Constants.BANK_NIFTY_TOKEN, date, date.AddDays(1), "day");

                foreach (var historicalbPrice in btokenPrices)
                {
                    calls = GetOption(expiry, historicalbPrice.Close, 260105, InstrumentType.CE); // 10 strike prices above & below
                    puts = GetOption(expiry, historicalbPrice.Close, 260105, InstrumentType.PE); // 10 strike prices above & below
                    
                    foreach (var call in calls)
                    {
                        uint instrumentToken = call.InstrumentToken;
                        historicalPrices = ZObjects.kite.GetHistoricalData(instrumentToken.ToString(), date, date.AddDays(1), "minute");
                        CalculateGreeks(call, historicalPrices);
                    }
                    foreach (var put in puts)
                    {
                        uint instrumentToken = put.InstrumentToken;
                        historicalPrices = ZObjects.kite.GetHistoricalData(instrumentToken.ToString(), date, date.AddDays(1), "minute");
                        CalculateGreeks(put, historicalPrices);
                    }
                }
            }
        }
        private void CalculateGreeks(Option option, List<Historical> historicalPrices)
        {
            foreach (var ohlcv in historicalPrices)
            {
                option.LastPrice = ohlcv.Close;
                option.LastTradeTime = ohlcv.TimeStamp;
                BS bs = new BS(option);
                decimal delta = bs.Delta(ohlcv.TimeStamp, ohlcv.Close);
                decimal? iv = bs.ImpliedVolatility(ohlcv.TimeStamp, ohlcv.Close);
                decimal? gamma = bs.Gamma(ohlcv.TimeStamp, ohlcv.Close);
                idb.WriteTickGreeks(ohlcv, option.InstrumentToken, delta, gamma.HasValue ? gamma.Value : 0, iv.HasValue ? iv.Value : 0, option.Expiry.Value, option.TradingSymbol);
            }
        }
        public Option[] GetOption(DateTime expiry, decimal strike, uint binstrumentToken, InstrumentType instrumentType)
        {
            DataLogic dl = new DataLogic();
            var optionsList = dl.RetrieveNextNodes(binstrumentToken, strike, instrumentType.ToString(), strike, expiry, 0, 10);
            return optionsList.Values.ToArray<Option>();
        }

        private void btnST_Click(object sender, EventArgs e)
        {

        }
    }
}