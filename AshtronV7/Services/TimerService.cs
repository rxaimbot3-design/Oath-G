using System;
using System.Runtime.InteropServices;
using AshtronV7.Models;
using AshtronV7.Services;

namespace AshtronV7.Services
{
    public class TimerService
    {
        private readonly LoggerService _logger;
        private readonly SystemBackupService _backupService;
        private int _originalResolution;
        private bool _isHighResolutionSet;

        [DllImport("ntdll.dll")]
        private static extern int NtQueryTimerResolution(out int MinimumResolution, out int MaximumResolution, out int CurrentResolution);

        [DllImport("ntdll.dll")]
        private static extern int NtSetTimerResolution(int DesiredResolution, bool SetResolution, out int CurrentResolution);

        public TimerService(LoggerService logger, SystemBackupService backupService)
        {
            _logger = logger;
            _backupService = backupService;
        }

        public bool SetHighResolutionTimer(int resolutionMs = 1)
        {
            try
            {
                NtQueryTimerResolution(out int minRes, out int maxRes, out int currentRes);
                _originalResolution = currentRes / 10000;
                _backupService.BackupTimerResolution(_originalResolution);

                var desiredResolution = resolutionMs * 10000;
                if (desiredResolution < minRes) desiredResolution = minRes;
                if (desiredResolution > maxRes) desiredResolution = maxRes;

                var result = NtSetTimerResolution(desiredResolution, true, out int newRes);
                if (result == 0)
                {
                    _isHighResolutionSet = true;
                    _backupService.RecordChange("TimerResolution", resolutionMs);
                    _logger.Log($"Set timer resolution to {newRes / 10000}ms (requested: {resolutionMs}ms)");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"Failed to set timer resolution, NtSetTimerResolution returned: {result}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to set high resolution timer", ex);
            }
            return false;
        }

        public void RestoreTimerResolution()
        {
            if (_isHighResolutionSet && _originalResolution > 0)
            {
                try
                {
                    var desiredResolution = _originalResolution * 10000;
                    NtSetTimerResolution(desiredResolution, false, out _);
                    _isHighResolutionSet = false;
                    _logger.Log($"Restored timer resolution to {_originalResolution}ms");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to restore timer resolution", ex);
                }
            }
        }

        public (int Min, int Max, int Current) GetTimerResolution()
        {
            try
            {
                NtQueryTimerResolution(out int minRes, out int maxRes, out int currentRes);
                return (minRes / 10000, maxRes / 10000, currentRes / 10000);
            }
            catch
            {
                return (0, 0, 0);
            }
        }
    }
}