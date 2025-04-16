using System;
using System.Text;
using Confluent.Kafka;
using Confluent.SchemaRegistry;

namespace KafkaStream
{
    public class Producer
    {

        public Confluent.Kafka.Producer<byte[], byte[]> _producer;

        public Producer()
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = "localhost:65533",
                EnableIdempotence = true
            };

            _producer = (Confluent.Kafka.Producer<byte[], byte[]>)
                new ProducerBuilder<byte[], byte[]>(producerConfig).Build();
        }
      
        public void BeginStreaming (string ticks)
        {
            using (_producer)
            {
                //using (var adminClient = new AdminClient(_producer.Handle))
                //{
                //    try
                //    {
                //        var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(3));
                //    }
                //    catch (KafkaException e)
                //    {
                //        //  Assert.Equal(ErrorCode.Local_Transport, e.Error.Code);
                //    }
                //}
                    _producer.BeginProduce(
                    new TopicPartition("First Topic", new Partition(0)),
                    new Message<byte[], byte[]>
                    {
                        Key = Encoding.UTF8.GetBytes("hello world"),
                        Value = Encoding.UTF8.GetBytes("Hello value world"),
                        Timestamp = Timestamp.Default
                    }
                    );
                _producer.Flush(TimeSpan.FromSeconds(10));
            }

        }

    }
}
