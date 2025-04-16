using Humanizer;
using Microsoft.AspNetCore.SignalR;
using StockMarketAlertsApp.Models;

namespace StockMarketAlertsApp.Clients
{
    public class TradesClient (HttpClient httpClient)
    {

        public async Task<List<PositionSummary>> GetUserPositionsAsync(string  userid, int brokerid)
        {
            List<PositionSummary> userPositions = await httpClient.GetFromJsonAsync<List<PositionSummary>>($"positions/{userid}/{brokerid}") ?? new List<PositionSummary>();


            //PositionSummary[] userPositions = [
            //new (){Id=1, TradingSymbol="NIFTY", Quantity=25, SellPrice=23500, TodayPNL=1000, PNL=2000, Target=23000, StopLoss = 25000, BuyQuantity=20, SellQuantity=0 },
            //new (){Id=2,  TradingSymbol="BankNIFTY", Quantity=25, SellPrice=23500, TodayPNL=1000, PNL=2000, Target=23000, StopLoss = 2500 , BuyQuantity=20, SellQuantity=0 },
            //new (){Id=3,  TradingSymbol="TCS", Quantity=25, SellPrice=23500, TodayPNL=1000, PNL=2000, Target=23000, StopLoss = 250 , BuyQuantity=20, SellQuantity=0 },
            //new (){Id=4,  TradingSymbol="TataNIFTY", Quantity=25, SellPrice=23500, TodayPNL=1000, PNL=2000, Target=23000, StopLoss = 52  , BuyQuantity=20, SellQuantity=0},
            //];
            return userPositions;
        }
        

        
    }
}
