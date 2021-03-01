using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;


namespace LighthouseV2PowerControl
{
    public partial class Form1 : Form
    {
        private Guid service = Guid.Parse("00001523-1212-efde-1523-785feabcd124");
        private Guid characteristic = Guid.Parse("00001525-1212-efde-1523-785feabcd124");
        private byte activateByte = 0x01;
        private byte deactivateByte = 0x00;
        private static List<GattCharacteristic> listGattCharacteristics = new List<GattCharacteristic>();

        private Regex regex = new Regex("^LHB-.{8}");

        public Form1(string[] args)
        {
            InitializeComponent();
            btnStart.Click += (obj, e) => SendOnLighthouseAsync(activateByte);
            btnStop.Click += (obj, e) => SendOnLighthouseAsync(deactivateByte);
            if (args.Length > 0)
            {
                UseArgumentsAsync(args);
            }
            else
            {
                GetGattCharacteristicsAsync();
            }
        }

        private async void UseArgumentsAsync(string[] args)
        {
            await GetGattCharacteristicsAsync();
            for (int i = 0; i < args.Length; ++i)
            {
                Log(args[i] + " " + listGattCharacteristics.Count.ToString());
                if (args[i] == "--powerOn")
                {
                    await SendOnLighthouseAsync(activateByte);
                    break;
                }
                else if (args[i] == "--powerOff")
                {
                    await SendOnLighthouseAsync(deactivateByte);
                    break;
                }
            }
            Close();
        }

        #region Body
        /// <summary>
        /// Call once at startup to get the characteristics of all base stations.
        /// </summary>
        /// <returns></returns>
        private async Task GetGattCharacteristicsAsync()
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
            if (listGattCharacteristics.Count > 0)
            {
                btnStop.Enabled = btnStart.Enabled = true;
            }
        }

        /// <summary>
        /// Called to write the value to the characteristic for all found base stations.
        /// </summary>
        /// <param name="byte4send"></param>
        /// <returns></returns>
        private async Task SendOnLighthouseAsync(byte byte4send)
        {
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
        }
        #endregion

        private void Log(object msg)
        {
            lvStatus.Items.Add(msg.ToString());
            lvStatus.Items[lvStatus.Items.Count - 1].ForeColor = Color.Green;
        }

        private void LogError(object msg)
        {
            lvStatus.Items.Add(msg.ToString());
            lvStatus.Items[lvStatus.Items.Count - 1].ForeColor = Color.DarkRed;
        }
    }
}
