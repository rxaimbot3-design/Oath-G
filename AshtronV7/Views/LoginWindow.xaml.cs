using System.Windows;
using System.Windows.Input;
using AshtronV7.ViewModels;
using AshtronV7.Services;

namespace AshtronV7.Views
{
    public partial class LoginWindow : Window
    {
        private readonly SettingsService _settingsService;

        public LoginWindow()
        {
            InitializeComponent();
            _settingsService = new SettingsService(LoggerService.Instance);
            DataContext = new LoginViewModel(_settingsService, OnLoginResult);
        }

        private void OnLoginResult(bool success)
        {
            if (success)
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
                Close();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm)
            {
                vm.Password = PasswordBox.Password;
            }
        }
    }
}