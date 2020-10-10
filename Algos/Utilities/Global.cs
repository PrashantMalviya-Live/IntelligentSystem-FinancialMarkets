using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KiteConnect;
using GlobalLayer;

namespace Algorithms.Utilities
{
    static class Global
    {
        //public static Ticker ticker;
        public static Kite kite;
        public static System.Timers.Timer BoxTimer;
        public static decimal NetPAndL = 0;
        public static string CurrentActivity;
        public static string StrikePrices;
       // public static User CurrentUser;

        public static event PandLWatcherEventHandler PandLUpdated;
        public delegate void PandLWatcherEventHandler(object sender, PandLWatcherEventArgs e);
        static Global.PandLWatcherEventArgs args;

        public static event ActivityWatcherEventHandler ActivityUpdated;
        public delegate void ActivityWatcherEventHandler(object sender, ActivityWatcherEventArgs e);
        static Global.ActivityWatcherEventArgs aargs;


        public static void InitBoxTimer()
        {

            //BoxTimer = new System.Timers.Timer(6000000);
            //BoxTimer.Start();
            //BoxTimer.Elapsed += CheckBoxSpread;
        }
        public static void UpdatePandL(decimal plImpact)
        {
            NetPAndL += plImpact;

            args = new PandLWatcherEventArgs();
            args.NetValue = NetPAndL;

            PandLUpdated(null, args);
        }

        public static void UpdateActivity(string  activity, string strikePrices="")
        {
            CurrentActivity = activity;
            StrikePrices = strikePrices;

            aargs = new ActivityWatcherEventArgs();
            aargs.CurrentActivity = CurrentActivity;

            if(strikePrices !="")
            aargs.StrikePrices = StrikePrices;

            ActivityUpdated(null, aargs);
        }


       
        //static void CheckBoxSpread(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    Logger log = new Logger("Timer Started");
        //    StopTicker();
        //}
        //public static string StopTicker()
        //{
        //    string message;
        //    if (ticker != null)
        //    {
        //        try
        //        {
        //            ///TODO: Check for ticker.Abort();
        //            ticker.Close();
        //            message = "Ticker stopped successfully";
        //        }
        //        catch (Exception exp)
        //        {
        //            message = exp.Message;
        //        }
        //    }
        //    else
        //    {
        //        message = "Ticker not available";
        //    }
        //    return message;
        //}
        public class PandLWatcherEventArgs : EventArgs
        {
            public decimal NetValue { get; set; }

        }
        public class ActivityWatcherEventArgs : EventArgs
        {
            public string CurrentActivity { get; set; }
            public string StrikePrices { get; set; }

        }

        public static decimal CalculateDelta()
        {

            return 0;
        }
    }
}
