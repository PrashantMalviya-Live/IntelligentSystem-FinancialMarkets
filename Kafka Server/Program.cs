using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using KafkaStreams;
namespace Kafka_Server
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {

            if (Debugger.IsAttached)
            {
                KafkaServer.StartKafka();
                System.Threading.Thread.Sleep(500000);
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                new KafkaServerService()
                };
                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}
