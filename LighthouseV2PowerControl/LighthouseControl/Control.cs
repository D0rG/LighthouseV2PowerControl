using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Windows.Storage.Streams;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Valve.VR;


namespace LighthouseV2PowerControl
{
    class LighthousePowerControl
    {
        private readonly Regex _lighthuiuseRegex = new Regex("^LHB-.{8}");
        private readonly Guid _service = Guid.Parse("00001523-1212-efde-1523-785feabcd124");
        private readonly Guid _characteristic = Guid.Parse("00001525-1212-efde-1523-785feabcd124");
        private readonly byte _activateByte = 0x01;
        private readonly byte _deactivateByte = 0x00;
        private List<GattCharacteristic> _listGattCharacteristics = new List<GattCharacteristic>();
        public delegate void StatusHandler(bool ready);
        private CVRSystem _OVRSystem;
        private Thread _onQuitThread;
        private CancellationTokenSource _cancellationToken = new CancellationTokenSource();

        public async Task<List<TaskResultAndMessage>> StartAsync()
        {
            var results = new List<TaskResultAndMessage>();
            await GetGattCharacteristicsAsync();
            results.Add(StartSteamVR().Result);
            results.Add(new TaskResultAndMessage
            {
                result = (_listGattCharacteristics.Count > 0) ? TaskResult.success : TaskResult.failure,
                message = $"lighthouses found: {_listGattCharacteristics.Count};"
            });
            return results;
        }

        public void Cancel()
        {
            _cancellationToken.Cancel();
        }

        private async Task<TaskResultAndMessage> StartSteamVR()
        {
            TaskResultAndMessage result;

            try
            {
                EVRInitError error = EVRInitError.None;
                _OVRSystem = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);    //starts and shuts down with SteamVR.
                if (error == EVRInitError.Init_NoServerForBackgroundApp || error != EVRInitError.None)
                {
                    result.message = "Init without SteamVR;";
                }
                else
                {
                    result.message = "Init without SteamVR;";
                    _onQuitThread = new Thread(new ThreadStart(QuitThreadChecker));
                    _onQuitThread.Start();
                    await SendOnAllLighthouseAsync(true);
                }
                result.result = TaskResult.success;
            }
            catch (Exception e)
            {
                result.result = TaskResult.failure;
                result.message = e.Message;
            }

            return result;
        }

        /// <summary>
        /// A thread that does not allow the application to close until the base stations are disabled
        /// </summary>
        private void QuitThreadChecker()
        {
            while (true && !_cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(100);
                VREvent_t lastEvent = new VREvent_t();
                _OVRSystem.PollNextEvent(ref lastEvent,
                    (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VREvent_t)));
                if ((EVREventType)lastEvent.eventType == EVREventType.VREvent_Quit)
                {
                    _OVRSystem.AcknowledgeQuit_Exiting();
                    SendOnAllLighthouseAsync(false);
                    break;
                }
            }

            while (!_cancellationToken.IsCancellationRequested)
            {
                _OVRSystem.AcknowledgeQuit_Exiting();
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
        private async Task<List<TaskResultAndMessage>> GetGattCharacteristicsAsync()
        {
            DeviceInformationCollection GatDevices = await DeviceInformation.FindAllAsync(GattDeviceService.GetDeviceSelectorFromUuid(_service));
            List<TaskResultAndMessage> taskResults = new List<TaskResultAndMessage>(GatDevices.Count);

            for (int id = 0; id < GatDevices.Count; ++id)
            {
                if (!_lighthuiuseRegex.IsMatch(GatDevices[id].Name)) continue;

                BluetoothLEDevice bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(GatDevices[id].Id);
                GattDeviceServicesResult result = await bluetoothLeDevice.GetGattServicesAsync();

                if (result.Status == GattCommunicationStatus.Success)
                {
                    IReadOnlyList<GattDeviceService> serviceList = result.Services;
                    for (int i = 0; i < serviceList.Count; ++i)
                    {
                        if (serviceList[i].Uuid != _service) continue;
                        GattCharacteristicsResult gattRes;
                        do
                        {
                            gattRes = await serviceList[i].GetCharacteristicsForUuidAsync(_characteristic);
                        }
                        while (gattRes.Status == GattCommunicationStatus.AccessDenied);

                        if (gattRes.Status == GattCommunicationStatus.Success)
                        {
                            var openStatus = await serviceList[i].OpenAsync(GattSharingMode.SharedReadAndWrite);
                            IReadOnlyList<GattCharacteristic> characteristics = gattRes.Characteristics;
                            for (int j = 0; j < characteristics.Count; ++j)
                            {
                                if (characteristics[j].Uuid != _characteristic) continue;
                                _listGattCharacteristics.Add(characteristics[j]);
                            }
                        }
                        else
                        {
                            taskResults.Add(new TaskResultAndMessage
                            {
                                result = TaskResult.failure,
                                message = $"Characteristics {gattRes.Status};"
                            });
                        }
                    }
                }
                else
                {
                    taskResults.Add(new TaskResultAndMessage
                    {
                        result = TaskResult.failure,
                        message = $"Sevices {result.Status};"
                    });
                }
            }
            return taskResults;
        }

        /// <summary>
        /// Called to write the value to the characteristic for all found base stations.
        /// </summary>
        /// <param name="byte4send"></param>
        /// <returns></returns>
        public async Task<List<TaskResultAndMessage>> SendOnAllLighthouseAsync(bool activate)
        {
            byte byte4send = activate ? _activateByte : _deactivateByte;
            List<TaskResultAndMessage> taskResults = new List<TaskResultAndMessage>(_listGattCharacteristics.Count);

            for (int i = 0; i < _listGattCharacteristics.Count; ++i)
            {
                TaskResultAndMessage taskResult;
                DataWriter writer = new DataWriter();
                writer.WriteByte(byte4send);
                GattCommunicationStatus resWrite = await _listGattCharacteristics[i].WriteValueAsync(writer.DetachBuffer());
                if (resWrite == GattCommunicationStatus.Success)
                {
                    taskResult.result = TaskResult.success;
                    taskResult.message = String.Empty;
                }
                else
                {
                    taskResult.result = TaskResult.failure;
                    taskResult.message = $"lighthouse {i + 1}: {resWrite};";
                }
                taskResults.Add(taskResult);
            }
            if (byte4send == _deactivateByte) //Для выхода из треда и корректного отключения. 
            {
                _cancellationToken.Cancel();
            }
            return taskResults;
        }

        public TaskResultAndMessage AppManifest(ManifestTask task)
        {
            TaskResultAndMessage result;
            result.result = TaskResult.failure;
            EVRInitError evrInitError = EVRInitError.None;
            OpenVR.Init(ref evrInitError, EVRApplicationType.VRApplication_Utility);
            if (evrInitError != EVRInitError.None)
            {
                result.message = evrInitError.ToString();
                return result;
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
                result.message = applicationError.ToString();
                return result;
            }

            result.result = TaskResult.success;
            result.message = $"Application manifest {((task == ManifestTask.add) ? "registered;" : "removed;")}";
            return result;
        }
        #endregion
    }

    public enum ManifestTask
    {
        add,
        rm
    }

    public enum TaskResult
    {
        success,
        failure
    }

    public struct TaskResultAndMessage
    {
        public TaskResult result;
        public string message;
    }
}
