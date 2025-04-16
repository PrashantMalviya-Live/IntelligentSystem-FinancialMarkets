using BrokerConnectWrapper;
using DataAccess;
using GlobalLayer;
using KiteConnect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlgoBackTester
{
    public class MarketData
    {
        private void RetrieveHistoricalTicksFromKite()
        {
            ZConnect.Login();
            ZObjects.ticker = new Ticker(ZConnect.UserAPIkey, ZConnect.UserAccessToken, null);//State:zSessionState.Current);

            decimal fromStrike = 33500;
            decimal toStrike = 40000;
            DateTime fromDate = Convert.ToDateTime("2021-10-01");
            DateTime toDate = Convert.ToDateTime("2021-12-21");

            var dates = new List<DateTime>();
            //zmqServer = new ZMQServer();
            for (var dt = fromDate; dt <= toDate; dt = dt.AddDays(1))
            {
                dates.Add(dt);
            }
            DataAccess.MarketDAO dao = new MarketDAO();

            foreach (var date in dates)
            {
                List<Historical> btokenPrices = ZObjects.kite.GetHistoricalData(Constants.BANK_NIFTY_TOKEN, date, date.AddDays(1), "minute");
                GlobalObjects.InstrumentTokenSymbolCollection = dao.GetInstrumentListToSubscribe(0, btokenPrices[10].Close);
                List<Historical> historicalPrices = new List<Historical>();
                foreach (uint instrumentToken in GlobalObjects.InstrumentTokenSymbolCollection.Keys)
                {
                    historicalPrices.AddRange(ZObjects.kite.GetHistoricalData(instrumentToken.ToString(), date, date.AddDays(1), "minute"));
                    //ZObjects.ticker.zmqServer.PublishAllTicks(ticks, shortenedTick: true);
                    //zmqServer.PublishAllTicks(ticks, shortenedTick: true);
                }
            }
        }
    }
}
