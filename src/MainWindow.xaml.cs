using System.Windows;
using System.Windows.Controls;
using Cling.Models;
using Cling.Services;

namespace Cling
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly OpenClDeviceService _deviceService = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Load real OpenCL devices
            var devices = await _deviceService.GetAvailableDevices();
            DeviceSelector.ItemsSource = devices;
            DeviceSelector.IsEnabled = devices.Count > 1;
            if (devices.Count > 1)
            {
                DeviceSelector.SelectedIndex = 0;
            }
        }

        private void DeviceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectedDevice();
        }

        private void UpdateSelectedDevice()
        {
            if (DeviceSelector.SelectedItem is OpenClDeviceInfo deviceInfo)
            {
                DeviceInfoView.DataContext = deviceInfo;
                MemoryInfoView.DataContext = deviceInfo;
            }
        }

        private void AboutView_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}