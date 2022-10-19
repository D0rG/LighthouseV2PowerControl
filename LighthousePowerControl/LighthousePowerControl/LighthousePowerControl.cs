using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Windows.Storage.Streams;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Valve.VR;
using System.Linq;

namespace LighthousePowerControl
{
    public sealed class LighthouseV2PowerControl
    {
        private readonly Regex _lighthouseNameRegex = new Regex("^LHB-.{8}");
        private readonly Guid _serviceGuid = Guid.Parse("00001523-1212-efde-1523-785feabcd124");
        private readonly Guid _characteristicGuid = Guid.Parse("00001525-1212-efde-1523-785feabcd124");
        private readonly byte _activateByte = 0x01;
        private readonly byte _deactivateByte = 0x00;
        private List<GattCharacteristic> _listGattCharacteristics = new List<GattCharacteristic>();
        private CVRSystem _OVRSystem;
        private Thread _onQuitThread;
        private CancellationTokenSource _cancellationToken = new CancellationTokenSource();
        public event Action onAppQuit;  //Need 4 quit app with steamvr

        #region SteamVR connection
        /// <summary>
        /// If SteamVR open connetc to steamvr 4 deactivate with VR.
        /// </summary>
        /// <returns></returns>
        public TaskResultAndMessage ConnectToSteamVR()
        {
            TaskResultAndMessage result;

            try
            {
                EVRInitError error = EVRInitError.None;
                _OVRSystem = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);    //starts and shuts down with SteamVR.
                if (error == EVRInitError.Init_NoServerForBackgroundApp || error != EVRInitError.None)
                {
                    result.message = "Init without SteamVR;";
                    result.result = TaskResult.failure;
                }
                else
                {
                    result.message = "Init with SteamVR;";
                    result.result = TaskResult.success;
                    _onQuitThread = new Thread(new ThreadStart(QuitThreadChecker));
                    _onQuitThread.Start();
                }
            }
            catch (Exception e)
            {
                result.result = TaskResult.failure;
                result.message = e.Message;
                return result;
            }

            return result;
        }

        #region Threading
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
                    SendOnAllLighthouseAsync(_deactivateByte).Wait();
                    break;
                }
            }

            while (!_cancellationToken.IsCancellationRequested)
            {
                _OVRSystem.AcknowledgeQuit_Exiting();
                Thread.Sleep(100);
            }
            OpenVR.Shutdown();
            onAppQuit?.Invoke();
        }

        /// <summary>
        /// Call it with AppExit
        /// </summary>
        public void Cancel()
        {
            _cancellationToken.Cancel();
        }
        #endregion

        #endregion

        #region Bluetooth
        /// <summary>
        /// Call once at startup to get the gatt characteristics of all base stations. taskResults.Count == 0 if Success
        /// </summary>
        /// <returns></returns>
        public async Task<List<TaskResultAndMessage>> UpdateLighthouseListAsync()
        {
            DeviceInformationCollection allGattDevices = await DeviceInformation.FindAllAsync(GattDeviceService.GetDeviceSelectorFromUuid(_serviceGuid));
            var gattDevices = allGattDevices
                .Where(x => _lighthouseNameRegex.IsMatch(x.Name));

            List<TaskResultAndMessage> taskResults = new List<TaskResultAndMessage>(allGattDevices.Count);
            foreach (var gatDevice in gattDevices)
            {
                BluetoothLEDevice bluetoothDevice = await BluetoothLEDevice.FromIdAsync(gatDevice.Id);
                GattDeviceServicesResult getGattServicesResult = await bluetoothDevice.GetGattServicesAsync();
                if (getGattServicesResult.Status == GattCommunicationStatus.Success)
                {
                    var gattServiceList = getGattServicesResult.Services
                        .Where(x => x.Uuid == _serviceGuid);
                    foreach (var gattService in gattServiceList)
                    {
                        GattCharacteristicsResult gattReslult;
                        do
                        {
                            gattReslult = await gattService.GetCharacteristicsForUuidAsync(_characteristicGuid);
                        }
                        while (gattReslult.Status == GattCommunicationStatus.AccessDenied); //mb add try conunt here.

                        if (gattReslult.Status == GattCommunicationStatus.Success)
                        {
                            await gattService.OpenAsync(GattSharingMode.SharedReadAndWrite);
                            var listGattCharacteristics = gattReslult.Characteristics
                                .Where(x => x.Uuid == _characteristicGuid);
                            foreach (var characteristic in listGattCharacteristics)
                            {
                                _listGattCharacteristics.Add(characteristic);
                            }
                        }
                        else
                        {
                            taskResults.Add(new TaskResultAndMessage
                            {
                                result = TaskResult.failure,
                                message = $"Characteristics {gattReslult.Status};"
                            });
                        }
                    }
                }
                else
                {
                    taskResults.Add(new TaskResultAndMessage
                    {
                        result = TaskResult.failure,
                        message = $"Sevices {getGattServicesResult.Status};"
                    });
                }
            }
            return taskResults;
        }

        /// <summary>
        /// Called to write the value to the characteristic for all found base stations.
        /// </summary>
        /// <param name="activate"></param>
        /// <returns></returns>
        private async Task<List<TaskResultAndMessage>> SendOnAllLighthouseAsync(byte byte4send)
        {
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
                    taskResult.message = $"Lighthouse {i + 1}:" + ((byte4send == _activateByte) ? " has started;" : " stopped;");
                }
                else
                {
                    taskResult.result = TaskResult.failure;
                    taskResult.message = $"Lighthouse {i + 1}: {resWrite};";
                }
                taskResults.Add(taskResult);
            }
            if (byte4send == _deactivateByte) //Для выхода из треда и корректного отключения. 
            {
                _cancellationToken.Cancel();
            }
            return taskResults;
        }
    
        public async Task<List<TaskResultAndMessage>> ActivateAllLighthouseAsync()
        {
            return await SendOnAllLighthouseAsync(_activateByte);
        }

        public async Task<List<TaskResultAndMessage>> DeactivateAllLighthouseAsync()
        {
            return await SendOnAllLighthouseAsync(_deactivateByte);
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
}
