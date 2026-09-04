using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Lights;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using static BLELight.BLEClient;

// Документацию по шаблону элемента "Пустая страница" см. по адресу https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x419

namespace BLELight
{
    /// <summary>
    /// Пустая страница, которую можно использовать саму по себе или для перехода внутри фрейма.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private BLEClient Client = new BLEClient();
        private DispatcherTimer ConnectionTimer;
        private bool LastAvailable = true;
        public MainPage()
        {
            this.InitializeComponent();

            Client.Init();
            Client.LampDiscovered += Client_LampDiscovered;
            Client.ScanStopped += Client_ScanStopped;

            Client.EnableEvents();
            Client.StartScan();

            ConnectionTimer = new DispatcherTimer();
            ConnectionTimer.Interval = TimeSpan.FromSeconds(1);
            ConnectionTimer.Tick += ConnectionTimer_Tick;
            ConnectionTimer.Start();
        }

        private async void ConnectionTimer_Tick(object sender, object e)
        {
            int index = DevicesList.SelectedIndex;
            BluetoothLEDevice current_device = Client.GetDeviceByIndex(index);

            if (current_device != null)
            {
                bool available = current_device.ConnectionStatus == BluetoothConnectionStatus.Connected;
                if(available != LastAvailable)
                {
                    if(available)
                    {
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            WarmSlider.IsEnabled = true;
                            Colorpicker.IsEnabled = true;
                        });
                    } else
                    {
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            WarmSlider.IsEnabled = false;
                            Colorpicker.IsEnabled = false;
                        });

                        LampColor lampColor = await Client.GetColor(current_device);
                        if(lampColor.IsWarm)
                        {
                            Client.SetWarmColor(current_device, lampColor.Warm);
                        } else
                        {
                            Client.SetColor(current_device, lampColor.Color);
                        }
                    }
                    LastAvailable = available;
                }
            }
        }
        private async void Client_LampDiscovered(object sender, LampDiscoveredEventArgs e)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                DevicesList.Items.Add(e.Device.DeviceInformation.Name + " (" + e.Distance + " dbm) ");
            });
        }

        private async void Client_ScanStopped(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                ScanButton.IsEnabled = true;
            });
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            App.Current.Suspending += App_Suspending;
            App.Current.Resuming += App_Resuming;
        }


        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            App.Current.Suspending -= App_Suspending;
            App.Current.Resuming -= App_Resuming;

            Client.StopScan();
            Client.DisableEvents();

            base.OnNavigatingFrom(e);
        }


        private void App_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            Client.StopScan();
            Client.DisableEvents();
        }

        private void App_Resuming(object sender, object e)
        {
            Client.EnableEvents();
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Colorpicker.IsEnabled = false;
            WarmSlider.IsEnabled = false;
            ScanButton.IsEnabled = false;

            DevicesList.Items.Clear();

            Client.EnableEvents();
            Client.StartScan();
        }
    

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            
        }

        private void ColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            
        }

        private async void WarmSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            int index = DevicesList.SelectedIndex;
            BluetoothLEDevice device = Client.GetDeviceByIndex(index);

            if(device != null)
            {
                int value = (int)WarmSlider.Value;
                double percent = value / 255.0;
                WarmSliderText.Text = value.ToString();
                LampColor lampColor = await Client.GetColor(device);
                Windows.UI.Color color = Windows.UI.Color.FromArgb(255, (byte)(255.0 * percent), (byte)(166.0 * percent), 0);
                lampColor.Color = color;
                Colorpicker.Color = color;
                Client.SetWarmColor(device, value);
            }
        }

        private void Colorpicker_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            int index = DevicesList.SelectedIndex;
            BluetoothLEDevice device = Client.GetDeviceByIndex(index);

            if (device != null)
            {
                Client.SetColor(device, Colorpicker.Color);
            }
        }

        private void DevicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int index = DevicesList.SelectedIndex;
            BluetoothLEDevice device = Client.GetDeviceByIndex(index);

            if (device != null)
            {
                Colorpicker.IsEnabled = true;
                WarmSlider.IsEnabled = true;

                Task.Factory.StartNew(async () =>
                {
                    LampColor color = await Client.GetColor(device);
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        Colorpicker.Color = color.Color;
                        WarmSlider.Value = color.Warm;
                        WarmSliderText.Text = color.Warm.ToString();
                    });
                });
            }
        }
    }
}
