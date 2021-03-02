using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.ApplicationModel.Email.DataProvider;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace LighthouseV2PowerControl
{
    static class Program
    {
        private static Guid service = Guid.Parse("00001523-1212-efde-1523-785feabcd124");
        private static Guid characteristic = Guid.Parse("00001525-1212-efde-1523-785feabcd124");
        private static byte activateByte = 0x01;
        private static byte deactivateByte = 0x00;
        private static List<GattCharacteristic> listGattCharacteristics = new List<GattCharacteristic>();

        private static Regex regex = new Regex("^LHB-.{8}");

        public delegate void LogHandler(object msg, LogType type);
        public static event LogHandler OnLog;
        private static Form1 app = null;

        [STAThread]
        static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            app = new Form1();
            OnLog += (msg, type) => app.Log(msg, type);
            Stack<EventHandler> eventHandlers = new Stack<EventHandler>();
            eventHandlers.Push(new EventHandler((obj, args) => SendActiveStatus(true)));
            eventHandlers.Push(new EventHandler((obj, args) => SendActiveStatus(false)));
            foreach (Button button in app.GetButtons())
            {
                button.Click += eventHandlers.Pop();
            }

            if (args.Length > 0)
            {
                app.ShowInTaskbar = false;
                app.WindowState = FormWindowState.Minimized;
                UseArgumentsAsync(args);
            }
            else
            {
                GetGattCharacteristicsAsync();
            }

            Application.Run(app);
        }

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
                        GattCharacteristicsResult gattRes = await serviceList[i].GetCharacteristicsForUuidAsync(characteristic);
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

            Log($"lighthouses found: {listGattCharacteristics.Count};");
            if (listGattCharacteristics.Count > 0 && app != null)
            {
                app.BtnActive(true);
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
        }


        private static async void UseArgumentsAsync(string[] args)
        {
            await GetGattCharacteristicsAsync();
            if (args[0] == "--powerOn")
            {
                await SendOnLighthouseAsync(activateByte);
            }
            else if (args[0] == "--powerOff")
            {
                await SendOnLighthouseAsync(deactivateByte);
            }
            Application.Exit();
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

        public static void SendActiveStatus(bool status)
        {
            SendOnLighthouseAsync((status)? activateByte : deactivateByte);
        }
    }

    public enum LogType
    {
        log,
        error
    }
}
