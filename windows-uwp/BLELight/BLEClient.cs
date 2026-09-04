using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Lights;
using Windows.Storage.Streams;
using Windows.UI;

namespace BLELight
{
    class BLEClient
    {
        #region Константы
        public static Guid UUID_COLOR_READ = Guid.Parse("0000ee01-0000-1000-8000-00805f9b34fb");
        public static Guid UUID_COLOR_WRITE = Guid.Parse("0000ee03-0000-1000-8000-00805f9b34fb");
        public static Guid UUID_EBOYLIGHT_LED = Guid.Parse("0000cc02-0000-1000-8000-00805f9b34fb");
        public static Guid UUID_HAND_CHAR = Guid.Parse("0000ee02-0000-1000-8000-00805f9b34fb");
        public static Guid UUID_SCAN_EBOYLIGHT_CHAR = Guid.Parse("ffffcc02-0000-1000-8000-00805f9b34fb");
        public static Guid UUID_SCAN_EBOYLIGHT_CHAR_else = Guid.Parse("0000cc02-0000-1000-8000-00805f9b34fb");
        #endregion

        /// <summary>
        /// Сканнер BLE
        /// </summary>
        private BluetoothLEAdvertisementWatcher Watcher;

        private List<BluetoothLEDevice> FoundedDevices = new List<BluetoothLEDevice>();
        private List<string> FoundedDevices_ID = new List<string>();
        private Dictionary<BluetoothLEDevice, LampColor> LampColors = new Dictionary<BluetoothLEDevice, LampColor>();

        public void Init()
        {
            Watcher = new BluetoothLEAdvertisementWatcher();

            Watcher.ScanningMode = BluetoothLEScanningMode.Active;
            Watcher.SignalStrengthFilter.InRangeThresholdInDBm = -70;
            Watcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -75;
            Watcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(2000);
        }
        public void StartScan()
        {
            foreach (BluetoothLEDevice device in FoundedDevices) device.Dispose();

            FoundedDevices.Clear();
            FoundedDevices_ID.Clear();
            LampColors.Clear();

            Watcher.Start();
            Debug.WriteLine("Запускаю сканирование...");
        }
        public void StopScan()
        {
            Watcher.Stop();
        }
        public void EnableEvents()
        {
            Watcher.Received += OnAdvertisementReceived;
            Watcher.Stopped += OnAdvertisementWatcherStopped;
        }
        public void DisableEvents()
        {
            Watcher.Received -= OnAdvertisementReceived;
            Watcher.Stopped -= OnAdvertisementWatcherStopped;
        }
        private async void WriteCharacteristic(BluetoothLEDevice device, Guid uuid, IBuffer buffer)
        {
            GattDeviceServicesResult result = await device.GetGattServicesAsync();

            if (device.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                foreach (GattDeviceService service in result.Services)
                {
                    if (service.Uuid == UUID_EBOYLIGHT_LED)
                    {
                        try
                        {
                            GattCharacteristicsResult HandCharacteristicList = await service.GetCharacteristicsAsync();


                            if (HandCharacteristicList.Characteristics.Count > 0)
                            {
                                foreach (GattCharacteristic characteristic in HandCharacteristicList.Characteristics)
                                {
                                    if (characteristic.Uuid == uuid)
                                    {
                                        await characteristic.WriteValueAsync(buffer);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
        }
        private async Task<GattReadResult> ReadCharacteristic(BluetoothLEDevice device, Guid uuid)
        {
            GattDeviceServicesResult result = await device.GetGattServicesAsync();

            if (device.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                foreach (GattDeviceService service in result.Services)
                {
                    if (service.Uuid == UUID_EBOYLIGHT_LED)
                    {
                        try
                        {
                            GattCharacteristicsResult HandCharacteristicList = await service.GetCharacteristicsAsync();


                            if (HandCharacteristicList.Characteristics.Count > 0)
                            {
                                foreach (GattCharacteristic characteristic in HandCharacteristicList.Characteristics)
                                {
                                    if (characteristic.Uuid == uuid)
                                    {
                                        return await characteristic.ReadValueAsync();
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            return null;
        }
        public BluetoothLEDevice GetDeviceByIndex(int index)
        {
            if (index < FoundedDevices.Count && index >= 0)
            {
                return FoundedDevices[index];
            } else
            {
                return null;
            }
        }
        public void SetColor(BluetoothLEDevice device, Color color)
        {
            var writer = new DataWriter();
            byte[] p = new byte[10];
            p[7] = color.R;
            p[1] = color.G;
            p[5] = color.B;

            p[6] = 1;
            p[4] = 1;
            p[0] = 1;
            p[9] = 0;
            p[8] = 0;
            p[3] = 0;
            p[2] = 0;
            writer.WriteBytes(p);

            this.WriteCharacteristic(device, UUID_COLOR_WRITE, writer.DetachBuffer());

            LampColor lampColor = LampColors.GetValueOrDefault(device);
            lampColor.IsWarm = false;
            lampColor.Color = color;
        }
        public async Task<LampColor> GetColor(BluetoothLEDevice device)
        {
            if(LampColors.ContainsKey(device))
            {
                return LampColors.GetValueOrDefault(device);
            } else
            {
                GattReadResult result = await ReadCharacteristic(device, UUID_COLOR_READ);

                if (result != null)
                {
                    Stream inputStream = result.Value.AsStream();
                    byte[] data = new byte[inputStream.Length];
                    inputStream.Read(data, 0, (int)inputStream.Length);

                    Color color = Color.FromArgb(255, data[7], data[1], data[5]);
                    bool isWarm = data[2] == 1 && data[8] == 1;
                    bool isTurnOn = isWarm || (data[0] == 1 && data[6] == 1 && data[4] == 1);

                    int cold = data[3];
                    int warm = data[9];

                    if (isWarm && data[3] <= 0 && data[9] <= 0)
                    {
                        cold = 5;
                        warm = 250;
                    }
                    if (!(cold == 0 || warm == 0))
                    {
                        isWarm = true;
                    }

                    double percent = warm / 255.0;
                    if (isWarm) color = Color.FromArgb(255, (byte)(255.0 * percent), (byte)(166.0 * percent), 0);

                    LampColor lampColor = new LampColor
                    {
                        Color = color,
                        IsWarm = isWarm,
                        IsTurnOn = isTurnOn,
                        Warm = warm
                    };
                    try
                    {
                        LampColors.TryAdd(device, lampColor);
                    }
                    catch(Exception e) { }
                    
                    return lampColor;
                }
                else
                {
                    return null;
                }
            }
        }
        public void SetWarmColor(BluetoothLEDevice device, int warm)
        {
            var writer = new DataWriter();
            byte[] p = new byte[10];

            if (warm < 1) warm = 1;

            p[6] = 0;
            p[4] = 0;
            p[0] = 0;
            p[8] = 1;
            p[2] = 1;
            p[3] = 0; // Изначально холодный, но лампа не поддерживает
            p[9] = (byte)warm;
            writer.WriteBytes(p);

            this.WriteCharacteristic(device, UUID_COLOR_WRITE, writer.DetachBuffer());

            LampColor lampColor = LampColors.GetValueOrDefault(device);
            lampColor.IsWarm = true;
            lampColor.Warm = warm;

        }
        private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            var address = eventArgs.BluetoothAddress;

            if (!FoundedDevices_ID.Contains(address.ToString()))
            {
                FoundedDevices_ID.Add(address.ToString());
                BluetoothLEDevice device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);

                GattDeviceServicesResult result;
                try
                {
                    result = await device.GetGattServicesAsync();
                    Debug.WriteLine("Найдено устройство! Название - " + device.Name + ", Кол-во сервисов - " + result.Services.Count + ", Альтернативное название - " + device.DeviceInformation.Name + ", Статус - " + device.ConnectionStatus.ToString() + ", UUID - " + device.DeviceId);

                    if (device.ConnectionStatus == BluetoothConnectionStatus.Connected)
                    {
                        foreach (GattDeviceService service in result.Services)
                        {
                            if (service.Uuid == UUID_EBOYLIGHT_LED)
                            {
                                Debug.WriteLine("Обнаружена лампа");
                                FoundedDevices.Add(device);
                                
                                OnLampDiscovered(new LampDiscoveredEventArgs(device, eventArgs.RawSignalStrengthInDBm));
                            }
                        }
                    }
                }
                catch (Exception ex) { }
            }
        }
        private void OnAdvertisementWatcherStopped(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementWatcherStoppedEventArgs eventArgs)
        {
            Debug.WriteLine("Сканирование завершено.");

            OnScanStopped(new EventArgs());
        }
        public event EventHandler<LampDiscoveredEventArgs> LampDiscovered;
        public event EventHandler ScanStopped;
        protected virtual void OnLampDiscovered(LampDiscoveredEventArgs e)
        {
            EventHandler<LampDiscoveredEventArgs> handler = LampDiscovered;
            handler?.Invoke(this, e);
        }
        protected virtual void OnScanStopped(EventArgs e)
        {
            EventHandler handler = ScanStopped;
            handler?.Invoke(this, e);
        }
        public class LampDiscoveredEventArgs : EventArgs
        {
            public LampDiscoveredEventArgs(BluetoothLEDevice newDevice, int newDistance)
            {
                Device = newDevice;
                Distance = newDistance;
            }

            public BluetoothLEDevice Device { get; private set; }
            public int Distance { get; private set; }
        }
    }
}
