using System;
using System.Collections.Generic;
using System.Linq;

using System.Threading.Tasks;
using System.Windows.Forms;
using Apache.Ignite.Core;
using Apache.Ignite.Linq;
using Apache.Ignite.Service;
namespace TestWindowsApplication
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {

            //using (var ignite = Ignition.StartFromApplicationConfiguration("igniteConfiguration"))
            //{
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Form1());
            //}

        }
    }
}
