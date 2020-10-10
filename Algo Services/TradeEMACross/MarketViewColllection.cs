using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TradeEMACross
{
    public class BInstumentView
    {
        public UInt32 InstrumentToken { get; set; }
        public string TradingSymbol { get; set; }
    }
    public class OptionView
    {
        public UInt32 InstrumentToken { get; set; }
        public string TradingSymbol { get; set; }
        public string Type { get; set; }
        public decimal Strike { get; set; }
    }
    public  class ChartModel
    {
        public List<int> Data { get; set; }
        public string Label { get; set; }
        public ChartModel()
        {
            Data = new List<int>();
        }
    }
   
}
