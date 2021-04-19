using System;
using System.Windows.Forms;
using System.Collections.Generic;
using LighthouseV2PowerControl.Log;

namespace LighthouseV2PowerControl
{
    static class Program
    {
        private static Form1 app = null;
        private delegate void LogHandler(object msg, LogType type);
        private static event LogHandler programLog;
        private static LighthousePowerControl powerControl = new LighthousePowerControl();

        static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            app = new Form1();
            LogWriter.logFileName = null;
            LogWriter.ClearLogFile();
            Application.ApplicationExit += (obj, e) => powerControl.Cancel();

            programLog += (msg, type) =>
            {
                if (app != null)
                {
                    app.Log(msg, type);
                }
                LogWriter.WriteToLogFile(msg, type);
            };

            powerControl.OnLog += (msg, type) =>
            {
                programLog.Invoke(msg, type);
            };

            powerControl.OnStatusChange += (ready) =>
            {
                app.BtnActive(ready);
            };
            
            if (args.Length > 0)
            {
                app.ShowInTaskbar = false;
                app.WindowState = FormWindowState.Minimized;
                UseArgumentsAsync(args);
            }
            else
            {
                powerControl.StartAsync();
            }

            Stack<EventHandler> eventHandlers = new Stack<EventHandler>();  //Buttons from the form.
            eventHandlers.Push(new EventHandler((obj, args) => powerControl.SendActiveStatusAsync(true)));
            eventHandlers.Push(new EventHandler((obj, args) => powerControl.SendActiveStatusAsync(false)));
            eventHandlers.Push(new EventHandler((obj, args) => powerControl.AppManifest(ManifestTask.add)));
            eventHandlers.Push(new EventHandler((obj, args) => powerControl.AppManifest(ManifestTask.rm)));
            foreach (Button button in app.GetButtons())
            {
                button.Click += eventHandlers.Pop();
            }
            Application.Run(app);
        }

        #region StartupArgs
        /// <summary>
        /// Processing arguments at the start of the application.
        /// </summary>
        /// <param name="args"></param>
        private static async void UseArgumentsAsync(string[] args)
        {
            if (args[0] == "--powerOn" || args[0] == "--powerOff")
            {
                await powerControl.GetGattCharacteristicsAsync();
                if (args[0] == "--powerOn")
                {
                    await powerControl.SendActiveStatusAsync(true);
                }
                else
                {
                    await powerControl.SendActiveStatusAsync(false);
                }
            }
            else if (args[0] == "--reg")
            {
                powerControl.AppManifest(ManifestTask.add);
            }
            else if (args[0] == "--rm")
            {
                powerControl.AppManifest(ManifestTask.rm);
            }
            powerControl.Cancel();
        }
        #endregion

        private static void Log(object msg)
        {
            programLog.Invoke(msg, LogType.log);
        }

        private static void LogError(object msg)
        {
            programLog.Invoke(msg, LogType.error);
        }
    }
}
