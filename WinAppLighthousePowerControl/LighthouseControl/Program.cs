using System.Windows.Forms;
using LighthousePowerControl;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System;

namespace WinAppLighthousePowerControl
{
    static class Program
    {
        internal static LighthouseV2PowerControl powerControl = new LighthouseV2PowerControl();
        internal static Form1 app = null;
        internal static string[] startupArgs;
        internal static string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");

        static void Main(string[] args)
        {
            powerControl.onAppQuit += Application.Exit;
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ApplicationExit += (obj, e) => powerControl.Cancel();

            if (args.Length == 0)
            {
                app = new Form1();
                Application.Run(app);
            }
            else
            {
                foreach (string argument in args)
                {
                    var TrimArgument = argument.Trim();
                    if (TrimArgument == "--reg" || TrimArgument == "--rm")
                    {
                        powerControl.AppManifest((TrimArgument == "--reg") ? ManifestTask.add : ManifestTask.rm);
                        Application.Exit();
                    }
                    else if (TrimArgument == "--powerOn" || TrimArgument == "--powerOff")
                    {
                        if (TrimArgument == "--powerOn")
                        {
                            powerControl.UpdateLighthouseListAsync().ContinueWith(task => AfterUpdateLighthouseList(task, true));
                        }
                        else
                        {
                            powerControl.UpdateLighthouseListAsync().ContinueWith(task => AfterUpdateLighthouseList(task, false));
                        }
                    }
                }
                Application.Run();
            }
        }

        internal static void AfterUpdateLighthouseList(Task<List<TaskResultAndMessage>> arg, bool activate)
        {
            if (arg.Result.Count == 0)
            {
                if (activate)
                {
                    powerControl.ActivateAllLighthouseAsync().ContinueWith(AfterLighthouseChangeState);
                }
                else
                {
                    powerControl.DeactivateAllLighthouseAsync().ContinueWith(AfterLighthouseChangeState);
                }
            }
        }

        internal static void AfterLighthouseChangeState(Task tast)
        {
            Application.Exit();
        }

        internal static void Log(string message)
        {
            File.AppendAllTextAsync(logFilePath, $"[{DateTime.UtcNow}] {message};\n");
        }
    }
}
