namespace StockMarketAlertsApp.Models
{
    public class KotakLoginParam
    {
        public string userid { get; set; }
        public string pwd { get; set; }
        public string otp { get; set; }
        public string accessToken { get; set; }
        public string applicationUserId { get; set; }
    }
    public class KotakLoginResponse
    {
        public string? userName { get; set; }
        public string? message { get; set; }
        public int statusCode { get; set; }
    }
}
