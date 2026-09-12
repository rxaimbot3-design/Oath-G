using System;
using System.Windows;
using System.Windows.Input;
using AshtronV7.Services;
using AshtronV7.Views;

namespace AshtronV7.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private readonly Action<bool> _onLoginResult;

        private string _username = "ashtron";
        private string _password = "";
        private string _errorMessage = "";
        private bool _isLoggingIn;

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool IsLoggingIn
        {
            get => _isLoggingIn;
            set => SetProperty(ref _isLoggingIn, value);
        }

        public ICommand LoginCommand { get; }

        public LoginViewModel(SettingsService settingsService, Action<bool> onLoginResult)
        {
            _settingsService = settingsService;
            _onLoginResult = onLoginResult;
            LoginCommand = new RelayCommand(async () => await LoginAsync(), () => !IsLoggingIn && !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password));
        }

        private async System.Threading.Tasks.Task LoginAsync()
        {
            IsLoggingIn = true;
            ErrorMessage = "";

            await System.Threading.Tasks.Task.Delay(500);

            if (_settingsService.ValidateCredentials(Username, Password))
            {
                _onLoginResult?.Invoke(true);
            }
            else
            {
                ErrorMessage = "Invalid credentials. Username: ashtron, Password: 1";
                _onLoginResult?.Invoke(false);
            }

            IsLoggingIn = false;
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object parameter) => _execute();
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter)
        {
            if (parameter is T t) return _canExecute?.Invoke(t) ?? true;
            if (parameter == null && default(T) == null) return _canExecute?.Invoke(default) ?? true;
            return false;
        }

        public void Execute(object parameter)
        {
            if (parameter is T t) _execute(t);
            else if (parameter == null && default(T) == null) _execute(default);
        }
    }
}