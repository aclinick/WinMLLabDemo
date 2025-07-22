using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Win32;
using Microsoft.Windows.AI.MachineLearning;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml.Linq;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Streams;
using IOPath = System.IO.Path;

namespace WinMLLabDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private OrtEnv _ortEnv;
        public ObservableCollection<OrtEpDevice> ExecutionProviders { get; set; }
        private string selectedImagePath = string.Empty;
        private OrtEpDevice? selectedExecutionProvider = null;
        private const string ModelName = "SqueezeNet";
        private const string ModelExtension = ".onnx";

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

            ExecutionProviders = new ObservableCollection<OrtEpDevice>();
            ExecutionProvidersGrid.ItemsSource = ExecutionProviders;
            
            // Set up EP selection event
            ExecutionProvidersGrid.SelectionChanged += ExecutionProvidersGrid_SelectionChanged;
            
            // Initialize with some sample data
            LoadExecutionProviders();
            WriteToConsole("WinML Demo Application initialized.");
        }

        private void ExecutionProvidersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ExecutionProvidersGrid.SelectedItem is OrtEpDevice selectedEP)
            {
                selectedExecutionProvider = selectedEP;
                WriteToConsole($"Selected execution provider: {selectedEP.EpName}");
                
                // Enable Compile Model button
                CompileModelButton.IsEnabled = true;
                
                // Check if compiled model exists and enable/disable Run button accordingly
                UpdateRunButtonState();
            }
            else
            {
                selectedExecutionProvider = null;
                CompileModelButton.IsEnabled = false;
                RunButton.IsEnabled = false;
            }
        }

        private void UpdateRunButtonState()
        {
            if (selectedExecutionProvider != null)
            {
                string compiledModelPath = GetCompiledModelPath(selectedExecutionProvider);
                RunButton.IsEnabled = File.Exists(compiledModelPath);
                
                if (RunButton.IsEnabled)
                {
                    WriteToConsole($"Compiled model found: {IOPath.GetFileName(compiledModelPath)}");
                }
                else
                {
                    WriteToConsole($"Compiled model not found: {compiledModelPath}");
                }
            }
            else
            {
                RunButton.IsEnabled = false;
            }
        }

        private string GetCompiledModelPath(OrtEpDevice ep)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string compiledModelName = $"{ep.EpName}.{ModelName}{ModelExtension}";
            return IOPath.Combine(baseDirectory, compiledModelName);
        }

        private void LoadExecutionProviders()
        {
            // Add some default/sample execution providers
            ExecutionProviders.Clear();

            var eps = _ortEnv.GetEpDevices();

            foreach (var ep in eps)
            {
                ExecutionProviders.Add(ep);
            }

            WriteToConsole("Loaded execution providers.");
        }

        private void RefreshEPButton_Click(object sender, RoutedEventArgs e)
        {
            LoadExecutionProviders();
            // Reset selection state
            selectedExecutionProvider = null;
            CompileModelButton.IsEnabled = false;
            RunButton.IsEnabled = false;
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

        private async void CompileModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedExecutionProvider == null)
            {
                WriteToConsole("No execution provider selected.");
                return;
            }

            CompileModelButton.IsEnabled = false;
            try
            {
                WriteToConsole($"Compiling model for {selectedExecutionProvider.EpName}...");
                var now = DateTime.Now;

                string compiledModelPath = await Task.Run(() => CompileModelForExecutionProvider(selectedExecutionProvider));

                var elapsed = DateTime.Now - now;
                WriteToConsole($"Model compiled successfully in {elapsed.TotalMilliseconds} ms: {compiledModelPath}");
                
                // Update Run button state
                UpdateRunButtonState();
            }
            catch (Exception ex)
            {
                WriteToConsole($"Error compiling model: {ex.Message}");
            }
            finally
            {
                CompileModelButton.IsEnabled = true;
            }
        }

        private SessionOptions GetSessionOptions(OrtEpDevice executionProvider)
        {
            // Create a session
            var sessionOptions = new SessionOptions();

            Dictionary<string, string> epOptions = new(StringComparer.OrdinalIgnoreCase);

            switch (executionProvider.EpName)
            {
                case "VitisAIExecutionProvider":
                    sessionOptions.AppendExecutionProvider(_ortEnv, [executionProvider], epOptions);
                    break;

                case "OpenVINOExecutionProvider":
                    // Configure threading for OpenVINO EP
                    epOptions["num_of_threads"] = "4";
                    sessionOptions.AppendExecutionProvider(_ortEnv, [executionProvider], epOptions);
                    break;

                case "QNNExecutionProvider":
                    // Configure performance mode for QNN EP
                    epOptions["htp_performance_mode"] = "high_performance";
                    sessionOptions.AppendExecutionProvider(_ortEnv, [executionProvider], epOptions);
                    break;

                case "NvTensorRTRTXExecutionProvider":
                    // Configure performance mode for TensorRT RTX EP
                    sessionOptions.AppendExecutionProvider(_ortEnv, [executionProvider], epOptions);
                    break;

                default:
                    break;
            }

            return sessionOptions;
        }

        private string CompileModelForExecutionProvider(OrtEpDevice executionProvider)
        {
            string baseModelPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{ModelName}{ModelExtension}");
            string compiledModelPath = GetCompiledModelPath(executionProvider);

            try
            {
                var sessionOptions = GetSessionOptions(executionProvider);

                // Create compilation options from session options
                OrtModelCompilationOptions compileOptions = new(sessionOptions);

                // Set input and output model paths
                compileOptions.SetInputModelPath(baseModelPath);
                compileOptions.SetOutputModelPath(compiledModelPath);

                // Compile the model
                compileOptions.CompileModel();
            }
            catch
            {
                throw new Exception($"Failed to create session with execution provider: {executionProvider.EpName}");
            }
            
            return compiledModelPath;
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

        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedImagePath))
            {
                WriteToConsole("Please select an image first.");
                return;
            }

            if (selectedExecutionProvider == null)
            {
                WriteToConsole("Please select an execution provider first.");
                return;
            }

            string compiledModelPath = GetCompiledModelPath(selectedExecutionProvider);
            if (!File.Exists(compiledModelPath))
            {
                WriteToConsole("Compiled model not found. Please compile the model first.");
                return;
            }

            WriteToConsole($"Running classification on: {IOPath.GetFileName(selectedImagePath)}");
            WriteToConsole($"Using execution provider: {selectedExecutionProvider.EpName}");
            WriteToConsole($"Using compiled model: {IOPath.GetFileName(compiledModelPath)}");
            
            // Disable the button during inference
            RunButton.IsEnabled = false;
            
            try
            {
                ResultsTextBlock.Text = "Running classification ...";
                DateTime start = DateTime.Now;
                var results = await Task.Run(() => RunModelAsync(selectedImagePath, compiledModelPath, selectedExecutionProvider));
                ResultsTextBlock.Text = results;
                var time = DateTime.Now - start;
                WriteToConsole($"Classification completed successfully in {time.TotalMilliseconds} ms.");
            }
            catch (Exception ex)
            {
                WriteToConsole($"Error during classification: {ex.Message}");
                ResultsTextBlock.Text = $"Error during classification: {ex.Message}";
            }
            finally
            {
                // Re-enable the button
                RunButton.IsEnabled = true;
            }
        }

        private async Task<string> RunModelAsync(string imagePath, string compiledModelPath, OrtEpDevice executionProvider)
        {
            var sessionOptions = GetSessionOptions(executionProvider);

            using var session = new InferenceSession(compiledModelPath, sessionOptions);

            Console.WriteLine("Preparing input ...");
            var inputs = await ModelHelpers.BindInputs(imagePath, session);

            Console.WriteLine("Running inference ...");
            using var results = session.Run(inputs);

            // Format the results
            return ModelHelpers.FormatResults(results, session);
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
}