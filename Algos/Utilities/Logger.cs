using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Reflection;

namespace Algorithms.Utilities
{
    public class Logger1
    {
        public static event LogWatcherEventHandler LogChanged;
        public delegate void LogWatcherEventHandler(object sender, LogWatcherEventArgs e);
        static LogWatcherEventArgs args;

         private static string m_exePath = string.Empty;
         public Logger1(string logMessage)
         {
             LogWrite(logMessage);
         }
         public static void LogWrite(string logMessage)
         {
             m_exePath  =  @"D:\\Prashant\\ZerodhaAutoAlgo\\ZerodhaAutoAlgo\\ZerodhaAutoAlgo\\Logs";
             try
             {
                 using (StreamWriter w = File.AppendText(m_exePath + "\\" + "log.txt"))
                 {
                     Log(logMessage, w);
                 }
             }
             catch
             {
             }
         }
         public static void Log(string logMessage, TextWriter txtWriter)
         {
             try
             {
                 logMessage = string.Format("\r\n{0} {1} : {2}", DateTime.Now.ToShortDateString(),
                     DateTime.Now.ToLongTimeString(), logMessage);

                 txtWriter.WriteLine(logMessage);

                 args = new LogWatcherEventArgs();
                 args.Content = logMessage;

                 LogChanged(null, args);
             }
             catch
             {
             }
         }
    }
    public class LogWatcherEventArgs : EventArgs
    {
        public string Content { get; set; }
        
    }
}