using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using AshtronV7.Models;
using AshtronV7.Services;

namespace AshtronV7.Services
{
    public class SystemBackupService
    {
        private readonly LoggerService _logger;
        private readonly List<SystemBackup> _backups = new();
        private readonly string _backupPath;

        public SystemBackupService(LoggerService logger)
        {
            _logger = logger;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var backupDir = Path.Combine(appData, "AshtronV7", "Backups");
            Directory.CreateDirectory(backupDir);
            _backupPath = Path.Combine(backupDir, "backup.json");
            LoadBackups();
        }

        public void BackupRegistryValue(string keyPath, string valueName, BackupType type)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, true) 
                    ?? Registry.LocalMachine.OpenSubKey(keyPath, true);
                if (key != null)
                {
                    var originalValue = key.GetValue(valueName);
                    var backup = new SystemBackup
                    {
                        Key = $"{keyPath}\\{valueName}",
                        OriginalValue = originalValue,
                        Type = type
                    };
                    _backups.Add(backup);
                    _logger.Log($"Backed up registry: {backup.Key} = {originalValue}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to backup registry {keyPath}\\{valueName}", ex);
            }
        }

        public void BackupRegistryKey(string keyPath, BackupType type)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, true)
                    ?? Registry.LocalMachine.OpenSubKey(keyPath, true);
                if (key != null)
                {
                    var valueNames = key.GetValueNames();
                    foreach (var valueName in valueNames)
                    {
                        var originalValue = key.GetValue(valueName);
                        var backup = new SystemBackup
                        {
                            Key = $"{keyPath}\\{valueName}",
                            OriginalValue = originalValue,
                            Type = type
                        };
                        _backups.Add(backup);
                    }
                    _logger.Log($"Backed up registry key: {keyPath} ({valueNames.Length} values)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to backup registry key {keyPath}", ex);
            }
        }

        public void BackupPowerPlan(string planGuid)
        {
            try
            {
                var backup = new SystemBackup
                {
                    Key = $"PowerPlan_{planGuid}",
                    OriginalValue = planGuid,
                    Type = BackupType.PowerPlan
                };
                _backups.Add(backup);
                _logger.Log($"Backed up active power plan: {planGuid}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to backup power plan", ex);
            }
        }

        public void BackupProcessState(int processId, ProcessPriorityClass originalPriority, IntPtr originalAffinity)
        {
            try
            {
                var proc = Process.GetProcessById(processId);
                var backup = new SystemBackup
                {
                    Key = $"Process_{proc.ProcessName}_{processId}",
                    OriginalValue = new { Priority = originalPriority.ToString(), Affinity = originalAffinity.ToString() },
                    Type = BackupType.ProcessPriority
                };
                _backups.Add(backup);
                _logger.Log($"Backed up process state: {proc.ProcessName} (PID: {processId})");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to backup process state for PID {processId}", ex);
            }
        }

        public void BackupTimerResolution(int originalResolution)
        {
            var backup = new SystemBackup
            {
                Key = "TimerResolution",
                OriginalValue = originalResolution,
                Type = BackupType.TimerResolution
            };
            _backups.Add(backup);
            _logger.Log($"Backed up timer resolution: {originalResolution}ms");
        }

        public void BackupGameBarSettings()
        {
            try
            {
                var gameBarKeys = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR",
                    @"SOFTWARE\Microsoft\GameBar"
                };

                foreach (var keyPath in gameBarKeys)
                {
                    BackupRegistryKey(keyPath, BackupType.GameBarSetting);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to backup Game Bar settings", ex);
            }
        }

        public void RecordChange(string key, object newValue)
        {
            var backup = _backups.Find(b => b.Key == key);
            if (backup != null)
            {
                backup.NewValue = newValue;
                _logger.Log($"Recorded change: {key} = {newValue}");
            }
        }

        public bool RestoreAll()
        {
            bool allSuccess = true;
            var grouped = _backups.GroupBy(b => b.Type).ToList();

            foreach (var group in grouped)
            {
                try
                {
                    switch (group.Key)
                    {
                        case BackupType.Registry:
                        case BackupType.GameBarSetting:
                            RestoreRegistry(group.ToList());
                            break;
                        case BackupType.PowerPlan:
                            RestorePowerPlan(group.ToList());
                            break;
                        case BackupType.ProcessPriority:
                        case BackupType.ProcessAffinity:
                            RestoreProcessState(group.ToList());
                            break;
                        case BackupType.TimerResolution:
                            RestoreTimerResolution(group.ToList());
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to restore {group.Key}", ex);
                    allSuccess = false;
                }
            }

            _logger.Log($"Restore completed. Success: {allSuccess}");
            return allSuccess;
        }

        private void RestoreRegistry(List<SystemBackup> backups)
        {
            foreach (var backup in backups)
            {
                try
                {
                    var lastSlash = backup.Key.LastIndexOf('\\');
                    if (lastSlash > 0)
                    {
                        var keyPath = backup.Key.Substring(0, lastSlash);
                        var valueName = backup.Key.Substring(lastSlash + 1);

                        using var key = Registry.CurrentUser.OpenSubKey(keyPath, true)
                            ?? Registry.LocalMachine.OpenSubKey(keyPath, true);
                        if (key != null)
                        {
                            if (backup.OriginalValue == null)
                            {
                                key.DeleteValue(valueName, false);
                            }
                            else
                            {
                                key.SetValue(valueName, backup.OriginalValue);
                            }
                            _logger.Log($"Restored registry: {backup.Key} = {backup.OriginalValue}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to restore registry {backup.Key}", ex);
                }
            }
        }

        private void RestorePowerPlan(List<SystemBackup> backups)
        {
            foreach (var backup in backups)
            {
                try
                {
                    var planGuid = backup.OriginalValue?.ToString();
                    if (!string.IsNullOrEmpty(planGuid))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "powercfg",
                            Arguments = $"/setactive {planGuid}",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        Process.Start(psi)?.WaitForExit(5000);
                        _logger.Log($"Restored power plan: {planGuid}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to restore power plan", ex);
                }
            }
        }

        private void RestoreProcessState(List<SystemBackup> backups)
        {
            foreach (var backup in backups)
            {
                try
                {
                    var parts = backup.Key.Split('_');
                    if (parts.Length >= 3 && int.TryParse(parts[^1], out int pid))
                    {
                        var proc = Process.GetProcessById(pid);
                        var original = JsonSerializer.Deserialize<dynamic>(backup.OriginalValue.ToString());
                        if (original != null)
                        {
                            if (Enum.TryParse<ProcessPriorityClass>(original.Priority.ToString(), out ProcessPriorityClass priority))
                            {
                                proc.PriorityClass = priority;
                            }
                            if (IntPtr.TryParse(original.Affinity.ToString(), out IntPtr affinity))
                            {
                                proc.ProcessorAffinity = affinity;
                            }
                            _logger.Log($"Restored process state: {proc.ProcessName} (PID: {pid})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to restore process state {backup.Key}", ex);
                }
            }
        }

        private void RestoreTimerResolution(List<SystemBackup> backups)
        {
            foreach (var backup in backups)
            {
                try
                {
                    if (backup.OriginalValue is int resolution)
                    {
                        SetTimerResolution(resolution);
                        _logger.Log($"Restored timer resolution: {resolution}ms");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to restore timer resolution", ex);
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("ntdll.dll")]
        private static extern int NtSetTimerResolution(int DesiredResolution, bool SetResolution, out int CurrentResolution);

        private void SetTimerResolution(int resolution)
        {
            NtSetTimerResolution(resolution * 10000, true, out _);
        }

        public void SaveBackups()
        {
            try
            {
                var json = JsonSerializer.Serialize(_backups, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_backupPath, json);
                _logger.Log($"Saved {_backups.Count} backups to {_backupPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save backups", ex);
            }
        }

        private void LoadBackups()
        {
            try
            {
                if (File.Exists(_backupPath))
                {
                    var json = File.ReadAllText(_backupPath);
                    var loaded = JsonSerializer.Deserialize<List<SystemBackup>>(json);
                    if (loaded != null)
                    {
                        _backups.AddRange(loaded);
                        _logger.Log($"Loaded {loaded.Count} previous backups");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load backups", ex);
            }
        }

        public List<SystemBackup> GetBackups() => _backups;
        public void ClearBackups() => _backups.Clear();
    }
}