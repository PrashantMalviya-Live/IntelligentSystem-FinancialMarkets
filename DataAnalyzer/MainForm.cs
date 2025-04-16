using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace DataAnalyzer
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void btnDelta_Click(object sender, EventArgs e)
        {
            DateTime expiry = Convert.ToDateTime("2021-12-23");

            decimal strikePriceFrom = 37000;
            decimal strikePriceTo = 34000;



        }
    }
}
