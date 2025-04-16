using StockMarketAlertsApp.Models;
using static StockMarketAlertsApp.Models.Indicator;

namespace StockMarketAlertsApp.Clients
{
    public class MarketClient (HttpClient httpClient)
    {
        public async Task<Instrument[]> GetInstrumentsAsync()
        {
            return await httpClient.GetFromJsonAsync<Instrument[]>("market/instruments") ?? [];
        }
        public async Task<CandleTimeFrame[]> GetCandleTimeFramesAsync()
        {
            return await httpClient.GetFromJsonAsync<CandleTimeFrame[]>("market/candletimeframes") ?? [];
        }
        public async Task<IndicatorOperator[]> GetIndicatorListAsync()
        {
            return await httpClient.GetFromJsonAsync<IndicatorOperator[]>("market/indicators") ?? [];
        }
        
        //public async Task<IndicatorPropertiesOptionsValues[]> GetIndicatorPropertyOptions()
        //{
        //    return await httpClient.GetFromJsonAsync<IndicatorPropertiesOptionsValues[]>("market/indicatorpropertyoptionvalues") ?? [];
        //}
        
        public Dictionary<string, string> GetMathOperatorOptions()
        {
            return new Dictionary<string, string> { { "0", "Equals to" }, { "1", "Greater than" }, { "2", "Less than" } };
        }

        public Dictionary<string, string> GetLogicalOperatorOptions()
        {
            return new Dictionary<string, string> { { "0", "AND" }, { "1", "OR" } };
        }


        public static List<string> Labels => GetSectorTrend().Select(x => x.TradeDate.ToShortDateString()).Distinct().ToList();   //). new() { "1", "2", "3", "4" };

        public static int SectorCount => GetSectorTrend().Select(s => s.SectorId).Distinct().Count();

        public static int SectorDatasetCount =>  GetSectorTrend().Select(s => s.TradeDate).Distinct().Count();

        public static List<double> PriceChangesBySectorId(int sectorId)
        {
            var sectorTrends = GetSectorTrend();

            return sectorTrends.Where(i => i.SectorId.Equals(sectorId)).Select(x => Convert.ToDouble(x.AveragePriceChange)).ToList();
        }
        
        public static List<SectorTrend> GetSectorTrend()
        {
            SectorTrend[] sectorHistoricalPriceData = [
            new (){Id=1, SectorId=1, TradeDate=DateOnly.Parse("2024-06-28"), AveragePriceChange= 24  },
            new (){Id=2, SectorId=1, TradeDate=DateOnly.Parse("2024-06-27"), AveragePriceChange= 25  },
            new (){Id=3, SectorId=1, TradeDate=DateOnly.Parse("2024-06-26"), AveragePriceChange= 26  },
            new (){Id=4, SectorId=1, TradeDate=DateOnly.Parse("2024-06-25"), AveragePriceChange= 28  },
            new (){Id=5, SectorId=1, TradeDate=DateOnly.Parse("2024-06-24"), AveragePriceChange= 29  },
            new (){Id=6, SectorId=1, TradeDate=DateOnly.Parse("2024-06-23"), AveragePriceChange= 25  },
            new (){Id=7, SectorId=1, TradeDate=DateOnly.Parse("2024-06-22"), AveragePriceChange= 26  },

            new (){Id=8, SectorId=2, TradeDate=DateOnly.Parse("2024-06-28"), AveragePriceChange= 100  },
            new (){Id=9, SectorId=2, TradeDate=DateOnly.Parse("2024-06-27"), AveragePriceChange= 80 },
            new (){Id=10, SectorId=2, TradeDate=DateOnly.Parse("2024-06-26"), AveragePriceChange= 76  },
            new (){Id=11, SectorId=2, TradeDate=DateOnly.Parse("2024-06-25"), AveragePriceChange= 77  },
            new (){Id=12, SectorId=2, TradeDate=DateOnly.Parse("2024-06-24"), AveragePriceChange= 78  },
            new (){Id=13, SectorId=2, TradeDate=DateOnly.Parse("2024-06-23"), AveragePriceChange= 79  },
            new (){Id=14, SectorId=2, TradeDate=DateOnly.Parse("2024-06-22"), AveragePriceChange= 75  },


            new (){Id=15, SectorId=3, TradeDate=DateOnly.Parse("2024-06-28"), AveragePriceChange= 1  },
            new (){Id=16, SectorId=3, TradeDate=DateOnly.Parse("2024-06-27"), AveragePriceChange= 2  },
            new (){Id=17, SectorId=3, TradeDate=DateOnly.Parse("2024-06-26"), AveragePriceChange= 4  },
            new (){Id=18, SectorId=3, TradeDate=DateOnly.Parse("2024-06-25"), AveragePriceChange= 3  },
            new (){Id=19, SectorId=3, TradeDate=DateOnly.Parse("2024-06-24"), AveragePriceChange= 5  },
            new (){Id=20, SectorId=3, TradeDate=DateOnly.Parse("2024-06-23"), AveragePriceChange= 6  },
            new (){Id=21, SectorId=3, TradeDate=DateOnly.Parse("2024-06-22"), AveragePriceChange= 8  },

            new (){Id=22, SectorId=4, TradeDate=DateOnly.Parse("2024-06-28"), AveragePriceChange= -41  },
            new (){Id=23, SectorId=4, TradeDate=DateOnly.Parse("2024-06-27"), AveragePriceChange= -42  },
            new (){Id=24, SectorId=4, TradeDate=DateOnly.Parse("2024-06-26"), AveragePriceChange= -46  },
            new (){Id=25, SectorId=4, TradeDate=DateOnly.Parse("2024-06-25"), AveragePriceChange= 38  },
            new (){Id=26, SectorId=4, TradeDate=DateOnly.Parse("2024-06-24"), AveragePriceChange= 42  },
            new (){Id=27, SectorId=4, TradeDate=DateOnly.Parse("2024-06-23"), AveragePriceChange= 32  },
            new (){Id=28, SectorId=4, TradeDate=DateOnly.Parse("2024-06-22"), AveragePriceChange= 26  },
            ];

            var sectorList = sectorHistoricalPriceData.ToList();
            sectorList.Sort();

            return sectorList;
        }
    }
}
