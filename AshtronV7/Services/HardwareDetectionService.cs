using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using AshtronV7.Models;
using AshtronV7.Services;

namespace AshtronV7.Services
{
    public class HardwareDetectionService
    {
        private readonly LoggerService _logger;

        public HardwareDetectionService(LoggerService logger)
        {
            _logger = logger;
        }

        public HardwareInfo DetectHardware()
        {
            var info = new HardwareInfo();

            try
            {
                info.CpuModel = GetCpuModel();
                _logger.Log($"CPU: {info.CpuModel}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to detect CPU", ex);
                info.CpuModel = "Unknown";
            }

            try
            {
                info.LogicalProcessorCount = Environment.ProcessorCount;
                info.PhysicalCoreCount = GetPhysicalCoreCount();
                _logger.Log($"Logical Processors: {info.LogicalProcessorCount}, Physical Cores: {info.PhysicalCoreCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to detect processor count", ex);
                info.LogicalProcessorCount = Environment.ProcessorCount;
                info.PhysicalCoreCount = Environment.ProcessorCount;
            }

            try
            {
                info.GpuModel = GetGpuModel();
                _logger.Log($"GPU: {info.GpuModel}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to detect GPU", ex);
                info.GpuModel = "Unknown";
            }

            try
            {
                var ramInfo = GetRamInfo();
                info.TotalRamBytes = ramInfo.Total;
                info.AvailableRamBytes = ramInfo.Available;
                _logger.Log($"RAM: {info.TotalRamBytes / (1024 * 1024 * 1024)} GB Total, {info.AvailableRamBytes / (1024 * 1024 * 1024)} GB Available");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to detect RAM", ex);
            }

            try
            {
                info.WindowsVersion = GetWindowsVersion();
                _logger.Log($"Windows: {info.WindowsVersion}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to detect Windows version", ex);
            }

            try
            {
                info.MsiVersion = GetMsiVersion();
                _logger.Log($"MSI App Player: {info.MsiVersion}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to detect MSI version", ex);
            }

            return info;
        }

        private string GetCpuModel()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    return obj["Name"]?.ToString()?.Trim() ?? "Unknown";
                }
            }
            catch { }
            return "Unknown";
        }

        private int GetPhysicalCoreCount()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT NumberOfCores FROM Win32_Processor");
                int totalCores = 0;
                foreach (var obj in searcher.Get())
                {
                    totalCores += Convert.ToInt32(obj["NumberOfCores"]);
                }
                return totalCores > 0 ? totalCores : Environment.ProcessorCount;
            }
            catch { }
            return Environment.ProcessorCount;
        }

        private string GetGpuModel()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController WHERE AdapterCompatibility LIKE '%NVIDIA%' OR AdapterCompatibility LIKE '%AMD%' OR AdapterCompatibility LIKE '%Intel%'");
                foreach (var obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(name) && !name.Contains("Microsoft"))
                    {
                        return name;
                    }
                }
            }
            catch { }
            return "Unknown";
        }

        private (long Total, long Available) GetRamInfo()
        {
            long total = 0, available = 0;
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (var obj in searcher.Get())
                {
                    total = Convert.ToInt64(obj["TotalVisibleMemorySize"]) * 1024;
                    available = Convert.ToInt64(obj["FreePhysicalMemory"]) * 1024;
                }
            }
            catch { }
            return (total, available);
        }

        private string GetWindowsVersion()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                var build = key?.GetValue("CurrentBuildNumber")?.ToString();
                var releaseId = key?.GetValue("ReleaseId")?.ToString();
                var displayVersion = key?.GetValue("DisplayVersion")?.ToString();
                return $"Windows 10/11 Build {build} (Release: {releaseId ?? displayVersion ?? "Unknown"})";
            }
            catch { }
            return Environment.OSVersion.ToString();
        }

        private string GetMsiVersion()
        {
            try
            {
                var paths = new[]
                {
                    @"C:\Program Files\MSI\AppPlayer\HD-Player.exe",
                    @"C:\Program Files (x86)\MSI\AppPlayer\HD-Player.exe",
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\MSI\AppPlayer\HD-Player.exe")
                };

                foreach (var path in paths)
                {
                    if (File.Exists(path))
                    {
                        var versionInfo = FileVersionInfo.GetVersionInfo(path);
                        return $"{versionInfo.FileVersion} ({path})";
                    }
                }

                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\MSI\AppPlayer");
                if (key != null)
                {
                    var installPath = key.GetValue("InstallPath")?.ToString();
                    if (!string.IsNullOrEmpty(installPath))
                    {
                        var exePath = Path.Combine(installPath, "HD-Player.exe");
                        if (File.Exists(exePath))
                        {
                            var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                            return $"{versionInfo.FileVersion} ({exePath})";
                        }
                    }
                }
            }
            catch { }
            return "Not detected";
        }
    }
}