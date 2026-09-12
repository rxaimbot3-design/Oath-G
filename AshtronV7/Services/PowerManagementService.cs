using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using AshtronV7.Models;
using AshtronV7.Services;

namespace AshtronV7.Services
{
    public class PowerManagementService
    {
        private readonly LoggerService _logger;
        private readonly SystemBackupService _backupService;
        private string _originalPowerPlanGuid;

        public PowerManagementService(LoggerService logger, SystemBackupService backupService)
        {
            _logger = logger;
            _backupService = backupService;
        }

        public string GetActivePowerPlanGuid()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/getactivescheme",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                var process = Process.Start(psi);
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                var parts = output.Split(':');
                if (parts.Length > 1)
                {
                    var guid = parts[1].Split('(')[0].Trim();
                    return guid;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get active power plan", ex);
            }
            return null;
        }

        public bool SetHighPerformancePowerPlan()
        {
            try
            {
                _originalPowerPlanGuid = GetActivePowerPlanGuid();
                if (!string.IsNullOrEmpty(_originalPowerPlanGuid))
                {
                    _backupService.BackupPowerPlan(_originalPowerPlanGuid);
                }

                var highPerfGuid = GetHighPerformanceGuid();
                if (string.IsNullOrEmpty(highPerfGuid))
                {
                    highPerfGuid = GetUltimatePerformanceGuid();
                }

                if (string.IsNullOrEmpty(highPerfGuid))
                {
                    _logger.LogWarning("No high performance power plan found");
                    return false;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = $"/setactive {highPerfGuid}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var process = Process.Start(psi);
                process.WaitForExit(5000);

                var newActive = GetActivePowerPlanGuid();
                if (newActive == highPerfGuid)
                {
                    _backupService.RecordChange($"PowerPlan_{_originalPowerPlanGuid}", highPerfGuid);
                    _logger.Log($"Set power plan to High Performance: {highPerfGuid}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to set high performance power plan", ex);
            }
            return false;
        }

        private string GetHighPerformanceGuid()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/list",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                var process = Process.Start(psi);
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("High performance", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("High Performance", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1)
                        {
                            return parts[1].Split('(')[0].Trim();
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private string GetUltimatePerformanceGuid()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/list",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                var process = Process.Start(psi);
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("Ultimate", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1)
                        {
                            return parts[1].Split('(')[0].Trim();
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public void RestoreOriginalPowerPlan()
        {
            if (!string.IsNullOrEmpty(_originalPowerPlanGuid))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = $"/setactive {_originalPowerPlanGuid}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi)?.WaitForExit(5000);
                    _logger.Log($"Restored original power plan: {_originalPowerPlanGuid}");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to restore original power plan", ex);
                }
            }
        }

        public void ConfigureProcessorPowerManagement()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/setacvalueindex scheme_current sub_processor PROCTHROTTLEMAX 100",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi)?.WaitForExit(3000);

                psi.Arguments = "/setacvalueindex scheme_current sub_processor PROCTHROTTLEMIN 100";
                Process.Start(psi)?.WaitForExit(3000);

                psi.Arguments = "/setactive scheme_current";
                Process.Start(psi)?.WaitForExit(3000);

                _logger.Log("Configured processor power management for maximum performance");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to configure processor power management", ex);
            }
        }
    }
}