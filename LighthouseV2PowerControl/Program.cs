using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using Valve.VR;
using System.Threading;

namespace LighthouseV2PowerControl
{
    static class Program
    {
        private static readonly Regex regex = new Regex("^LHB-.{8}");
        private static readonly Guid service = Guid.Parse("00001523-1212-efde-1523-785feabcd124");
        private static readonly Guid characteristic = Guid.Parse("00001525-1212-efde-1523-785feabcd124");
        private static readonly byte activateByte = 0x01;
        private static readonly byte deactivateByte = 0x00;
        private static List<GattCharacteristic> listGattCharacteristics = new List<GattCharacteristic>();


        public delegate void LogHandler(object msg, LogType type);
        public static event LogHandler OnLog;
        private static Form1 app = null;
        private static CVRSystem OVRSystem;
        private static Thread onQuitThread;
        private static CancellationTokenSource cancellationToken = new CancellationTokenSource();

        static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ApplicationExit += (obj, e) => cancellationToken.Cancel();
            app = new Form1();
            OnLog += (msg, type) => app.Log(msg, type);
            if (args.Length > 0)
            {
                app.ShowInTaskbar = false;
                app.WindowState = FormWindowState.Minimized;
                UseArgumentsAsync(args);
            }
            Startup();

            Stack<EventHandler> eventHandlers = new Stack<EventHandler>();  //Прсото для удобного назначения кнопок.
            eventHandlers.Push(new EventHandler((obj, args) => SendActiveStatus(true)));
            eventHandlers.Push(new EventHandler((obj, args) => SendActiveStatus(false)));
            eventHandlers.Push(new EventHandler((obj, args) => AppManifest(WithManifestTask.add)));
            eventHandlers.Push(new EventHandler((obj, args) => AppManifest(WithManifestTask.rm)));
            foreach (Button button in app.GetButtons())
            {
                button.Click += eventHandlers.Pop();
            }
            Application.Run(app);
        }

        private static async Task Startup()
        {
            await GetGattCharacteristicsAsync();
            Log($"lighthouses found: {listGattCharacteristics.Count};");
            if (listGattCharacteristics.Count > 0 && app != null)
            {
                app.BtnActive(true);
            }

            try
            {
                EVRInitError error = EVRInitError.None;
                OVRSystem = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);    //запускается и вырубается с SteamVR
                if (error == EVRInitError.Init_NoServerForBackgroundApp || error != EVRInitError.None)
                {
                    Log("Init without SteamVR;");
                }
                else
                {
                    Log("Init with SteamVR;");
                    onQuitThread = new Thread(new ThreadStart(QuitThreadChecker));
                    onQuitThread.Start();
                    await SendOnLighthouseAsync(activateByte);
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
            }
        }


        /// <summary>
        /// A thread that does not allow the application to close until the base stations are disabled
        /// </summary>
        private static void QuitThreadChecker()
        {
            while (true && !cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(100);
                VREvent_t lastEvent = new VREvent_t();
                OVRSystem.PollNextEvent(ref lastEvent,
                    (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VREvent_t)));
                if ((EVREventType)lastEvent.eventType == EVREventType.VREvent_Quit)
                {
                    OVRSystem.AcknowledgeQuit_Exiting();
                    SendOnLighthouseAsync(deactivateByte);
                    break;
                }
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                OVRSystem.AcknowledgeQuit_Exiting();
            }
            Exit();
        }

        #region Bluetooth
        /// <summary>
        /// Call once at startup to get the characteristics of all base stations.
        /// </summary>
        /// <returns></returns>
        private static async Task GetGattCharacteristicsAsync()
        {
            DeviceInformationCollection GatDevices = await DeviceInformation.FindAllAsync(GattDeviceService.GetDeviceSelectorFromUuid(service));

            for (int id = 0; id < GatDevices.Count; ++id)
            {
                if (!regex.IsMatch(GatDevices[id].Name)) continue;

                BluetoothLEDevice bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(GatDevices[id].Id);
                GattDeviceServicesResult result = await bluetoothLeDevice.GetGattServicesAsync();

                if (result.Status == GattCommunicationStatus.Success)
                {
                    IReadOnlyList<GattDeviceService> serviceList = result.Services;
                    for (int i = 0; i < serviceList.Count; ++i)
                    {
                        if (serviceList[i].Uuid != service) continue;
                        GattCharacteristicsResult gattRes;
                        do
                        {
                            gattRes = await serviceList[i].GetCharacteristicsForUuidAsync(characteristic);
                        } 
                        while (gattRes.Status == GattCommunicationStatus.AccessDenied);

                        if (gattRes.Status == GattCommunicationStatus.Success)
                        {
                            var openStatus = await serviceList[i].OpenAsync(GattSharingMode.SharedReadAndWrite);
                            IReadOnlyList<GattCharacteristic> characteristics = gattRes.Characteristics;
                            for (int j = 0; j < characteristics.Count; ++j)
                            {
                                if (characteristics[j].Uuid != characteristic) continue;
                                listGattCharacteristics.Add(characteristics[j]);
                            }
                        }
                        else
                        {
                            LogError($"Characteristics {gattRes.Status};");
                        }
                    }
                }
                else
                {
                    LogError($"Sevices {result.Status};");
                }
            }
        }

        /// <summary>
        /// Called to write the value to the characteristic for all found base stations.
        /// </summary>
        /// <param name="byte4send"></param>
        /// <returns></returns>
        private static async Task SendOnLighthouseAsync(byte byte4send)
        {
            app.BtnActive(false);
            for (int i = 0; i < listGattCharacteristics.Count; ++i)
            {
                DataWriter writer = new DataWriter();
                writer.WriteByte(byte4send);
                GattCommunicationStatus resWrite = await listGattCharacteristics[i].WriteValueAsync(writer.DetachBuffer());
                if (resWrite == GattCommunicationStatus.Success)
                {
                    Log($"Success;");
                }
                else
                {
                    LogError($"lighthouse {i + 1}: {resWrite};");
                }
            }
            app.BtnActive(true);
            if(byte4send == activateByte) return;
            cancellationToken.Cancel();    //Для выхода из треда и корректного отключения. 
        }

        public static void SendActiveStatus(bool status)
        {
            SendOnLighthouseAsync((status) ? activateByte : deactivateByte);
        }
        #endregion

        /// <summary>
        /// Processing arguments at the start of the application.
        /// </summary>
        /// <param name="args"></param>
        private static async void UseArgumentsAsync(string[] args)  
        {
            if (args[0] == "--powerOn" || args[0] == "--powerOff")
            {
                await GetGattCharacteristicsAsync();
                if (args[0] == "--powerOn")
                {
                    await SendOnLighthouseAsync(activateByte);
                }
                else
                {
                    await SendOnLighthouseAsync(deactivateByte);
                }
            }
            else if (args[0] == "--reg")
            {
                AppManifest(WithManifestTask.add);
            }
            else if (args[0] == "--rm")
            {
                AppManifest(WithManifestTask.rm);
            }
            Exit();
        }

        private static void Log(object msg)
        {
            if (app == null) return;
            OnLog.Invoke(msg, LogType.log);
        }

        private static void LogError(object msg)
        {
            if (app == null) return;
            OnLog.Invoke(msg, LogType.error);
        }

        private static void AppManifest(WithManifestTask task)
        {
            EVRInitError evrInitError = EVRInitError.None;
            OpenVR.Init(ref evrInitError, EVRApplicationType.VRApplication_Utility);
            if (evrInitError != EVRInitError.None)
            {
                LogError(evrInitError);
                return;
            }

            EVRApplicationError applicationError;
            string manifestPath = Path.Combine(Directory.GetCurrentDirectory(), "manifest.vrmanifest");
            if (task == WithManifestTask.add)
            {
                applicationError = OpenVR.Applications.AddApplicationManifest(manifestPath, false);
            }
            else
            {
                applicationError = OpenVR.Applications.RemoveApplicationManifest(manifestPath);
            }

            if (applicationError != EVRApplicationError.None)
            {
                LogError(applicationError);
                return;
            }
            Log($"Application manifest {((task == WithManifestTask.add)? "registered;" : "removed;")}");
        }

        private static void Exit()
        {
            OpenVR.Shutdown();
            Application.Exit();
        }
    }

    public enum LogType
    {
        log,
        error
    }

    public enum WithManifestTask
    {
        add,
        rm
    }
}
