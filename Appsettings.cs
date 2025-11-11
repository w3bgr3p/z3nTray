using System;
using System.Configuration;

namespace OtpTrayApp
{
    public class AppSettings
    {
        // Browser process limits
        public int MaxMemoryForInstance { get; set; }
        public int MaxAgeForInstance { get; set; }
        
        // Main ZennoPoster process limit
        public int MaxMemoryForZennoposter { get; set; }
        
        // Kill flags
        public bool KillOld { get; set; }
        public bool KillHeavy { get; set; }
        public bool KillMain { get; set; }
        
        // Auto-check settings
        public int AutoCheckInterval { get; set; } // minutes, 0 = disabled
        
        // UI settings
        public bool ShowLogs { get; set; }
        public bool ShowRawCommandLine { get; set; }

        // Default values
        public AppSettings()
        {
            MaxMemoryForInstance = 1000;
            MaxAgeForInstance = 30;
            MaxMemoryForZennoposter = 20000;
            KillOld = true;
            KillHeavy = true;
            KillMain = false;
            AutoCheckInterval = 0;
            ShowLogs = false;
            ShowRawCommandLine = false;
        }

        // Load from app.config
        public static AppSettings Load()
        {
            var settings = new AppSettings();
            
            try
            {
                settings.MaxMemoryForInstance = GetInt("MaxMemoryForInstance", 1000);
                settings.MaxAgeForInstance = GetInt("MaxAgeForInstance", 30);
                settings.MaxMemoryForZennoposter = GetInt("MaxMemoryForZennoposter", 20000);
                settings.KillOld = GetBool("KillOld", true);
                settings.KillHeavy = GetBool("KillHeavy", true);
                settings.KillMain = GetBool("KillMain", false);
                settings.AutoCheckInterval = GetInt("AutoCheckInterval", 0);
                settings.ShowLogs = GetBool("ShowLogs", false);
                settings.ShowRawCommandLine = GetBool("ShowRawCommandLine", false);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Ошибка загрузки настроек: {ex.Message}\nИспользуются значения по умолчанию.",
                    "Предупреждение",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
            }
            
            return settings;
        }

        // Save to app.config
        public void Save()
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                
                SetValue(config, "MaxMemoryForInstance", MaxMemoryForInstance.ToString());
                SetValue(config, "MaxAgeForInstance", MaxAgeForInstance.ToString());
                SetValue(config, "MaxMemoryForZennoposter", MaxMemoryForZennoposter.ToString());
                SetValue(config, "KillOld", KillOld.ToString());
                SetValue(config, "KillHeavy", KillHeavy.ToString());
                SetValue(config, "KillMain", KillMain.ToString());
                SetValue(config, "AutoCheckInterval", AutoCheckInterval.ToString());
                SetValue(config, "ShowLogs", ShowLogs.ToString());
                SetValue(config, "ShowRawCommandLine", ShowRawCommandLine.ToString());
                
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch (Exception ex)
            {
                throw new Exception($"Не удалось сохранить настройки: {ex.Message}");
            }
        }

        private static int GetInt(string key, int defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        private static bool GetBool(string key, bool defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            return bool.TryParse(value, out bool result) ? result : defaultValue;
        }

        private static void SetValue(Configuration config, string key, string value)
        {
            if (config.AppSettings.Settings[key] == null)
            {
                config.AppSettings.Settings.Add(key, value);
            }
            else
            {
                config.AppSettings.Settings[key].Value = value;
            }
        }

        public AppSettings Clone()
        {
            return new AppSettings
            {
                MaxMemoryForInstance = this.MaxMemoryForInstance,
                MaxAgeForInstance = this.MaxAgeForInstance,
                MaxMemoryForZennoposter = this.MaxMemoryForZennoposter,
                KillOld = this.KillOld,
                KillHeavy = this.KillHeavy,
                KillMain = this.KillMain,
                AutoCheckInterval = this.AutoCheckInterval,
                ShowLogs = this.ShowLogs,
                ShowRawCommandLine = this.ShowRawCommandLine
            };
        }
    }
}