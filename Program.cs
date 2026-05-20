using System;
using System.Windows.Forms;
using TapiMonitorApp.Contexts;

namespace TapiMonitorApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {

            // Force the environment working folder to the actual location of the executable asset
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SystemTrayApplicationContext());

            ApplicationConfiguration.Initialize();
            
            // Runs the message loop configured using a custom context class implementation instead of showing default Forms
            Application.Run(new SystemTrayApplicationContext());
        }
    }
}
