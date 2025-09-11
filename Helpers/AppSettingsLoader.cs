using AgrolandApiSync.DTOs;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace AgrolandApiSync.Helpers
{
    public static class AppSettingsLoader
    {
        public static AgrolandApiSettings LoadApiSettings()
        {
            return new AgrolandApiSettings
            {
                BaseUrl = GetString("AgrolandApiBaseUrl"),
                ApiKey = GetString("AgrolandApiKey"),
            };
        }

        public static int GetLogsExpirationDays() => GetInt("LogsExpirationDays", 14);

        public static int GetMargin() => GetInt("Margin%", 25);

        public static TimeSpan GetFetchInterval() => TimeSpan.FromMinutes(GetInt("FetchIntervalMinutes", 60));

        public static string GetConnenctionString() => ConfigurationManager.ConnectionStrings["DefaultConnectionString"].ToString();

        private static string GetString(string key, bool required = true)
        {
            var value = ConfigurationManager.AppSettings[key];

            if (required && string.IsNullOrWhiteSpace(value))
                throw new ConfigurationErrorsException($"Missing required appSetting: '{key}'");

            return value;
        }

        private static int GetInt(string key, int defaultValue)
        {
            var raw = ConfigurationManager.AppSettings[key];
            if (int.TryParse(raw, out int result))
                return result;

            return defaultValue;
        }
    }
}