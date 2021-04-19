using System;
using System.IO;

namespace LighthouseV2PowerControl.Log
{
    static class LogWriter
    {
        private static string _logFileName = string.Empty;
        public static string logFileName
        {
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _logFileName = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                }
                else
                {
                    _logFileName = value;
                }
            }
            get
            {
                return _logFileName;
            }
        }

        public static void ClearLogFile()
        {
            File.WriteAllText(logFileName, null);
        }

        public static void WriteToLogFile(object msg, LogType type = LogType.log)
        {
            string res = $"[{DateTime.Now}]";
            res += "[" + ((type == LogType.log) ? "Log" : "ERROR") + "] ";
            res += msg.ToString();
            File.AppendAllText(logFileName, res + "\n");
        }
    }

    public enum LogType
    {
        log,
        error
    }
}
