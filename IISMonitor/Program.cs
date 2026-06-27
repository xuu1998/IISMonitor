using System;
using System.ServiceProcess;
using System.Windows.Forms;

namespace IISMonitor
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool runAsService = args.Length > 0 && args[0].Equals("--service", StringComparison.OrdinalIgnoreCase);

            if (runAsService)
            {
                ServiceBase[] ServicesToRun = new ServiceBase[]
                {
                    new IISMonitorService()
                };
                ServiceBase.Run(ServicesToRun);
            }
            else
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}
