using DataAccess;
using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Algos.Utilities
{
    public class CryptoDataLogic
    {
        public void UpdateOrder(int algoInstance, CryptoOrder order, int strategyId = 0)
        {
            order.AlgoInstanceId = algoInstance;
            order.StrategyId = strategyId;
            DataAccess.QuestDB.InsertObject(order).Wait();
        }

        public void UpdateAlgoPnl(int algoInstance, decimal pnl, DateTime currentTime)
        {
            DataAccess.QuestDB.InsertObject(algoInstance, Math.Round(pnl, 0), currentTime).Wait();
        }
    }
}
