using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Microsoft.Win32;
using System.IO;
using IOPath = System.IO.Path;

namespace AI_frontend
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<ChatMessage> Messages { get; } = new();
        public ObservableCollection<AttachedFile> AttachedFiles { get; } = new();

        private DispatcherTimer heartbeatTimer;
        private readonly HttpClient httpClient;
        private const string HeartbeatUrl = "http://localhost:8000/heartbeat"; // Change if your backend uses a different endpoint

        public MainWindow()
        {
            InitializeComponent();
            ConversationListBox.ItemsSource = Messages;
            
            // Initialize HttpClient with no timeout for long-running operations
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMilliseconds(Timeout.Infinite); // Remove timeout completely
            
            // Subscribe to collection changes to auto-scroll
            Messages.CollectionChanged += Messages_CollectionChanged;
            
            StartHeartbeat();
        }

        private void Messages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Auto-scroll to the latest message when a new message is added
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                ScrollToBottom();
            }
        }

        private void ScrollToBottom()
        {
            // Use Dispatcher to ensure UI updates are completed before scrolling
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Scroll to the bottom of the ScrollViewer
                ChatScrollViewer.ScrollToEnd();
            }), DispatcherPriority.Background);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // Set up attached files list box after XAML is fully loaded
            AttachedFilesListBox.ItemsSource = AttachedFiles;
        }

        private class ChatResponse
        {
           public required string response { get; set; }
        }

        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            string userMessage = UserInputTextBox.Text;

            if (string.IsNullOrWhiteSpace(userMessage) && AttachedFiles.Count == 0)
            {
                return; // Don't send empty messages with no files
            }

            // Create a copy of attached files for the message
            var messageFiles = AttachedFiles.Select(f => f.Name).ToList();

            // Add the user's message to the conversation list
            Messages.Add(new ChatMessage { 
                Text = string.IsNullOrWhiteSpace(userMessage) ? "Sent files" : userMessage, 
                IsUser = true,
                AttachedFiles = messageFiles
            });
            
            UserInputTextBox.Clear();
            TypingBubble.Visibility = Visibility.Visible;

            try
            {
                // Create multipart form data content
                using var formData = new MultipartFormDataContent();
                
                // Add the message text
                formData.Add(new StringContent(userMessage ?? ""), "message");
                
                // Add files if any
                foreach (var file in AttachedFiles)
                {
                    var fileContent = new ByteArrayContent(File.ReadAllBytes(file.FullPath));
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    formData.Add(fileContent, "files", file.Name);
                }

                var response = await httpClient.PostAsync("http://localhost:8000/chat", formData);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<ChatResponse>(responseJson);
                AddAIResponse(responseObj?.response ?? "No response from AI.");
            }
            catch (Exception ex)
            {
                AddAIResponse($"Error: {ex.Message}");
            }
            finally
            {
                // Clear attached files after sending
                AttachedFiles.Clear();
                UpdateAttachedFilesVisibility();
                TypingBubble.Visibility = Visibility.Collapsed;
            }
        }

        private void AddAIResponse(string aiMessage)
        {
            if (!string.IsNullOrWhiteSpace(aiMessage))
            {
                Messages.Add(new ChatMessage { Text = aiMessage, IsUser = false, AttachedFiles = new List<string>() });
            }
        }

        private void UserInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SubmitButton_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void AttachFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select files to attach",
                Multiselect = true,
                Filter = "All files (*.*)|*.*|Text files (*.txt)|*.txt|Image files (*.jpg;*.jpeg;*.png;*.gif;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|Document files (*.pdf;*.doc;*.docx)|*.pdf;*.doc;*.docx"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string fileName in openFileDialog.FileNames)
                {
                    // Check if file is already attached
                    if (!AttachedFiles.Any(f => f.FullPath == fileName))
                    {
                        // Check file size (limit to 10MB per file)
                        var fileInfo = new FileInfo(fileName);
                        if (fileInfo.Length > 10 * 1024 * 1024) // 10MB limit
                        {
                            MessageBox.Show($"File '{IOPath.GetFileName(fileName)}' is too large. Maximum file size is 10MB.", 
                                          "File Too Large", MessageBoxButton.OK, MessageBoxImage.Warning);
                            continue;
                        }

                        AttachedFiles.Add(new AttachedFile 
                        { 
                            Name = IOPath.GetFileName(fileName), 
                            FullPath = fileName 
                        });
                    }
                }
                UpdateAttachedFilesVisibility();
            }
        }

        private void RemoveFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is AttachedFile file)
            {
                AttachedFiles.Remove(file);
                UpdateAttachedFilesVisibility();
            }
        }

        private void UpdateAttachedFilesVisibility()
        {
            AttachedFilesListBox.Visibility = AttachedFiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void StartHeartbeat()
        {
            heartbeatTimer = new DispatcherTimer();
            heartbeatTimer.Interval = TimeSpan.FromSeconds(10); // Check every 10 seconds
            heartbeatTimer.Tick += async (s, e) => await CheckBackendConnection();
            heartbeatTimer.Start();
        }

        private async Task CheckBackendConnection()
        {
            try
            {
                // Use a separate HttpClient with shorter timeout for heartbeat
                using var heartbeatClient = new HttpClient();
                heartbeatClient.Timeout = TimeSpan.FromSeconds(5); // Short timeout for heartbeat
                
                var response = await heartbeatClient.GetAsync(HeartbeatUrl);
                if (response.IsSuccessStatusCode)
                {
                    ConnectionStatusLight.Fill = Brushes.LimeGreen;
                    ConnectionStatusLabel.Text = "Online";
                    ConnectionStatusLabel.Foreground = Brushes.Green;
                }
                else
                {
                    ConnectionStatusLight.Fill = Brushes.Red;
                    ConnectionStatusLabel.Text = "Offline";
                    ConnectionStatusLabel.Foreground = Brushes.Red;
                }
            }
            catch
            {
                ConnectionStatusLight.Fill = Brushes.Red;
                ConnectionStatusLabel.Text = "Offline";
                ConnectionStatusLabel.Foreground = Brushes.Red;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Dispose HttpClient when window closes
            httpClient?.Dispose();
            heartbeatTimer?.Stop();
            
            // Unsubscribe from events
            Messages.CollectionChanged -= Messages_CollectionChanged;
            
            base.OnClosed(e);
        }
    }

    public class ChatMessage 
    {
        public required string Text { get; set; }
        public bool IsUser { get; set; }
        public List<string> AttachedFiles { get; set; } = new();
    }

    public class AttachedFile
    {
        public required string Name { get; set; }
        public required string FullPath { get; set; }
    }
}