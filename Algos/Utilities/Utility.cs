using DataAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GlobalLayer;
using ZConnectWrapper;
namespace Algorithms.Utilities
{
    public class Utility
    {
        public static int GenerateAlgoInstance(AlgoIndex algoIndex, uint bToken, DateTime timeStamp, DateTime expiry,
            int initialQtyInLotsSize, int maxQtyInLotSize = 0, int stepQtyInLotSize = 0, decimal upperLimit = 0,
            decimal upperLimitPercent = 0, decimal lowerLimit = 0, decimal lowerLimitPercent = 0,
            float stopLossPoints = 0, int optionType = 0, float candleTimeFrameInMins = 5, 
            CandleType candleType = CandleType.Time, int optionIndex = 0)
        {
            MarketDAO dao = new MarketDAO();
            return dao.GenerateAlgoInstance(algoIndex, bToken, timeStamp, expiry,
            initialQtyInLotsSize, maxQtyInLotSize, stepQtyInLotSize, upperLimit,
            upperLimitPercent, lowerLimit, lowerLimitPercent,
            stopLossPoints: stopLossPoints, optionType: optionType, 
            candleTimeFrameInMins: candleTimeFrameInMins, candleType: candleType, 
            optionIndex: optionIndex);
        }

        public static void LoadTokens()
        {
            List<Instrument> instruments = ZObjects.kite.GetInstruments(Exchange: "NFO");
            MarketDAO dao = new MarketDAO();
            dao.StoreInstrumentList(instruments);

            instruments = ZObjects.kite.GetInstruments(Exchange: "NSE");
            dao = new MarketDAO();
            dao.StoreInstrumentList(instruments);
        }
    }
}
