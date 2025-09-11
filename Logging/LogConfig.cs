using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgrolandApiSync.Logging
{
    public static class LogConfig
    {
        public static void Configure(int expirationDays)
        {
            try
            {
                var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(
                        path: Path.Combine(logDirectory, "log-.txt"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: expirationDays,
                        shared: true,
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                    )
                    .CreateLogger();
            }
            catch (Exception ex)
            {
                // Fallback to console logging in case file sink fails
                Console.WriteLine("Serilog initialization failed: " + ex);
            }
        }
    }
}