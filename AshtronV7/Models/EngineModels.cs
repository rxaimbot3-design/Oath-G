using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;
using AshtronV7.ViewModels;

namespace AshtronV7.Models
{
    public enum EngineProfile
    {
        UltraPeak,
        Competitive,
        Stable,
        SafeFallback
    }

    public enum FocusLevel
    {
        Minimal,
        Competitive,
        Maximum
    }

    public enum EngineState
    {
        Stopped,
        Starting,
        Active,
        Monitoring,
        Restoring,
        Error
    }

    public class HardwareInfo
    {
        public string CpuModel { get; set; }
        public int LogicalProcessorCount { get; set; }
        public int PhysicalCoreCount { get; set; }
        public string GpuModel { get; set; }
        public long TotalRamBytes { get; set; }
        public long AvailableRamBytes { get; set; }
        public string WindowsVersion { get; set; }
        public string MsiVersion { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.Now;
    }

    public class PerformanceMetrics
    {
        public double CurrentFps { get; set; }
        public double AverageFps { get; set; }
        public double OnePercentLowFps { get; set; }
        public double FrameTimeMs { get; set; }
        public double FrameTimeVariance { get; set; }
        public double CpuUsage { get; set; }
        public double GpuUsage { get; set; }
        public double RamUsage { get; set; }
        public long RamUsedBytes { get; set; }
        public int ProcessCount { get; set; }
        public int UserBackgroundProcesses { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class ProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public string MainWindowTitle { get; set; }
        public long WorkingSetBytes { get; set; }
        public double CpuUsage { get; set; }
        public ProcessPriorityClass Priority { get; set; }
        public IntPtr ProcessorAffinity { get; set; }
        public bool IsCritical { get; set; }
        public ProcessCategory Category { get; set; }
    }

    public enum ProcessCategory
    {
        CriticalSystem,
        GamingRequired,
        Communication,
        UserBackground,
        Unknown
    }

    public class OptimizationProfile : ViewModelBase
    {
        private bool _isCurrent;
        public EngineProfile Profile { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ProcessPriorityClass EmulatorPriority { get; set; }
        public IntPtr EmulatorAffinity { get; set; }
        public ProcessPriorityClass DiscordPriority { get; set; }
        public IntPtr DiscordAffinity { get; set; }
        public FocusLevel FocusLevel { get; set; }
        public bool EnableGameBarDisable { get; set; }
        public bool EnableBackgroundRecordingDisable { get; set; }
        public int TimerResolutionMs { get; set; }
        public string PowerPlanGuid { get; set; }
        public bool IsCurrent
        {
            get => _isCurrent;
            set => SetProperty(ref _isCurrent, value);
        }
    }

    public class SystemBackup
    {
        public string Key { get; set; }
        public object OriginalValue { get; set; }
        public object NewValue { get; set; }
        public BackupType Type { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public enum BackupType
    {
        Registry,
        PowerPlan,
        ProcessPriority,
        ProcessAffinity,
        TimerResolution,
        GameBarSetting
    }

    public class SessionReport
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public HardwareInfo Hardware { get; set; }
        public EngineProfile AppliedProfile { get; set; }
        public List<PerformanceMetrics> MetricsHistory { get; set; } = new();
        public List<string> EngineEvents { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public double AverageFps { get; set; }
        public double OnePercentLowFps { get; set; }
        public double AverageFrameTime { get; set; }
    }

    public class UserSettings
    {
        public string Username { get; set; } = "ashtron";
        public string PasswordHash { get; set; } = "1";
        public FocusLevel DefaultFocusLevel { get; set; } = FocusLevel.Competitive;
        public EngineProfile DefaultProfile { get; set; } = EngineProfile.Competitive;
        public List<string> CustomBackgroundApps { get; set; } = new();
        public bool AutoStartEngine { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool EnableNotifications { get; set; } = true;
    }

    public class KnownBackgroundApp
    {
        public string ProcessName { get; set; }
        public string DisplayName { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}