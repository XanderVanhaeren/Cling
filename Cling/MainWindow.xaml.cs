using System.Windows;
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
            var deviceInfo = await Task.Run(() => new OpenClDeviceService().GetDefaultDeviceInfo());
            DeviceInfoView.DataContext = deviceInfo;
            MemoryInfoView.DataContext = deviceInfo;
        }
    }
}