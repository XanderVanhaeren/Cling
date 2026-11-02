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
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var devices = await Task.Run(() => new OpenClDeviceService().GetAvailableDevices());
            DeviceSelector.ItemsSource = devices;
            DeviceSelector.IsEnabled = devices.Count > 1;
            DeviceSelector.SelectedIndex = 0;
            UpdateSelectedDevice();
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
    }
}