using Microsoft.Extensions.Primitives;
using StockMarketAlertsApp.Models;
using StockMarketAlertsApp.Helper;
using Microsoft.AspNetCore.Mvc;
using StockMarketAlertsApp.Components.Pages;
using Microsoft.AspNetCore.Http;

namespace StockMarketAlertsApp.Clients
{
    public class AlgoClient (HttpClient httpClient)
    {
        public async Task<AlgoParams[]> GetUserAlgos(string userId)
        {
            return await httpClient.GetFromJsonAsync<AlgoParams[]>($"Algo/user/{userId}") ?? [];
        }
        public async Task<List<StockBreakout>> GetStockBreakoutsAsync(int algoid)
        {
            //1 is for breakout algo
            StockBreakout[] stockBreakouts = await httpClient.GetFromJsonAsync<StockBreakout[]>($"Algo/{algoid}") ?? [];

            return stockBreakouts.ToList();

            
        }

        //private string GrpcClient_AlertManagerEvent(GrpcAlertProtos.AlertMessage alertMessage)
        //{
        //    //check if algo id is breakout algoid, if yes send it to breakout method

        //    if(alertMessage != null)
        //    {
        //        if(alertMessage.AlertId == 1)
        //        {

        //        }

        //    }
        //}

        //public async Task<Dictionary<uint, Dictionary<TimeSpan, StockBreakoutView>>> UpdateStockBreakoutView(AlertMessage)
        //{
        //    RBC.BaseInstruments
        //}

        public async Task<Dictionary<uint, Dictionary<TimeSpan, StockBreakoutView>>> GetStockBreakoutView(int algoId)
        {
            List<StockBreakout> stockBreakouts = await GetStockBreakoutsAsync(algoId);

            Dictionary<uint, Dictionary<TimeSpan, StockBreakoutView>> sbvDictionary = new Dictionary<uint, Dictionary<TimeSpan, StockBreakoutView>>();

            foreach (var stockBreakout in stockBreakouts)
            {
                Dictionary<TimeSpan, StockBreakoutView> sbvt;
                
                if (sbvDictionary.ContainsKey(stockBreakout.InstrumentToken))
                {
                    sbvt = sbvDictionary[stockBreakout.InstrumentToken];
                }
                else
                {
                    sbvt = new Dictionary<TimeSpan, StockBreakoutView>();
                    sbvDictionary.Add(stockBreakout.InstrumentToken, sbvt);
                }

                StockBreakoutView sbv;

                if (sbvt.ContainsKey(stockBreakout.TimeFrame))
                {
                    sbv = sbvt[stockBreakout.TimeFrame];
                }
                else
                {
                    sbv = new StockBreakoutView()
                    {
                        InstrumentToken = stockBreakout.InstrumentToken,
                        TimeFrame = stockBreakout.TimeFrame,
                        TradingSymbol = stockBreakout.TradingSymbol
                    };
                    sbvt.Add(stockBreakout.TimeFrame, sbv);
                }

                sbv.AveragePercentageRetracement = sbv.AveragePercentageRetracement == 0? stockBreakout.PercentageRetracement:
                    (sbv.AveragePercentageRetracement * sbv.TotalBreakouts + stockBreakout.PercentageRetracement)
                    / (sbv.TotalBreakouts + 1);

                sbv.TotalBreakouts++;

                if (stockBreakout.Success)
                    sbv.SuccessCount++;

                if (stockBreakout.Completed)
                    sbv.CompletedCount++;
            }
            
            return sbvDictionary;
        }


        public async Task GetLog(uint token, int algoid)
        {
            await httpClient.GetFromJsonAsync<int>($"rbc/{token}/{algoid}");

            //await GrpcClient.GRPCLoggerAsync();

            //GrpcClient.LoggerMessageEvent += GrpcClient_LoggerMessageEvent;
            //GrpcClient.OrderAlerterEvent += GrpcClient_OrderAlerterEvent;

        }

        private void GrpcClient_OrderAlerterEvent(GrpcProtos.OrderMessage orderMessage)
        {
        }

        private void GrpcClient_LoggerMessageEvent(GrpcProtos.LogMessage logMessage)
        {
        }

        public async Task<bool> StartAlgo(int algoId, dynamic? paInput = null)
        {
            HttpResponseMessage httpResponse = null;
            if (algoId == 1)
            {
                httpResponse = await httpClient.PostAsJsonAsync("rbc", paInput as PriceActionInput);
                ArgumentNullException.ThrowIfNull(httpResponse);

                //await GetLog(256265, 1);

                
            }
            else if (algoId == 2)
            {
                //this will just start the algo. and when breakout happens alerts will update the page.
                httpResponse = await httpClient.PostAsJsonAsync("breakouts", "1");
                ArgumentNullException.ThrowIfNull(httpResponse);
            }

            if (httpResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public async Task<DateTime[]> GetInstrumentExpiriesAsync(uint btoken)
        {
            return await httpClient.GetFromJsonAsync<DateTime[]>($"rbc/{btoken}") ?? [];
        }

        public string[] GetBreakoutTimeFrames()
        {
            return new string[] { "15 min", "30 min", "1 hour", "2 hour", "Day" };
        }
    }
}
