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

namespace AI_frontend
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<ChatMessage> Messages { get; } = new();

        private DispatcherTimer heartbeatTimer;
        private const string HeartbeatUrl = "http://localhost:8000/heartbeat"; // Change if your backend uses a different endpoint

        public MainWindow()
        {
        InitializeComponent();
        ConversationListBox.ItemsSource = Messages;
        StartHeartbeat();
        }


        private class ChatResponse
        {
           public required string response { get; set; }
        }


        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            string userMessage = UserInputTextBox.Text;

            if (!string.IsNullOrWhiteSpace(userMessage))
            {
                // Add the user's message to the conversation list
                Messages.Add(new ChatMessage { Text = userMessage, IsUser = true });
                UserInputTextBox.Clear();
            }

            TypingBubble.Visibility = Visibility.Visible;

            var requestBody = new { message = userMessage };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var httpClient = new HttpClient(); // Create an HTTP client
                var response = await httpClient.PostAsync("http://localhost:8000/chat", content); // Send POST request
                response.EnsureSuccessStatusCode(); // Throw if not successful

                var responseJson = await response.Content.ReadAsStringAsync(); // Get response as text
                var responseObj = JsonSerializer.Deserialize<ChatResponse>(responseJson); // Convert JSON to object
                AddAIResponse(responseObj?.response ?? "No response from AI.");
            }

            catch (Exception ex)
            {
                AddAIResponse($"Error: {ex.Message}");
            }
            finally
            {
                TypingBubble.Visibility = Visibility.Collapsed; // Hide indicator
            }

        }

        private void AddAIResponse(string aiMessage)
        {
            if (!string.IsNullOrWhiteSpace(aiMessage))
            {
                Messages.Add(new ChatMessage { Text = aiMessage, IsUser = false });
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

        private void StartHeartbeat()
        {
            heartbeatTimer = new DispatcherTimer();
            heartbeatTimer.Interval = TimeSpan.FromSeconds(3); // Check every 3 seconds
            heartbeatTimer.Tick += async (s, e) => await CheckBackendConnection();
            heartbeatTimer.Start();
        }

        private async Task CheckBackendConnection()
        {
            try
            {
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(HeartbeatUrl);
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
    }

    public class ChatMessage 
    {
        public required string Text { get; set; }
        public bool IsUser { get; set; }

    }




}