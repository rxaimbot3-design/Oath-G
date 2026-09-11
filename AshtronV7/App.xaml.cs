using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using AshtronV7.Services;
using AshtronV7.Views;
using Microsoft.Win32;

namespace AshtronV7
{
    public partial class App : Application
    {
        private static Mutex _mutex;
        private TrayService _trayService;
        private EngineService _engineService;
        private LoggerService _logger;
        private bool _isShuttingDown;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _logger = LoggerService.Instance;
            _logger.Log("Application starting...");

            bool createdNew;
            _mutex = new Mutex(true, "AshtronV7_SingleInstance", out createdNew);
            if (!createdNew)
            {
                _logger.Log("Another instance is already running");
                MessageBox.Show("ASHTRON V7 is already running.", "ASHTRON V7", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            if (!IsRunAsAdmin())
            {
                _logger.Log("Restarting as administrator...");
                RestartAsAdmin();
                Shutdown();
                return;
            }

            _logger.Log("Running as administrator");

            InitializeServices();
        }

        private void InitializeServices()
        {
            _engineService = new EngineService(_logger);
            _trayService = new TrayService(_engineService, _logger);
            
            _engineService.OnEngineStateChanged += (state) =>
            {
                Dispatcher.Invoke(() => _trayService.UpdateEngineState(state));
            };

            var loginWindow = new LoginWindow();
            loginWindow.Show();
        }

        private bool IsRunAsAdmin()
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        private void RestartAsAdmin()
        {
            var exeName = Assembly.GetExecutingAssembly().Location;
            var processInfo = new ProcessStartInfo
            {
                FileName = exeName,
                UseShellExecute = true,
                Verb = "runas"
            };
            try
            {
                Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to restart as admin", ex);
            }
        }

        public static void RequestShutdown()
        {
            if (Current != null)
            {
                Current.Dispatcher.Invoke(() => Current.Shutdown());
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (!_isShuttingDown)
            {
                _isShuttingDown = true;
                _engineService?.StopEngine();
                _engineService?.RestoreWindows();
                _trayService?.Dispose();
                _logger?.Log("Application exited");
            }
            base.OnExit(e);
        }
    }
}