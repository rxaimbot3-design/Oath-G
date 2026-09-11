using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using AshtronV7.Models;
using AshtronV7.Services;

namespace AshtronV7.Services
{
    public class ProcessManagementService
    {
        private readonly LoggerService _logger;
        private readonly List<string> _criticalProcesses = new()
        {
            "System", "Registry", "csrss", "wininit", "winlogon", "lsass",
            "services", "svchost", "dwm", "explorer", "taskhostw",
            "SecurityHealthService", "MsMpEng", "NisSrv", "WmiPrvSE",
            "spoolsv", "audiodg", "RTKAudioService", "RtkAudUService64",
            "nvcontainer", "nvsphelper64", "NVIDIA Web Helper",
            "amsi", "smss", "fontdrvhost", "dllhost", "WUDFHost"
        };

        private readonly List<string> _gamingProcesses = new()
        {
            "HD-Player", "HD-Player.exe", "MEmu", "MEmuHeadless",
            "Nox", "NoxVMHandle", "BlueStacks", "HD-Player"
        };

        private readonly List<string> _communicationProcesses = new()
        {
            "Discord", "Discord.exe", "DiscordCanary", "DiscordPTB",
            "Teams", "ms-teams", "Slack", "Telegram", "Telegram.exe"
        };

        private readonly List<KnownBackgroundApp> _defaultBackgroundApps = new()
        {
            new KnownBackgroundApp { ProcessName = "chrome", DisplayName = "Google Chrome" },
            new KnownBackgroundApp { ProcessName = "msedge", DisplayName = "Microsoft Edge" },
            new KnownBackgroundApp { ProcessName = "firefox", DisplayName = "Mozilla Firefox" },
            new KnownBackgroundApp { ProcessName = "Spotify", DisplayName = "Spotify" },
            new KnownBackgroundApp { ProcessName = "Steam", DisplayName = "Steam" },
            new KnownBackgroundApp { ProcessName = "EpicGamesLauncher", DisplayName = "Epic Games Launcher" },
            new KnownBackgroundApp { ProcessName = "Telegram", DisplayName = "Telegram" },
            new KnownBackgroundApp { ProcessName = "Teams", DisplayName = "Microsoft Teams" },
            new KnownBackgroundApp { ProcessName = "Adobe", DisplayName = "Adobe Background" },
            new KnownBackgroundApp { ProcessName = "CCXProcess", DisplayName = "Adobe Creative Cloud" },
            new KnownBackgroundApp { ProcessName = "CoreSync", DisplayName = "Adobe Core Sync" }
        };

        public ProcessManagementService(LoggerService logger)
        {
            _logger = logger;
        }

        public List<ProcessInfo> GetAllProcesses()
        {
            var processes = new List<ProcessInfo>();
            try
            {
                var allProcesses = Process.GetProcesses();
                foreach (var proc in allProcesses)
                {
                    try
                    {
                        var info = CreateProcessInfo(proc);
                        processes.Add(info);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to get info for process {proc.ProcessName}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to enumerate processes", ex);
            }
            return processes;
        }

        private ProcessInfo CreateProcessInfo(Process proc)
        {
            var info = new ProcessInfo
            {
                ProcessId = proc.Id,
                ProcessName = proc.ProcessName,
                MainWindowTitle = proc.MainWindowTitle ?? "",
                WorkingSetBytes = proc.WorkingSet64,
                Priority = proc.BasePriority >= 13 ? ProcessPriorityClass.High : 
                          proc.BasePriority >= 10 ? ProcessPriorityClass.AboveNormal :
                          proc.BasePriority >= 8 ? ProcessPriorityClass.Normal :
                          ProcessPriorityClass.BelowNormal,
                ProcessorAffinity = proc.ProcessorAffinity,
                IsCritical = IsCriticalProcess(proc.ProcessName),
                Category = CategorizeProcess(proc.ProcessName)
            };

            try
            {
                info.CpuUsage = GetProcessCpuUsage(proc);
            }
            catch { info.CpuUsage = 0; }

            return info;
        }

        private bool IsCriticalProcess(string processName)
        {
            var lowerName = processName.ToLowerInvariant();
            return _criticalProcesses.Any(p => lowerName.Contains(p.ToLowerInvariant()));
        }

        private ProcessCategory CategorizeProcess(string processName)
        {
            var lowerName = processName.ToLowerInvariant();

            if (_criticalProcesses.Any(p => lowerName.Contains(p.ToLowerInvariant())))
                return ProcessCategory.CriticalSystem;

            if (_gamingProcesses.Any(p => lowerName.Contains(p.ToLowerInvariant())))
                return ProcessCategory.GamingRequired;

            if (_communicationProcesses.Any(p => lowerName.Contains(p.ToLowerInvariant())))
                return ProcessCategory.Communication;

            if (_defaultBackgroundApps.Any(p => lowerName.Contains(p.ProcessName.ToLowerInvariant())))
                return ProcessCategory.UserBackground;

            return ProcessCategory.Unknown;
        }

        private double GetProcessCpuUsage(Process proc)
        {
            try
            {
                using var cpuCounter = new PerformanceCounter("Process", "% Processor Time", proc.ProcessName, true);
                cpuCounter.NextValue();
                System.Threading.Thread.Sleep(50);
                return cpuCounter.NextValue() / Environment.ProcessorCount;
            }
            catch { return 0; }
        }

        public ProcessInfo GetEmulatorProcess()
        {
            var processes = Process.GetProcessesByName("HD-Player");
            if (processes.Length > 0)
            {
                return CreateProcessInfo(processes[0]);
            }
            return null;
        }

        public ProcessInfo GetDiscordProcess()
        {
            var names = new[] { "Discord", "DiscordCanary", "DiscordPTB" };
            foreach (var name in names)
            {
                var processes = Process.GetProcessesByName(name);
                if (processes.Length > 0)
                {
                    return CreateProcessInfo(processes[0]);
                }
            }
            return null;
        }

        public List<ProcessInfo> GetBackgroundProcesses(FocusLevel level, List<string> customApps)
        {
            var allProcesses = GetAllProcesses();
            var backgroundApps = new List<string>(_defaultBackgroundApps.Where(a => a.IsEnabled).Select(a => a.ProcessName));
            backgroundApps.AddRange(customApps);

            var candidates = allProcesses.Where(p => 
                p.Category == ProcessCategory.UserBackground ||
                backgroundApps.Any(app => p.ProcessName.ToLowerInvariant().Contains(app.ToLowerInvariant()))
            ).ToList();

            if (level == FocusLevel.Minimal)
            {
                return candidates.Take(3).ToList();
            }
            else if (level == FocusLevel.Competitive)
            {
                return candidates.Take(8).ToList();
            }
            else
            {
                return candidates.Take(15).ToList();
            }
        }

        public bool SetProcessPriority(int processId, ProcessPriorityClass priority)
        {
            try
            {
                var proc = Process.GetProcessById(processId);
                proc.PriorityClass = priority;
                _logger.Log($"Set priority of {proc.ProcessName} (PID: {processId}) to {priority}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to set priority for PID {processId}", ex);
                return false;
            }
        }

        public bool SetProcessAffinity(int processId, IntPtr affinityMask)
        {
            try
            {
                var proc = Process.GetProcessById(processId);
                proc.ProcessorAffinity = affinityMask;
                _logger.Log($"Set affinity of {proc.ProcessName} (PID: {processId}) to 0x{affinityMask.ToString("X")}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to set affinity for PID {processId}", ex);
                return false;
            }
        }

        public IntPtr CalculateOptimalAffinity(int logicalProcessors, int reservedForSystem = 2)
        {
            if (logicalProcessors <= reservedForSystem)
                return (IntPtr)((1 << logicalProcessors) - 1);

            var availableCores = logicalProcessors - reservedForSystem;
            var mask = 0L;
            for (int i = reservedForSystem; i < logicalProcessors; i++)
            {
                mask |= (1L << i);
            }
            return (IntPtr)mask;
        }

        public bool CloseProcessGracefully(int processId, int timeoutMs = 5000)
        {
            try
            {
                var proc = Process.GetProcessById(processId);
                if (proc.CloseMainWindow())
                {
                    if (!proc.WaitForExit(timeoutMs))
                    {
                        _logger.LogWarning($"Process {proc.ProcessName} (PID: {processId}) did not close gracefully");
                        return false;
                    }
                    _logger.Log($"Closed {proc.ProcessName} (PID: {processId}) gracefully");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to close process {processId} gracefully", ex);
            }
            return false;
        }

        public bool TerminateProcess(int processId)
        {
            try
            {
                var proc = Process.GetProcessById(processId);
                proc.Kill();
                _logger.Log($"Terminated {proc.ProcessName} (PID: {processId})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to terminate process {processId}", ex);
                return false;
            }
        }

        public List<KnownBackgroundApp> GetDefaultBackgroundApps() => _defaultBackgroundApps;
    }
}