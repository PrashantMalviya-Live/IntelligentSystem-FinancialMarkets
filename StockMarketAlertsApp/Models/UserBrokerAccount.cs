namespace StockMarketAlertsApp.Models
{
    public class UserBrokerAccount
    {
        public required string UserId { get; set; }
        public int ClientId { get; set; }
        public required string UserName { get; set; }
        public string? APIKey { get; set; }
        public string? AppSecret { get; set; }
        public string? ConsumerKey { get; set; }
        public string? SessionToken { get; set; }
        public string? BaseUrl { get; set; }
        public string? SId { get; set; }
        public string? HsServerId { get; set; }
        public string? AccessToken { get; set; }
        public int BrokerId { get; set; }

        public bool LoggedIn { get; set; } = false;
        public bool IsActive { get; set; } = true;

    }

}
