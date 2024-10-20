using System;
using System.IO;
using Playnite.SDK.Models;
using SuccessStory.Models;

namespace SuccessStory.Clients
{
    public class AchievementWatcherProvider : GenericAchievements
    {
        private AchievementWatcherIntegration _awIntegration = new AchievementWatcherIntegration();

        public AchievementWatcherProvider() : base("AchievementWatcher")
        {
        }

        public override GameAchievements GetAchievements(Game game)
        {
            string apiKey = _awIntegration.GetSteamApiKey();

            // TODO: Implement Achievement Watcher integration
            throw new NotImplementedException();
        }

        public override bool ValidateConfiguration()
        {
            // TODO: Implement configuration validation
            return true;
        }

        public override bool EnabledInSettings()
        {
            return PluginDatabase.PluginSettings.Settings.EnableAchievementWatcher;
        }
    }

    public class AchievementWatcherIntegration
    {
        private string _steamApiKey;

        public string GetSteamApiKey()
        {
            if (string.IsNullOrEmpty(_steamApiKey))
            {
                _steamApiKey = FindSteamApiKey();
            }
            return _steamApiKey;
        }

        private string FindSteamApiKey()
        {
            // First, check SuccessStory's settings
            string apiKey = SuccessStory.SteamApi.CurrentAccountInfos?.ApiKey;

            if (!string.IsNullOrEmpty(apiKey))
            {
                return apiKey;
            }

            // If not found, look for Achievement Watcher's configuration
            string awConfigPath = GetAchievementWatcherConfigPath();
            if (File.Exists(awConfigPath))
            {
                string awConfig = File.ReadAllText(awConfigPath);
                apiKey = ParseApiKeyFromConfig(awConfig);
            }

            return apiKey;
        }

        private string GetAchievementWatcherConfigPath()
        {
            // TODO: Implement logic to find Achievement Watcher's config file
            throw new NotImplementedException();
        }

        private string ParseApiKeyFromConfig(string config)
        {
            // TODO: Implement logic to extract the API key from Achievement Watcher's config
            throw new NotImplementedException();
        }
    }
}