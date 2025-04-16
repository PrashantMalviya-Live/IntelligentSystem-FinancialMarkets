using GlobalLayer;
using Google.Protobuf.WellKnownTypes;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrpcPeerLoggerService
{
    public static class ClientChartRepository
    {
        public static List<CData> Charts { get; set; } = new List<CData>();

        public static void AddChart(CData chartData)
        {
            Charts.Add(chartData);
        }

        public static void AddChart(string algoid, int algoInstance, int chartId, 
            int chartDataId, uint instrumentToken, DateTime chartTimeStamp, decimal chartData)
        {
            CData chart = new CData
            {
                AlgoId = algoid,
                AlgoInstance = algoInstance,
                InstrumentToken = instrumentToken,
                ChartId = chartId,
                ChartdataId = chartDataId,
                T = Timestamp.FromDateTime(chartTimeStamp),
                D = Convert.ToDouble(chartData),
            };
            AddChart(chart);
        }

        public static void ClearChart()
        {
            Charts.Clear();
        }

    }

    public interface IClientChartRepository
    {
        public List<CData> Charts { get; set; }
        public void AddChart(CData chartData);
        public void ClearChart();
    }
}
