using Microsoft.JSInterop;

namespace StockMarketAlertsApp.State
{
    public class NotifierService (IJSRuntime JS)
    {
        
        public async Task Update(string key, GrpcProtos.AlertMessage alertMessage)
        {
            ShowNotification();
            if (Notify != null)
            {
                await Notify.Invoke(key, alertMessage);

            }
        }

        public event Func<string, GrpcProtos.AlertMessage, Task>? Notify;

        private async Task SetNotification()
        {

            // result = await JS.InvokeAsync<string>(
            //     "showPrompt1", "What's your name?");

            await JS.InvokeAsync<Task>("askNotificationPermission");
            //StateHasChanged();

        }

        private async Task ShowNotification()
        {

            // result = await JS.InvokeAsync<string>(
            //     "showPrompt1", "What's your name?");

            await JS.InvokeVoidAsync("setNotification");
            //StateHasChanged();

        }
    }

}
