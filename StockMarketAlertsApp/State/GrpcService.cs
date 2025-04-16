using Grpc.Core;
using Grpc.Net.Client.Web;
using Grpc.Net.Client;
using GrpcProtos;

namespace StockMarketAlertsApp.State
{
    public class GrpcService
    {
        private readonly AlertManager.AlertManagerClient _client;
        private CancellationTokenSource _cancellationTokenSource;

        //public delegate void LoggerMessageDelegate(LogMessage logMessage);
        //public event LoggerMessageDelegate LoggerMessageEvent;

        //public delegate void OrderAlerterDelegate(OrderMessage orderMessage);
        //public event OrderAlerterDelegate OrderAlerterEvent;

        //private readonly NotifierService _notifier = notifier;
        public delegate void AlertManagerDelegate(AlertMessage alertMessage);
        public event AlertManagerDelegate AlertGenerated;

        public GrpcService()
        {
            //_jsRuntime = jsRuntime;
            var channel = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions
            {
                HttpHandler = new GrpcWebHandler(new HttpClientHandler())
            });

            _client = new AlertManager.AlertManagerClient(channel);
        }
        //public async Task StartAlertService()
        //{
        //    await GRPCAlertManagerAsync();
        //}
        public async Task StartListeningAsync()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            await ListenForAlertsAsync(_cancellationTokenSource.Token);
        }
        private async Task ListenForAlertsAsync(CancellationToken cancellationToken)
        {
            //using var call = _client.GetAlerts(new AlertRequest());

            using AsyncDuplexStreamingCall<AlertStatus, AlertMessage> streamingCall = _client.Alert();

            await foreach (var alert in streamingCall.ResponseStream.ReadAllAsync(cancellationToken))
            {
                // Trigger JS notification when an alert is received
                AlertGenerated?.Invoke(alert);
                //await _jsRuntime.InvokeAsync<Task>("setNotification");
                //await _jsRuntime.InvokeVoidAsync("showNotification", alert.Id, alert.Message);
                
            }
        }
        public void StopListening()
        {
            _cancellationTokenSource?.Cancel();
        }

        //public async Task GRPCAlertManagerAsync()
        //{
        //    AsyncDuplexStreamingCall<AlertStatus, AlertMessage> streamingCall = client.Alert();

        //    await foreach (var message in streamingCall.ResponseStream.ReadAllAsync())
        //    {
        //        //Console.WriteLine(message.Orderid);
        //        //InvokeAsync(AlertManagerEvent, message);
        //        //InvokeAsync(() =>
        //        //{
        //       // AlertGenerated?.Invoke(message);

        //        await notifier.Update("alert", message);
        //        //    StateHasChanged();
        //        //});
        //    }
        //}
        //public void Dispose()
        //{
        //    // The following prevents derived types that introduce a
        //    // finalizer from needing to re-implement IDisposable.
        //    GC.SuppressFinalize(this);
        //}
    }
}
