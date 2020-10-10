using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using KiteConnect;
using System.Threading;

namespace TestWindowsApplication
{
    public partial class Kafkatest : Form
    {
        public Kafkatest()
        {
            InitializeComponent();
        }

        private void btnProduce_Click(object sender, EventArgs e)
        {
            string message = "this is the helloworld message";

            Producer producer = new Producer();

            using (producer.KProducer)
            {
                for (int i = 0; i < 1000; i++)
                {
                   //producer.KProducer.BeginProduce(
                    //new TopicPartition("Market_Ticks", new Partition(0)),
                    //new Message<byte[], byte[]>
                    //{
                    //    Key = Encoding.UTF8.GetBytes(i.ToString()),
                    //    Value = Encoding.UTF8.GetBytes(i.ToString()),
                    //    Timestamp = Timestamp.Default
                    //}
                    //);
                   // Thread.Sleep(500);
                }
                producer.KProducer.Flush(TimeSpan.FromSeconds(10));
            }
          //  Consumer consumer = new KafkaStreams.Consumer();
           // label1.Text = consumer.SubscribeToStream();

        }
    }
}
