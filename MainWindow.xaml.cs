using Microsoft.ML.OnnxRuntime;
using Microsoft.Win32;
using Microsoft.Windows.AI.MachineLearning;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using IOPath = System.IO.Path;

namespace WinMLLabDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private OrtEnv _ortEnv;
        public ObservableCollection<ExecutionProvider> ExecutionProviders { get; set; }
        private string selectedImagePath = string.Empty;

        public MainWindow()
        {
            InitializeComponent();

            // Create a new instance of EnvironmentCreationOptions
            EnvironmentCreationOptions envOptions = new()
            {
                logId = "WinMLLabDemo",
                logLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING
            };

            // Pass the options by reference to CreateInstanceWithOptions
            _ortEnv = OrtEnv.CreateInstanceWithOptions(ref envOptions);

            ExecutionProviders = new ObservableCollection<ExecutionProvider>();
            ExecutionProvidersGrid.ItemsSource = ExecutionProviders;
            
            // Initialize with some sample data
            LoadExecutionProviders();
        }

        private void LoadExecutionProviders()
        {
            // Add some default/sample execution providers
            ExecutionProviders.Clear();

            var eps = _ortEnv.GetEpDevices();

            foreach (var ep in eps)
            {
                ExecutionProviders.Add(new ExecutionProvider(ep.EpName, ep.EpVendor, ep.HardwareDevice.Type.ToString()));
            }

            WriteToConsole("Loaded execution providers.");
        }

        private void RefreshEPButton_Click(object sender, RoutedEventArgs e)
        {
            LoadExecutionProviders();
        }

        private async void InitializeWinMLEPsButton_Click(object sender, RoutedEventArgs e)
        {
            InitializeWinMLEPsButton.IsEnabled = false;
            try
            {
                WriteToConsole("WinML: Downloading and registering EPs...");
                var now = DateTime.Now;

                // TODO: Download and register the Execution Providers for our device
                var catalog = ExecutionProviderCatalog.GetDefault();
                var registeredProviders = await catalog.EnsureAndRegisterAllAsync();

                var elapsed = DateTime.Now - now;
                WriteToConsole($"WinML: EPs downloaded and registered in {elapsed.TotalMilliseconds} ms.");
                LoadExecutionProviders();
            }
            catch (Exception ex)
            {
                WriteToConsole($"Error downloading execution providers: {ex.Message}");
            }
            finally
            {
                InitializeWinMLEPsButton.IsEnabled = true;
            }
        }

        private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select an image file",
                Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*",
                InitialDirectory = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedImagePath = openFileDialog.FileName;
                ImagePathTextBox.Text = selectedImagePath;
                
                try
                {
                    // Load and display the selected image
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(selectedImagePath);
                    bitmap.DecodePixelWidth = 300; // Limit size for preview
                    bitmap.EndInit();
                    SelectedImage.Source = bitmap;
                    
                    WriteToConsole($"Selected image: {IOPath.GetFileName(selectedImagePath)}");
                }
                catch (Exception ex)
                {
                    WriteToConsole($"Error loading image: {ex.Message}");
                }
            }
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedImagePath))
            {
                WriteToConsole("Please select an image first.");
                return;
            }

            WriteToConsole($"Running classification on: {IOPath.GetFileName(selectedImagePath)}");
            
            // TODO: Implement actual WinML inference
            ResultsTextBlock.Text = "Classification results will appear here after WinML integration is implemented.";
            
            WriteToConsole("Classification completed (placeholder).");
        }

        private void ClearConsoleButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleTextBlock.Text = string.Empty;
        }

        public void WriteToConsole(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}\n";
            
            Dispatcher.Invoke(() =>
            {
                ConsoleTextBlock.Text += logEntry;
                
                // Auto-scroll to bottom
                if (ConsoleTextBlock.Parent is ScrollViewer scrollViewer)
                {
                    scrollViewer.ScrollToEnd();
                }
            });
        }
    }

    public class ExecutionProvider : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _vendor = string.Empty;
        private string _deviceType = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Vendor
        {
            get => _vendor;
            set
            {
                _vendor = value;
                OnPropertyChanged(nameof(Vendor));
            }
        }

        public string DeviceType
        {
            get => _deviceType;
            set
            {
                _deviceType = value;
                OnPropertyChanged(nameof(DeviceType));
            }
        }

        public ExecutionProvider(string name, string vendor, string deviceType)
        {
            Name = name;
            Vendor = vendor;
            DeviceType = deviceType;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}