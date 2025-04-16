using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Global;

namespace KafkaStreams
{
    public class Admin
    {
        public static void CreateTopics()
        {
            using (var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = Constants.BOOTSTRAP_SERVER }).Build())
            {
                Metadata metadata = adminClient.GetMetadata(Constants.TOPIC_NAME, new TimeSpan(1000000));
                if (metadata != null && metadata.Topics != null)
                {

                    adminClient.CreateTopicsAsync(
                        new TopicSpecification[]
                        {
                        new TopicSpecification { Name = Constants.TOPIC_NAME, NumPartitions = 600, ReplicationFactor = 1 }
                        }


                    ).Wait();
                }
            }
        }
    }
}
