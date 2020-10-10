using DataAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GlobalLayer;
namespace Algorithms.Utilities
{
    public class Utility
    {
        public static int GenerateAlgoInstance(AlgoIndex algoIndex, DateTime executionTime)
        {
            MarketDAO dao = new MarketDAO();
            return dao.GenerateAlgoInstance(algoIndex, executionTime);
        }
    }
}
