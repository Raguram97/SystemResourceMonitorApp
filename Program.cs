using System;
using System.IO;
using System.Threading.Tasks;
using SystemMonitorApp.Domain;
using SystemMonitorApp.Plugins;
using System.Text.Json;

namespace SystemMonitorApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                string jsonText = File.ReadAllText("appsettings.json");
                var configRoot = JsonSerializer.Deserialize<RootConfig>(jsonText);

                if (configRoot == null || configRoot.Monitoring == null)
                {
                    Console.WriteLine("[Startup Error] Configuration missing.");
                    return;
                }

                var settings = configRoot.Monitoring;

                var monitor = new SystemMonitorService();
                var fileLogger = new FileLoggerPlugin(settings.LogFilePath);
                var apiPlugin = new ApiPlugin(settings.ApiEndpoint);

                Console.WriteLine("Starting system monitor...");

                while (true)
                {
                    try
                    {
                        var data = await monitor.GetSystemUsageAsync();

                        Console.WriteLine(
                            $"CPU: {data.CpuUsage:F2}% | " +
                            $"RAM: {data.RamUsedMB:F2}/{data.TotalRamMB:F2} MB | " +
                            $"Disk: {data.DiskUsedMB:F2}/{data.TotalDiskMB:F2} MB");

                        await fileLogger.HandleAsync(data);
                        await apiPlugin.HandleAsync(data);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Monitoring Error] {ex.Message}");
                    }

                    await Task.Delay(settings.IntervalSeconds * 500);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Fatal Error] {ex.Message}");
                Console.ReadKey();
            }
        }
    }

    class RootConfig
    {
        public MonitoringSettings Monitoring { get; set; }
    }
}
