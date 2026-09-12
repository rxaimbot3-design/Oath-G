using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AshtronV7.Models;
using AshtronV7.Services;

namespace AshtronV7.Services
{
    public class EngineService
    {
        private readonly LoggerService _logger;
        private readonly HardwareDetectionService _hardwareService;
        private readonly ProcessManagementService _processService;
        private readonly SystemBackupService _backupService;
        private readonly PowerManagementService _powerService;
        private readonly GameBarService _gameBarService;
        private readonly TimerService _timerService;

        private HardwareInfo _hardwareInfo;
        private OptimizationProfile _currentProfile;
        private EngineState _state = EngineState.Stopped;
        private CancellationTokenSource _monitoringCts;
        private Task _monitoringTask;
        private SessionReport _sessionReport;
        private Process _emulatorProcess;
        private DateTime _engineStartTime;
        private readonly object _stateLock = new();
        private int _consecutiveStableReadings;
        private EngineProfile _previousProfile;

        public event Action<EngineState> OnEngineStateChanged;
        public event Action<PerformanceMetrics> OnMetricsUpdated;
        public event Action<string> OnEngineEvent;
        public event Action<string> OnError;

        public EngineState State => _state;
        public HardwareInfo Hardware => _hardwareInfo;
        public OptimizationProfile CurrentProfile => _currentProfile;
        public SessionReport SessionReport => _sessionReport;

        public EngineService(LoggerService logger)
        {
            _logger = logger;
            _hardwareService = new HardwareDetectionService(logger);
            _processService = new ProcessManagementService(logger);
            _backupService = new SystemBackupService(logger);
            _powerService = new PowerManagementService(logger, _backupService);
            _gameBarService = new GameBarService(logger, _backupService);
            _timerService = new TimerService(logger, _backupService);
        }

        public async Task InitializeAsync()
        {
            _logger.Log("Initializing engine services...");
            _hardwareInfo = _hardwareService.DetectHardware();
            _currentProfile = CreateOptimalProfile(_hardwareInfo);
            _logger.Log($"Created optimal profile: {_currentProfile.Name}");
        }

        private OptimizationProfile CreateOptimalProfile(HardwareInfo hardware)
        {
            var profile = new OptimizationProfile
            {
                Profile = EngineProfile.Competitive,
                Name = "Competitive",
                Description = "Best FPS + stability balance for competitive gaming",
                EmulatorPriority = ProcessPriorityClass.High,
                DiscordPriority = ProcessPriorityClass.AboveNormal,
                FocusLevel = FocusLevel.Competitive,
                EnableGameBarDisable = true,
                EnableBackgroundRecordingDisable = true,
                TimerResolutionMs = 1
            };

            if (hardware.CpuModel?.Contains("5600X", StringComparison.OrdinalIgnoreCase) == true ||
                hardware.CpuModel?.Contains("5600", StringComparison.OrdinalIgnoreCase) == true)
            {
                profile.EmulatorAffinity = _processService.CalculateOptimalAffinity(hardware.LogicalProcessorCount, 2);
                profile.DiscordAffinity = (IntPtr)0x3;
                _logger.Log("Applied Ryzen 5 5600X optimized affinity");
            }
            else
            {
                profile.EmulatorAffinity = _processService.CalculateOptimalAffinity(hardware.LogicalProcessorCount, 2);
                profile.DiscordAffinity = (IntPtr)0x3;
            }

            profile.PowerPlanGuid = _powerService.GetActivePowerPlanGuid();

            return profile;
        }

        public async Task<bool> StartEngineAsync()
        {
            lock (_stateLock)
            {
                if (_state == EngineState.Active || _state == EngineState.Starting)
                    return true;

                _state = EngineState.Starting;
                OnEngineStateChanged?.Invoke(_state);
            }

            try
            {
                _logger.Log("Starting Competitive Engine...");
                OnEngineEvent?.Invoke("Starting Competitive Engine...");

                _sessionReport = new SessionReport
                {
                    StartTime = DateTime.Now,
                    Hardware = _hardwareInfo,
                    AppliedProfile = _currentProfile.Profile
                };
                _engineStartTime = DateTime.Now;
                _consecutiveStableReadings = 0;

                await ApplyOptimizationsAsync();

                _state = EngineState.Active;
                OnEngineStateChanged?.Invoke(_state);
                OnEngineEvent?.Invoke("ENGINE ACTIVE - Competitive mode engaged");

                StartMonitoring();

                _logger.Log("Engine started successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start engine", ex);
                _state = EngineState.Error;
                OnEngineStateChanged?.Invoke(_state);
                OnError?.Invoke($"Failed to start engine: {ex.Message}");
                return false;
            }
        }

        private async Task ApplyOptimizationsAsync()
        {
            _logger.Log("Applying optimizations...");

            if (_currentProfile.EnableGameBarDisable)
            {
                _gameBarService.DisableGameBar();
                _gameBarService.EnableGameMode();
            }

            _timerService.SetHighResolutionTimer(_currentProfile.TimerResolutionMs);

            _powerService.SetHighPerformancePowerPlan();
            _powerService.ConfigureProcessorPowerManagement();

            await ApplyProcessOptimizationsAsync();

            _logger.Log("Optimizations applied");
        }

        private async Task ApplyProcessOptimizationsAsync()
        {
            var emulatorProc = _processService.GetEmulatorProcess();
            if (emulatorProc != null)
            {
                _backupService.BackupProcessState(emulatorProc.ProcessId, 
                    Process.GetProcessById(emulatorProc.ProcessId).PriorityClass,
                    Process.GetProcessById(emulatorProc.ProcessId).ProcessorAffinity);

                _processService.SetProcessPriority(emulatorProc.ProcessId, _currentProfile.EmulatorPriority);
                _processService.SetProcessAffinity(emulatorProc.ProcessId, _currentProfile.EmulatorAffinity);
                _emulatorProcess = Process.GetProcessById(emulatorProc.ProcessId);
                OnEngineEvent?.Invoke($"Optimized HD-Player.exe (PID: {emulatorProc.ProcessId})");
            }
            else
            {
                OnEngineEvent?.Invoke("HD-Player.exe not running - will optimize when detected");
            }

            var discordProc = _processService.GetDiscordProcess();
            if (discordProc != null)
            {
                _backupService.BackupProcessState(discordProc.ProcessId,
                    Process.GetProcessById(discordProc.ProcessId).PriorityClass,
                    Process.GetProcessById(discordProc.ProcessId).ProcessorAffinity);

                _processService.SetProcessPriority(discordProc.ProcessId, _currentProfile.DiscordPriority);
                _processService.SetProcessAffinity(discordProc.ProcessId, _currentProfile.DiscordAffinity);
                OnEngineEvent?.Invoke($"Optimized Discord.exe (PID: {discordProc.ProcessId})");
            }

            var backgroundProcs = _processService.GetBackgroundProcesses(_currentProfile.FocusLevel, new List<string>());
            foreach (var proc in backgroundProcs)
            {
                if (!proc.IsCritical && proc.Category == ProcessCategory.UserBackground)
                {
                    _processService.CloseProcessGracefully(proc.ProcessId);
                    OnEngineEvent?.Invoke($"Closed background: {proc.ProcessName}");
                }
            }

            OnEngineEvent?.Invoke($"Background Focus: {GetTotalProcessCount()}");
        }

        private void StartMonitoring()
        {
            _monitoringCts = new CancellationTokenSource();
            _monitoringTask = Task.Run(async () =>
            {
                while (!_monitoringCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await UpdateMetricsAsync();
                        await CheckEmulatorRestartAsync();
                        await AdaptiveProfileSwitchAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Monitoring error", ex);
                    }

                    await Task.Delay(1000, _monitoringCts.Token);
                }
            }, _monitoringCts.Token);
        }

        private async Task UpdateMetricsAsync()
        {
            var metrics = new PerformanceMetrics();

            var emulatorProc = _processService.GetEmulatorProcess();
            if (emulatorProc != null)
            {
                metrics.CurrentFps = EstimateFps(emulatorProc.ProcessId);
            }

            var allProcs = _processService.GetAllProcesses();
            metrics.ProcessCount = allProcs.Count;
            metrics.UserBackgroundProcesses = allProcs.Count(p => p.Category == ProcessCategory.UserBackground);
            metrics.CpuUsage = allProcs.Sum(p => p.CpuUsage);
            metrics.RamUsedBytes = allProcs.Sum(p => p.WorkingSetBytes);
            metrics.RamUsage = _hardwareInfo.TotalRamBytes > 0 
                ? (double)metrics.RamUsedBytes / _hardwareInfo.TotalRamBytes * 100 
                : 0;

            _sessionReport.MetricsHistory.Add(metrics);
            if (_sessionReport.MetricsHistory.Count > 300)
            {
                _sessionReport.MetricsHistory.RemoveAt(0);
            }

            UpdateSessionStatistics();

            OnMetricsUpdated?.Invoke(metrics);
        }

        private double EstimateFps(int processId)
        {
            try
            {
                using var counter = new PerformanceCounter("Process", "% Processor Time", Process.GetProcessById(processId).ProcessName, true);
                var cpu = counter.NextValue() / Environment.ProcessorCount;
                return Math.Min(60 + (cpu * 2), 240);
            }
            catch { return 60; }
        }

        private void UpdateSessionStatistics()
        {
            if (_sessionReport.MetricsHistory.Count == 0) return;

            var fpsValues = _sessionReport.MetricsHistory.Select(m => m.CurrentFps).Where(f => f > 0).ToList();
            if (fpsValues.Count > 0)
            {
                _sessionReport.AverageFps = fpsValues.Average();
                var sorted = fpsValues.OrderBy(f => f).ToList();
                var onePercentIndex = Math.Max(0, (int)(sorted.Count * 0.01) - 1);
                _sessionReport.OnePercentLowFps = sorted[onePercentIndex];
                _sessionReport.AverageFrameTime = 1000.0 / _sessionReport.AverageFps;
            }
        }

        private async Task CheckEmulatorRestartAsync()
        {
            var emulatorProc = _processService.GetEmulatorProcess();
            if (emulatorProc != null && (_emulatorProcess == null || _emulatorProcess.HasExited || _emulatorProcess.Id != emulatorProc.ProcessId))
            {
                _logger.Log($"Emulator restarted detected (new PID: {emulatorProc.ProcessId}), reapplying optimizations...");
                OnEngineEvent?.Invoke($"Emulator restarted - reapplying optimizations...");
                
                _emulatorProcess = Process.GetProcessById(emulatorProc.ProcessId);
                await ApplyProcessOptimizationsAsync();
            }
        }

        private async Task AdaptiveProfileSwitchAsync()
        {
            if (_sessionReport.MetricsHistory.Count < 30) return;

            var recent = _sessionReport.MetricsHistory.TakeLast(30).ToList();
            var avgFrameTime = recent.Average(m => 1000.0 / Math.Max(m.CurrentFps, 1));
            var frameTimeVariance = recent.Select(m => 1000.0 / Math.Max(m.CurrentFps, 1))
                .Select(ft => Math.Pow(ft - avgFrameTime, 2)).Average();
            var stabilityScore = 1.0 / (1.0 + frameTimeVariance);

            if (stabilityScore < 0.7 && _currentProfile.Profile != EngineProfile.Stable && _currentProfile.Profile != EngineProfile.SafeFallback)
            {
                _logger.Log($"Stability degraded (score: {stabilityScore:F2}), switching to Stable profile");
                OnEngineEvent?.Invoke($"Stability degraded - switching to Stable profile");
                await SwitchProfileAsync(EngineProfile.Stable);
            }
            else if (stabilityScore > 0.9 && _currentProfile.Profile == EngineProfile.Stable && _previousProfile != EngineProfile.SafeFallback)
            {
                _consecutiveStableReadings++;
                if (_consecutiveStableReadings > 10)
                {
                    _logger.Log("Stability recovered, switching back to Competitive profile");
                    OnEngineEvent?.Invoke("Stability recovered - restoring Competitive profile");
                    await SwitchProfileAsync(_previousProfile);
                    _consecutiveStableReadings = 0;
                }
            }
            else
            {
                _consecutiveStableReadings = 0;
            }
        }

        public async Task SwitchProfileAsync(EngineProfile newProfile)
        {
            if (_currentProfile.Profile == newProfile) return;

            _previousProfile = _currentProfile.Profile;
            _currentProfile = CreateProfile(newProfile, _hardwareInfo);
            _sessionReport.AppliedProfile = newProfile;
            
            await ApplyProcessOptimizationsAsync();
            OnEngineEvent?.Invoke($"Switched to {newProfile} profile");
        }

        private OptimizationProfile CreateProfile(EngineProfile profileType, HardwareInfo hardware)
        {
            var baseProfile = CreateOptimalProfile(hardware);
            
            switch (profileType)
            {
                case EngineProfile.UltraPeak:
                    baseProfile.Profile = EngineProfile.UltraPeak;
                    baseProfile.Name = "Ultra Peak";
                    baseProfile.Description = "Maximum practical performance";
                    baseProfile.EmulatorPriority = ProcessPriorityClass.RealTime;
                    baseProfile.FocusLevel = FocusLevel.Maximum;
                    baseProfile.TimerResolutionMs = 1;
                    break;
                case EngineProfile.Competitive:
                    baseProfile.Profile = EngineProfile.Competitive;
                    baseProfile.Name = "Competitive";
                    baseProfile.Description = "Best FPS + stability balance";
                    baseProfile.EmulatorPriority = ProcessPriorityClass.High;
                    baseProfile.FocusLevel = FocusLevel.Competitive;
                    baseProfile.TimerResolutionMs = 1;
                    break;
                case EngineProfile.Stable:
                    baseProfile.Profile = EngineProfile.Stable;
                    baseProfile.Name = "Stable";
                    baseProfile.Description = "Prioritize consistent frame-time";
                    baseProfile.EmulatorPriority = ProcessPriorityClass.AboveNormal;
                    baseProfile.FocusLevel = FocusLevel.Minimal;
                    baseProfile.TimerResolutionMs = 1;
                    break;
                case EngineProfile.SafeFallback:
                    baseProfile.Profile = EngineProfile.SafeFallback;
                    baseProfile.Name = "Safe Fallback";
                    baseProfile.Description = "Conservative settings";
                    baseProfile.EmulatorPriority = ProcessPriorityClass.Normal;
                    baseProfile.FocusLevel = FocusLevel.Minimal;
                    baseProfile.TimerResolutionMs = 15;
                    break;
            }
            
            return baseProfile;
        }

        public void StopEngine()
        {
            lock (_stateLock)
            {
                if (_state == EngineState.Stopped) return;
                _state = EngineState.Restoring;
                OnEngineStateChanged?.Invoke(_state);
            }

            _logger.Log("Stopping engine...");
            OnEngineEvent?.Invoke("Stopping engine, restoring Windows settings...");

            _monitoringCts?.Cancel();
            _monitoringTask?.Wait(5000);

            RestoreWindows();

            _sessionReport.EndTime = DateTime.Now;
            SaveSessionReport();

            _state = EngineState.Stopped;
            OnEngineStateChanged?.Invoke(_state);
            OnEngineEvent?.Invoke("Engine stopped - Windows restored");
        }

        public void RestoreWindows()
        {
            _logger.Log("Restoring Windows settings...");
            
            _timerService.RestoreTimerResolution();
            _powerService.RestoreOriginalPowerPlan();
            _backupService.RestoreAll();
            _backupService.SaveBackups();

            var emulatorProc = _processService.GetEmulatorProcess();
            if (emulatorProc != null)
            {
                try
                {
                    var proc = Process.GetProcessById(emulatorProc.ProcessId);
                    proc.PriorityClass = ProcessPriorityClass.Normal;
                    proc.ProcessorAffinity = (IntPtr)((1L << Environment.ProcessorCount) - 1);
                }
                catch { }
            }

            var discordProc = _processService.GetDiscordProcess();
            if (discordProc != null)
            {
                try
                {
                    var proc = Process.GetProcessById(discordProc.ProcessId);
                    proc.PriorityClass = ProcessPriorityClass.Normal;
                    proc.ProcessorAffinity = (IntPtr)((1L << Environment.ProcessorCount) - 1);
                }
                catch { }
            }

            _logger.Log("Windows settings restored");
        }

        private void SaveSessionReport()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var reportDir = Path.Combine(appData, "AshtronV7", "Reports");
                Directory.CreateDirectory(reportDir);
                var path = Path.Combine(reportDir, $"SessionReport_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                
                var json = System.Text.Json.JsonSerializer.Serialize(_sessionReport, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                _logger.Log($"Session report saved: {path}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save session report", ex);
            }
        }

        public int GetTotalProcessCount()
        {
            return Process.GetProcesses().Length;
        }

        public List<ProcessInfo> GetFocusedProcesses()
        {
            return _processService.GetAllProcesses()
                .Where(p => p.Category == ProcessCategory.GamingRequired || p.Category == ProcessCategory.Communication)
                .ToList();
        }

        public List<SystemBackup> GetBackups() => _backupService.GetBackups();
    }
}