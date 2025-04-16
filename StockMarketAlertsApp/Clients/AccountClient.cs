using StockMarketAlertsApp.Models;
using System.Net.Http;

namespace StockMarketAlertsApp.Clients
{
    public class AccountClient(HttpClient httpClient)
    {
        public async Task<BrokerLoginParams[]> GetUserBrokerLogins(string userId, BrokerLoginParams brokerLoginParams)
        {
            return await httpClient.GetFromJsonAsync<BrokerLoginParams[]>($"Account/BrokerLogins/{userId}") ?? [];
        }

    }
}
