namespace StockMarketAlertsApp.Models
{
    public class ZerodhaLoginResponse
    {
        public required string message { get; set; }
        public required bool login { get; set; }
        public required string url { get; set; }
    }
}
