using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TIBCO_FTL_MQ_Services;
using TIBCO.FTL;
using TIBCO.FTL.GROUP;

namespace TestWindowsApplication
{
    public partial class TIBCO_TEST : Form
    {
        public TIBCO_TEST()
        {
            InitializeComponent();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            FTLServer.Start();

            string[] args = new string[1];
            string realmServer = "http://localhost:8080";
            args[0] = realmServer;

            TibSend tibSend = new TibSend(args);
            tibSend.Send();
            TibRecv tibRecv = new TibRecv(args);
            tibRecv.Recv();
        }
    }
}
