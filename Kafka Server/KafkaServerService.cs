using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using KafkaStreams;
namespace Kafka_Server
{
    public partial class KafkaServerService : ServiceBase
    {
        public KafkaServerService()
        {
            InitializeComponent();

            if (!System.Diagnostics.EventLog.SourceExists("KafkaServer"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "KafkaServer", "KafkaServerLog");
            }
            eventLog1.Source = "KafkaServer";
            eventLog1.Log = "KafkaServerLog";
        }

        protected override void OnStart(string[] args)
        {
            KafkaServer.StartKafka();
            //Admin.CreateTopics();
        }

        protected override void OnStop()
        {
        }
    }
}
