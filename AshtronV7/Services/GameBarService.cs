using System;
using Microsoft.Win32;
using AshtronV7.Models;
using AshtronV7.Services;

namespace AshtronV7.Services
{
    public class GameBarService
    {
        private readonly LoggerService _logger;
        private readonly SystemBackupService _backupService;

        public GameBarService(LoggerService logger, SystemBackupService backupService)
        {
            _logger = logger;
            _backupService = backupService;
        }

        public bool DisableGameBar()
        {
            try
            {
                _backupService.BackupGameBarSettings();

                var gameDvrKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR";
                using (var key = Registry.CurrentUser.CreateSubKey(gameDvrKey))
                {
                    key.SetValue("AppCaptureEnabled", 0, RegistryValueKind.DWord);
                    key.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);
                    key.SetValue("HistoricalCaptureEnabled", 0, RegistryValueKind.DWord);
                    _backupService.RecordChange($"{gameDvrKey}\\AppCaptureEnabled", 0);
                    _backupService.RecordChange($"{gameDvrKey}\\GameDVR_Enabled", 0);
                    _backupService.RecordChange($"{gameDvrKey}\\HistoricalCaptureEnabled", 0);
                }

                var gameBarKey = @"SOFTWARE\Microsoft\GameBar";
                using (var key = Registry.CurrentUser.CreateSubKey(gameBarKey))
                {
                    key.SetValue("AllowAutoGameMode", 0, RegistryValueKind.DWord);
                    key.SetValue("ShowStartupPanel", 0, RegistryValueKind.DWord);
                    _backupService.RecordChange($"{gameBarKey}\\AllowAutoGameMode", 0);
                    _backupService.RecordChange($"{gameBarKey}\\ShowStartupPanel", 0);
                }

                var gameConfigKey = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Configuration";
                using (var key = Registry.LocalMachine.OpenSubKey(gameConfigKey, true))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            using var subKey = key.OpenSubKey(subKeyName, true);
                            if (subKey != null)
                            {
                                foreach (var subSubKeyName in subKey.GetSubKeyNames())
                                {
                                    using var subSubKey = subKey.OpenSubKey(subSubKeyName, true);
                                    if (subSubKey != null)
                                    {
                                        subSubKey.SetValue("GameModeEnabled", 0, RegistryValueKind.DWord);
                                    }
                                }
                            }
                        }
                    }
                }

                _logger.Log("Disabled Game Bar and Game DVR");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to disable Game Bar", ex);
                return false;
            }
        }

        public bool EnableGameMode()
        {
            try
            {
                var gameModeKey = @"SOFTWARE\Microsoft\GameBar";
                using (var key = Registry.CurrentUser.CreateSubKey(gameModeKey))
                {
                    key.SetValue("AutoGameModeEnabled", 1, RegistryValueKind.DWord);
                    _backupService.RecordChange($"{gameModeKey}\\AutoGameModeEnabled", 1);
                }

                _logger.Log("Enabled Game Mode");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to enable Game Mode", ex);
                return false;
            }
        }

        public void RestoreGameBarSettings()
        {
            _logger.Log("Game Bar settings will be restored via backup service");
        }
    }
}