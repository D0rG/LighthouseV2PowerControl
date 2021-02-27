using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
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
        private DeviceInformationCollection GatDevices;

        public Form1()
        {
            InitializeComponent();
            lbStatus.Text = "";
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            SendByte(activateByte);
        }

        private async void SendByte(byte byte4send)
        {
            GatDevices = await DeviceInformation.FindAllAsync(GattDeviceService.GetDeviceSelectorFromUuid(service));
            BluetoothLEDevice bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(GatDevices[1].Id);
            GattDeviceServicesResult result = await bluetoothLeDevice.GetGattServicesAsync();

            if (result.Status == GattCommunicationStatus.Success)
            {
                IReadOnlyList<GattDeviceService> serviceList = result.Services;
                for (int i = 0; i < serviceList.Count; ++i)
                {
                    if (serviceList[i].Uuid != service) continue;

                    GattCharacteristicsResult gattRes = await serviceList[i].GetCharacteristicsAsync();
                    if (gattRes.Status == GattCommunicationStatus.Success)
                    {
                        IReadOnlyList<GattCharacteristic> characteristics = gattRes.Characteristics;
                        DataWriter writer = new DataWriter();
                        writer.WriteByte(deactivateByte);
                        var openStatus = await serviceList[i].OpenAsync(GattSharingMode.SharedReadAndWrite);
                        lbStatus.Items.Add(openStatus);
                        for (int j = 0; j < characteristics.Count; ++j)
                        {
                            if(characteristics[j].Uuid != characteristic) continue;
                            lbStatus.Items.Add(characteristics[j].CharacteristicProperties);
                            var resWrite = await characteristics[j].WriteValueWithResultAsync(writer.DetachBuffer());
                            lbStatus.Items.Add(resWrite.Status);
                            //GattCommunicationStatus resWrite = await characteristics[j].WriteValueAsync(writer.DetachBuffer());
                            //if (resWrite == GattCommunicationStatus.Success)
                            //{
                            //    lbStatus.Items.Add(resWrite);
                            //}
                            //else
                            //{
                            //    lbStatus.Items.Add(resWrite);
                            //}
                        }
                    }
                }
            }
        }
    }
}
