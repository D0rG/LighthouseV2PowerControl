using System.Windows.Forms;

namespace LighthouseV2PowerControl
{
    static class Program
    {
        internal static LighthousePowerControl powerControl = new LighthousePowerControl();
        internal static Form1 app = null;

        static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            app = new Form1();
            Application.ApplicationExit += (obj, e) => powerControl.Cancel();
            Application.Run(app);
        }
    }
}
