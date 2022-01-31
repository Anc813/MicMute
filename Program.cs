using System;
using System.Windows.Forms;

namespace MicMute
{
    static class Program
    {
        public static MainForm mf = null;


        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            mf = new MainForm();
            Application.Run(mf);


        }
    }
}
