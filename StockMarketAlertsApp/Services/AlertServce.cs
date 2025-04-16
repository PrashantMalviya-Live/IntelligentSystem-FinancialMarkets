using StockMarketAlertsApp.Clients;

namespace StockMarketAlertsApp.Services
{
    public class AlertService
    {
        public event Action<string> OnAlert;

        public async void StartAlertService()
        {
            //GrpcClient.AlertManagerEvent += GrpcClient_AlertManagerEvent;

            //await GrpcClient.GRPCAlertManagerAsync();
        }
        private async void GrpcClient_AlertManagerEvent(GrpcProtos.AlertMessage alertMessage)
        {
            ShowAlert(alertMessage.Message);
        }
        public void ShowAlert(string message)
        {
            OnAlert?.Invoke(message);
        }
    }
}
