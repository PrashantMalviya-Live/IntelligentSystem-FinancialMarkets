using Plugin.FirebasePushNotification;
using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
namespace MarketMobile
{
    public partial class App : Application
    {
        //Market Alerts
        public const string MARKET_ALERTS = "NSEIndexAlerts";

        public App()
        {
            InitializeComponent();

            MainPage = new MainPage();
            CrossFirebasePushNotification.Current.Subscribe(MARKET_ALERTS);
            CrossFirebasePushNotification.Current.Subscribe("all");
            //string token = CrossFirebasePushNotification.Current.GetTokenAsync().Result;

            CrossFirebasePushNotification.Current.OnTokenRefresh += Current_OnTokenRefresh;
        }

        private void Current_OnTokenRefresh(object source, FirebasePushNotificationTokenEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Token: {e.Token}");
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }
    }
}
