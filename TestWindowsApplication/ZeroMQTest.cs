using Global;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZMQFacade;
namespace TestWindowsApplication
{
    public partial class ZeroMQTest : Form
    {
        public ZeroMQTest()
        {
            InitializeComponent();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            //ZMQClient.HWClient(new string[] { "Receive" });
            //ZMQServer.MQServer(new string[] { "Send" });
            //ZMQServer.MQServer(null);

            //ZMQServer.PublishAllTicks(new byte[10]);
        }

        private void btnReceive_Click(object sender, EventArgs e)
        {
            //ZMQClient.ZMQStorageClient("2");
        }
    }
}
