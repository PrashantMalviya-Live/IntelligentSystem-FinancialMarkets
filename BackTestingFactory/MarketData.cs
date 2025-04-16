using BrokerConnectWrapper;
using DataAccess;
using GlobalLayer;
using KiteConnect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackTestingFactory
{
    public class MarketData
    {
        public void RetrieveHistoricalTicksFromKite()
        {
            ZConnect.Login();
            ZObjects.ticker = new Ticker(ZConnect.UserAPIkey, ZConnect.UserAccessToken, null);//State:zSessionState.Current);

            decimal fromStrike = 33500;
            decimal toStrike = 40000;
            DateTime fromDate = Convert.ToDateTime("2021-10-01");
            DateTime toDate = Convert.ToDateTime("2021-12-21");
            DateTime expiry = Convert.ToDateTime("2021-12-30");
            TimeSpan candleTimeSpan = new TimeSpan(0, 5, 0);
            var dates = new List<DateTime>();
            //zmqServer = new ZMQServer();
            for (var dt = fromDate; dt <= toDate; dt = dt.AddDays(1))
            {
                dates.Add(dt);
            }
            
            DirectionalWithStraddleShiftCandle directionalWithStraddleShiftCandle = new DirectionalWithStraddleShiftCandle(DateTime.Now, candleTimeSpan, 260105,
                expiry, 1, 0, 10000, 100, true);

            foreach (var date in dates)
            {
                List<Historical> btokenPrices = ZObjects.kite.GetHistoricalData(Constants.BANK_NIFTY_TOKEN, date, date.AddDays(1), "minute");

                List<uint> TokensToSubscribe = GetInstrumentTokens(btokenPrices[10].Close);
                Dictionary<uint, List<Historical>> historicalPrices = new Dictionary<uint, List<Historical>>();
                foreach (uint instrumentToken in TokensToSubscribe)
                {
                    historicalPrices.Add(instrumentToken, ZObjects.kite.GetHistoricalData(instrumentToken.ToString(), date, date.AddDays(1), "minute"));//, Continuous:true));
                    //historicalPrices.AddRange(ZObjects.kite.GetHistoricalData(instrumentToken.ToString(), date, date.AddDays(1), "minute"));
                }
                Dictionary<DateTime, HistoricalData> historicalData = MergeHistoricals(historicalPrices);
                directionalWithStraddleShiftCandle.RunAlgo(historicalData);
            }
        }

        private List<uint> GetInstrumentTokens (decimal _baseInstrumentPrice)
        {
            DataAccess.MarketDAO dao = new MarketDAO();
            GlobalObjects.InstrumentTokenSymbolCollection = dao.GetInstrumentListToSubscribe(18000, _baseInstrumentPrice);
             return GlobalObjects.InstrumentTokenSymbolCollection.Select(x=>x)
                .Where(y=>y.Value.Contains("BANKNIFTY22") && Convert.ToDecimal(y.Value.Substring(14,5)) < _baseInstrumentPrice + 1000 && Convert.ToDecimal(y.Value.Substring(14, 5)) > _baseInstrumentPrice - 1000).Select(x=>x.Key).ToList();
        }

        private Dictionary<DateTime, HistoricalData> MergeHistoricals(Dictionary<uint, List<Historical>> historicalPrices)
        {
            List<DateTime> times = historicalPrices.Values.Select(x => x.Select(y=>y.TimeStamp).First()).Distinct().ToList();

            Dictionary<DateTime, HistoricalData> historicalData = new Dictionary<DateTime, HistoricalData>();
            foreach (var time in times)
            {
                foreach (var historical in historicalPrices)
                {
                    historicalData.Add(time, new HistoricalData()
                    {
                        HistoricalValue = historical.Value.Where(x => x.TimeStamp == time).FirstOrDefault(),
                        InstrumentToken = historical.Key
                    });
                }
            }
            return historicalData;
        }
    }
}
