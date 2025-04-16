using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace KafkaStreams
{
    public class KafkaServer
    {
        public static bool StartKafka()
        {
            //Start Zookeeper server
            // Process.Start("zkserver");

            ProcessStartInfo zookeeper = new ProcessStartInfo();


            //netstat -ano | findstr :2181
            //taskkill /PID {PID} /F

            ///DELTE ALL TOPICS
            //LOGIN TO ZOOKEEPER
            /// ./zkCli -server localhost:2181 rmr /config/topics
            /// ./ zkCli - server localhost: 2181 rmr / brokers / topics
            /// ./ zkCli - server localhost: 2181 rmr / admin / delete_topics

            //LIST ALL TOPICS
            // .\bin\windows\kafka-topics.bat --list --zookeeper localhost:2181

            zookeeper.FileName = "cmd";
            zookeeper.Arguments = "/c Zkserver";
            //zookeeper.CreateNoWindow = false;
            zookeeper.ErrorDialog = false;
            zookeeper.RedirectStandardError = false;
            zookeeper.RedirectStandardOutput = false;
            //   zookeeper.WindowStyle = ProcessWindowStyle.Hidden;
            zookeeper.UseShellExecute = false;
            Process.Start(zookeeper);

            Thread.Sleep(2000);

            ProcessStartInfo kafka = new ProcessStartInfo(@"D:\kafka\bin\windows\kafka-server-start.bat");
            kafka.Arguments = @"D:\kafka\config\server.properties";
            //kafka.CreateNoWindow = false;
            kafka.ErrorDialog = false;
            kafka.RedirectStandardError = false;
            kafka.RedirectStandardOutput = false;
            //kafka.WindowStyle = ProcessWindowStyle.Normal;
            kafka.UseShellExecute = false;
            Process.Start(kafka);

            return true;
        }
    }
}
