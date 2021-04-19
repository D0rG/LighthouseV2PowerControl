using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using LighthouseV2PowerControl.Log;
using Valve.VR;


namespace LighthouseV2PowerControl
{
    class LighthousePowerControl
    {
        private readonly Regex regex = new Regex("^LHB-.{8}");
        private readonly Guid service = Guid.Parse("00001523-1212-efde-1523-785feabcd124");
        private readonly Guid characteristic = Guid.Parse("00001525-1212-efde-1523-785feabcd124");
        private readonly byte activateByte = 0x01;
        private readonly byte deactivateByte = 0x00;
        private List<GattCharacteristic> listGattCharacteristics = new List<GattCharacteristic>();
        public delegate void LogHandler(object msg, LogType type);
        public event LogHandler OnLog;
        public delegate void StatusHandler(bool ready);
        public event StatusHandler OnStatusChange;
        private CVRSystem OVRSystem;
        private Thread onQuitThread;
        private CancellationTokenSource cancellationToken = new CancellationTokenSource();

        public LighthousePowerControl()
        {

        }

        public async void StartAsync()
        {
            await GetGattCharacteristicsAsync();
            Log($"lighthouses found: {listGattCharacteristics.Count};");
            await StartSteamVR();
            if(listGattCharacteristics.Count > 0) OnStatusChange.Invoke(true);
        }

        public void Cancel()
        {
            cancellationToken.Cancel();
        }

        private async Task StartSteamVR()
        {
            try
            {
                EVRInitError error = EVRInitError.None;
                OVRSystem = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);    //starts and shuts down with SteamVR.
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
        private void QuitThreadChecker()
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
                Thread.Sleep(100);
            }
            OpenVR.Shutdown();
            Application.Exit();
        }

        #region Bluetooth
        /// <summary>
        /// Call once at startup to get the characteristics of all base stations.
        /// </summary>
        /// <returns></returns>
        public async Task GetGattCharacteristicsAsync()
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
        private async Task SendOnLighthouseAsync(byte byte4send)
        {
            OnStatusChange.Invoke(false);
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
            OnStatusChange.Invoke(true);
            if (byte4send == deactivateByte) //Для выхода из треда и корректного отключения. 
            {
                cancellationToken.Cancel();
            }
            return;
        }

        public void AppManifest(ManifestTask task)
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
            if (task == ManifestTask.add)
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
            Log($"Application manifest {((task == ManifestTask.add) ? "registered;" : "removed;")}");
        }

        public async Task SendActiveStatusAsync(bool status)
        {
            await SendOnLighthouseAsync((status) ? activateByte : deactivateByte);
        }
        #endregion

        private void Log(object msg)
        {
            OnLog.Invoke(msg, LogType.log);
        }

        private void LogError(object msg)
        {
            OnLog.Invoke(msg, LogType.error);
        }
    }

    public enum ManifestTask
    {
        add,
        rm
    }
}
