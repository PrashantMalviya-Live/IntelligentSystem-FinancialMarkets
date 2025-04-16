using Plugin.FirebasePushNotification;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace MarketMobile
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
           // CrossFirebasePushNotification.Current.OnTokenRefresh += Current_OnTokenRefresh;
            //Task token = CrossFirebasePushNotification.Current.GetTokenAsync().ContinueWith( (result) =>
            //{
            //    string t = result.Result;
            //    Label label = new Label
            //    {
            //        Text = t,
            //        HorizontalOptions = LayoutOptions.Center,
            //        VerticalOptions = LayoutOptions.Center
            //    };
            //    this.Content = label;
            //    // return;
            //});
        }
        //private void Current_OnTokenRefresh(object source, FirebasePushNotificationTokenEventArgs e)
        //{
        //    System.Diagnostics.Debug.WriteLine($"Token: {e.Token}");

        //    //Label label = new Label
        //    //{
        //    //    Text = e.Token,
        //    //    HorizontalOptions = LayoutOptions.Center,
        //    //    VerticalOptions = LayoutOptions.Center
        //    //};
        //    //this.Content = label;
        //}
    }
}
