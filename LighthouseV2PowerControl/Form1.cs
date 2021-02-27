﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

        public Form1()
        {
            InitializeComponent();
            lbStatus.Text = "";
            btnStart.Click += (obj,e) => SendOnLighthouse(activateByte);
            btnStop.Click += (obj,e) => SendOnLighthouse(deactivateByte);
            btnStop.Enabled = btnStart.Enabled = false;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            GetGattCharacteristics();
        }

        private async void GetGattCharacteristics()
        {
            DeviceInformationCollection GatDevices = await DeviceInformation.FindAllAsync(GattDeviceService.GetDeviceSelectorFromUuid(service));
            for (int id = 0; id < GatDevices.Count; id++)
            {
                if(!regex.IsMatch(GatDevices[id].Name)) continue;

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
                    }
                }
            }

            Log($"lighthouses found: {listGattCharacteristics.Count}");
            if (listGattCharacteristics.Count > 0)
            {
                btnStop.Enabled = btnStart.Enabled = true;
            }
        }

        private async void SendOnLighthouse(byte byte4send)
        {
            for (int i = 0; i < listGattCharacteristics.Count; ++i)
            {
                DataWriter writer = new DataWriter();
                writer.WriteByte(byte4send);
                GattCommunicationStatus resWrite = await listGattCharacteristics[i].WriteValueAsync(writer.DetachBuffer());
                if (resWrite == GattCommunicationStatus.Success)
                {
                    Log($"Success");
                }
                else
                {
                    LogError($"lighthouse: {resWrite}");
                }
            }
        }

        private void Log(object msg)
        {
            lbStatus.Items.Add(msg);
        }

        private void LogError(object msg)
        {
            lbStatus.Items.Add(msg);
        }
    }
}