using System;
using System.IO;
using System.Text.Json;
using AshtronV7.Models;
using AshtronV7.Services;

namespace AshtronV7.Services
{
    public class SettingsService
    {
        private readonly LoggerService _logger;
        private readonly string _settingsPath;
        private UserSettings _settings;

        public SettingsService(LoggerService logger)
        {
            _logger = logger;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var settingsDir = Path.Combine(appData, "AshtronV7");
            Directory.CreateDirectory(settingsDir);
            _settingsPath = Path.Combine(settingsDir, "settings.json");
            LoadSettings();
        }

        public UserSettings Settings => _settings;

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
                    _logger.Log("Settings loaded");
                }
                else
                {
                    _settings = new UserSettings();
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load settings", ex);
                _settings = new UserSettings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
                _logger.Log("Settings saved");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save settings", ex);
            }
        }

        public bool ValidateCredentials(string username, string password)
        {
            return username == _settings.Username && password == _settings.PasswordHash;
        }

        public void UpdateCredentials(string username, string password)
        {
            _settings.Username = username;
            _settings.PasswordHash = password;
            SaveSettings();
        }
    }
}