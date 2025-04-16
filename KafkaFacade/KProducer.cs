using System;
using System.Collections.Generic;
using System.Threading;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using GlobalLayer;

namespace KafkaFacade
{
    public class KProducer
    {
        private IProducer<string, byte[]> _producer;
        static CancellationToken cancellationToken;


        public KProducer()
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = Constants.BOOTSTRAP_SERVER,
                EnableIdempotence = true//,
                //QueueBufferingMaxMessages = 500,
            };

            _producer = new ProducerBuilder<string, byte[]>(producerConfig).Build();

            CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true; // prevent the process from terminating.
                cts.Cancel();
            };
            cancellationToken = cts.Token;

        }
        public void PublishData(string token, byte[] tickData)
        {
            _producer.Produce
              (
                  token,
                   new Message<string, byte[]>
                   {
                       Key = token,
                       Value = tickData
                   },
                   handler
               );
            //    // wait for up to 10 seconds for any inflight messages to be delivered.
            //    p.Flush(TimeSpan.FromSeconds(10));
        }
        public void PublishAllTicks(List<Tick> tickData, bool shortenedTick = false)
        {
            foreach (Tick tick in tickData)
            {
                PublishData(tick.InstrumentToken.ToString(), TickDataSchema.ParseTickBytes(tick, shortenedTick));
            }
        }

        
        public void handler(DeliveryReport<string, byte[]> d)
        {

        }
    }
}
