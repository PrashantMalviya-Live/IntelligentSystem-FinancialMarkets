using Confluent.Kafka;
using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KafkaFacade
{
    public class KSubscriber
    {
        private IConsumer<string, byte[]>  _c;
        ConsumerConfig conf;
        public KSubscriber()
        {
            conf = new ConsumerConfig
            {
                GroupId = "BackTesters",
                BootstrapServers = "localhost:9092",
                // Note: The AutoOffsetReset property determines the start offset in the event
                // there are not yet any committed offsets for the consumer group for the
                // topic/partitions of interest. By default, offsets are committed
                // automatically, so in this example, consumption will only start from the
                // earliest message in the topic 'my-topic' the first time you run the program.
                AutoOffsetReset = AutoOffsetReset.Earliest
            };
            conf.AllowAutoCreateTopics = true;
            _c = new ConsumerBuilder<string, byte[]>(conf).Build();
        }

        public void AddSubscriber(List<uint> tokens)
        {
            //_subscriber.Subscribe("");
            foreach (uint token in tokens)
            {
                _c.Subscribe(token.ToString());
                //sub.Subscribe(BitConverter.GetBytes(token));
            }
        }
        public async Task Subscribe(IKConsumer kConsumer)
        {

            //_c.Subscribe("my-topic");

            CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true; // prevent the process from terminating.
                cts.Cancel();
            };

            try
            {
                while (true)
                {
                    try
                    {
                        var cr = _c.Consume(cts.Token);
                        //Console.WriteLine($"Consumed message '{cr.Message.Value}' at: '{cr.TopicPartitionOffset}'.");

                        byte[] tickData = cr.Message.Value;

                        SendData(kConsumer, tickData);
                    }
                    catch (ConsumeException e)
                    {
                        Console.WriteLine($"Error occured: {e.Error.Reason}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Ensure the consumer leaves the group cleanly and final offsets are committed.
                _c.Close();
            }
        }
        public static void SendData(IKConsumer kConsumer, byte[] tickData)
        {
            //Tick tick = TickDataSchema.ParseTick(tickData, shortenedTick: true);
            Tick tick = TickDataSchema.ParseTick(tickData, shortenedTick: false);

            if (tick.InstrumentToken != 0 && tick.Timestamp != null)
                //algoObject.OnNext(new Tick[] { tick });
                kConsumer.OnNext(tick);
        }
    }
}
