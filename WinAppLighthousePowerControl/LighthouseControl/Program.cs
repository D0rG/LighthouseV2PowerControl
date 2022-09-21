using System.Windows.Forms;
using LighthousePowerControl;

namespace WinAppLighthousePowerControl
{
    static class Program
    {
        internal static LighthouseV2PowerControl powerControl = new LighthouseV2PowerControl();
        internal static Form1 app = null;
        internal static string[] startupArgs;

        static void Main(string[] args)
        {
            startupArgs = args;
            powerControl.onAppQuit += Application.Exit;
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            app = new Form1();
            Application.ApplicationExit += (obj, e) => powerControl.Cancel();
            Application.Run(app);
        }
    }
}
