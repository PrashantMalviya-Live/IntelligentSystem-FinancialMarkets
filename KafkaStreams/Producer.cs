using System;
using System.Text;
using System.Collections.Generic;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using System.Threading;
using Global;

namespace KafkaStreams
{
    public class KProducer
    {

        // public Confluent.Kafka.Producer<byte[], byte[]> _kProducer;

        private Confluent.Kafka.Producer<uint, byte[]> _producer;
        static CancellationToken cancellationToken;
        public KProducer()
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = Constants.BOOTSTRAP_SERVER,
                EnableIdempotence = true,
                QueueBufferingMaxMessages = 500,
            };

            _producer = (Confluent.Kafka.Producer<uint, byte[]>)
                new ProducerBuilder<uint, byte[]>(producerConfig).Build();


            CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true; // prevent the process from terminating.
                cts.Cancel();
            };
            cancellationToken = cts.Token;

        }

        public void Publish(List<Tick> ticks)
        {
            //Dictionary<uint, TopicPartition> instrumentPartitions = new Dictionary<uint, TopicPartition>();

            foreach (Tick t in ticks)
            {
                uint iToken = t.InstrumentToken;
                //if (!instrumentPartitions.ContainsKey(iToken))
                //    instrumentPartitions.Add(iToken, new TopicPartition(Constants.TOPIC_NAME, new Partition(10)));

                //_producer.ProduceAsync
                //   (
                //       iToken.ToString(),
                //        new Message<uint, byte[]>
                //        {
                //            Key = iToken,
                //            Value = TickDataSchema.ParseTickBytes(new Tick[] { t }), //Data.Skip(8).ToArray(), //bytes for count, length and instrument token skipped. 
                //            Timestamp = Timestamp.Default
                //        },
                //        cancellationToken
                //    );

                _producer.Produce
                  (
                      iToken.ToString(),
                       new Message<uint, byte[]>
                       {
                           Key = iToken,
                           Value = TickDataSchema.ParseTickBytes(t), //Data.Skip(8).ToArray(), //bytes for count, length and instrument token skipped. 
                            Timestamp = Timestamp.Default
                       },
                       handler
                   );
            }

            //KProducer.Flush(TimeSpan.FromSeconds(10));
        }
        public void handler(DeliveryReport<uint, byte[]> d)
        {

        }

    }
}
