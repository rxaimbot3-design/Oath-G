using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AshtronV7.Models;
using AshtronV7.Services;

namespace AshtronV7.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly EngineService _engineService;
        private readonly LoggerService _logger;
        private readonly SettingsService _settingsService;

        private EngineState _engineState = EngineState.Stopped;
        private string _engineStatusText = "ENGINE STOPPED";
        private Brush _engineStatusColor = Brushes.Gray;
        private bool _isEngineActive;

        private PerformanceMetrics _currentMetrics = new();
        private HardwareInfo _hardwareInfo;
        private OptimizationProfile _currentProfile;
        private ObservableCollection<string> _activityLog = new();
        private ObservableCollection<ProcessInfo> _focusedProcesses = new();
        private ObservableCollection<OptimizationProfile> _availableProfiles = new();
        private ObservableCollection<FocusLevel> _focusLevels = new();
        private ObservableCollection<KnownBackgroundApp> _knownBackgroundApps = new();
        private string _selectedTab = "Home";

        public EngineState EngineState
        {
            get => _engineState;
            set
            {
                SetProperty(ref _engineState, value);
                UpdateEngineStatusUI(value);
            }
        }

        public string EngineStatusText
        {
            get => _engineStatusText;
            set => SetProperty(ref _engineStatusText, value);
        }

        public Brush EngineStatusColor
        {
            get => _engineStatusColor;
            set => SetProperty(ref _engineStatusColor, value);
        }

        public bool IsEngineActive
        {
            get => _isEngineActive;
            set => SetProperty(ref _isEngineActive, value);
        }

        public PerformanceMetrics CurrentMetrics
        {
            get => _currentMetrics;
            set => SetProperty(ref _currentMetrics, value);
        }

        public HardwareInfo HardwareInfo
        {
            get => _hardwareInfo;
            set => SetProperty(ref _hardwareInfo, value);
        }

        public OptimizationProfile CurrentProfile
        {
            get => _currentProfile;
            set
            {
                SetProperty(ref _currentProfile, value);
                UpdateAvailableProfiles();
            }
        }

        public ObservableCollection<string> ActivityLog
        {
            get => _activityLog;
            set => SetProperty(ref _activityLog, value);
        }

        public ObservableCollection<ProcessInfo> FocusedProcesses
        {
            get => _focusedProcesses;
            set => SetProperty(ref _focusedProcesses, value);
        }

        public ObservableCollection<OptimizationProfile> AvailableProfiles
        {
            get => _availableProfiles;
            set => SetProperty(ref _availableProfiles, value);
        }

        public ObservableCollection<FocusLevel> FocusLevels
        {
            get => _focusLevels;
            set => SetProperty(ref _focusLevels, value);
        }

        public ObservableCollection<KnownBackgroundApp> KnownBackgroundApps
        {
            get => _knownBackgroundApps;
            set => SetProperty(ref _knownBackgroundApps, value);
        }

        public UserSettings Settings => _settingsService.Settings;

        public string SelectedTab
        {
            get => _selectedTab;
            set => SetProperty(ref _selectedTab, value);
        }

        public ICommand StartEngineCommand { get; }
        public ICommand StopEngineCommand { get; }
        public ICommand RestoreWindowsCommand { get; }
        public ICommand MinimizeToTrayCommand { get; }
        public ICommand SwitchProfileCommand { get; }
        public ICommand SwitchTabCommand { get; }

        public MainViewModel(EngineService engineService, LoggerService logger, SettingsService settingsService)
        {
            _engineService = engineService;
            _logger = logger;
            _settingsService = settingsService;

            StartEngineCommand = new RelayCommand(async () => await StartEngineAsync(), () => EngineState == EngineState.Stopped || EngineState == EngineState.Error);
            StopEngineCommand = new RelayCommand(() => StopEngine(), () => EngineState == EngineState.Active || EngineState == EngineState.Monitoring);
            RestoreWindowsCommand = new RelayCommand(() => RestoreWindows(), () => true);
            MinimizeToTrayCommand = new RelayCommand(() => MinimizeToTray());
            SwitchProfileCommand = new RelayCommand<string>(profile => SwitchProfile(profile));
            SwitchTabCommand = new RelayCommand<string>(tab => SelectedTab = tab);

            _engineService.OnEngineStateChanged += state => Application.Current.Dispatcher.Invoke(() => EngineState = state);
            _engineService.OnMetricsUpdated += metrics => Application.Current.Dispatcher.Invoke(() => UpdateMetrics(metrics));
            _engineService.OnEngineEvent += msg => Application.Current.Dispatcher.Invoke(() => AddLog(msg));
            _engineService.OnError += msg => Application.Current.Dispatcher.Invoke(() => AddError(msg));

            InitializeFocusLevels();
            InitializeKnownBackgroundApps();
            InitializeAsync();
        }

        private void InitializeFocusLevels()
        {
            FocusLevels.Add(FocusLevel.Minimal);
            FocusLevels.Add(FocusLevel.Competitive);
            FocusLevels.Add(FocusLevel.Maximum);
        }

        private void InitializeKnownBackgroundApps()
        {
            var defaultApps = _engineService.GetDefaultBackgroundApps();
            foreach (var app in defaultApps)
            {
                KnownBackgroundApps.Add(app);
            }
        }

        private void UpdateAvailableProfiles()
        {
            AvailableProfiles.Clear();
            var hardware = _engineService.Hardware;
            var profiles = new[]
            {
                EngineProfile.UltraPeak,
                EngineProfile.Competitive,
                EngineProfile.Stable,
                EngineProfile.SafeFallback
            };

            foreach (var p in profiles)
            {
                var profile = _engineService.CreateProfile(p, hardware);
                profile.IsCurrent = p == _currentProfile?.Profile;
                AvailableProfiles.Add(profile);
            }
        }

        public async void InitializeAsync()
        {
            await _engineService.InitializeAsync();
            HardwareInfo = _engineService.Hardware;
            CurrentProfile = _engineService.CurrentProfile;
            UpdateAvailableProfiles();
            AddLog("ASHTRON V7 initialized");
            AddLog($"Hardware: {HardwareInfo.CpuModel}, {HardwareInfo.GpuModel}, {HardwareInfo.TotalRamBytes / (1024*1024*1024)}GB RAM");
            AddLog($"Detected MSI: {HardwareInfo.MsiVersion}");
        }

        private void UpdateEngineStatusUI(EngineState state)
        {
            EngineStatusText = state switch
            {
                EngineState.Active => "ENGINE ACTIVE",
                EngineState.Starting => "STARTING...",
                EngineState.Monitoring => "MONITORING",
                EngineState.Restoring => "RESTORING...",
                EngineState.Error => "ERROR",
                _ => "ENGINE STOPPED"
            };

            EngineStatusColor = state switch
            {
                EngineState.Active => Brushes.LimeGreen,
                EngineState.Starting => Brushes.Orange,
                EngineState.Monitoring => Brushes.Cyan,
                EngineState.Restoring => Brushes.Yellow,
                EngineState.Error => Brushes.Red,
                _ => Brushes.Gray
            };

            IsEngineActive = state == EngineState.Active || state == EngineState.Monitoring;
        }

        private void UpdateMetrics(PerformanceMetrics metrics)
        {
            CurrentMetrics = metrics;
            FocusedProcesses.Clear();
            foreach (var proc in _engineService.GetFocusedProcesses())
            {
                FocusedProcesses.Add(proc);
            }
        }

        private async System.Threading.Tasks.Task StartEngineAsync()
        {
            AddLog("Starting Competitive Engine...");
            var success = await _engineService.StartEngineAsync();
            if (success)
            {
                AddLog("Competitive Engine started successfully");
            }
        }

        public void StopEngine()
        {
            AddLog("Stopping engine...");
            _engineService.StopEngine();
        }

        private void RestoreWindows()
        {
            AddLog("Restoring Windows settings...");
            _engineService.RestoreWindows();
            AddLog("Windows restored to original state");
        }

        private void MinimizeToTray()
        {
            Application.Current.MainWindow?.Hide();
        }

        private void SwitchProfile(string profileName)
        {
            if (Enum.TryParse<EngineProfile>(profileName, out var profile))
            {
                _engineService.SwitchProfileAsync(profile);
                AddLog($"Switched to {profileName} profile");
            }
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            ActivityLog.Insert(0, $"[{timestamp}] {message}");
            if (ActivityLog.Count > 100)
                ActivityLog.RemoveAt(ActivityLog.Count - 1);
        }

        private void AddError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            ActivityLog.Insert(0, $"[{timestamp}] ERROR: {message}");
            if (ActivityLog.Count > 100)
                ActivityLog.RemoveAt(ActivityLog.Count - 1);
        }
    }
}