using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SystemMonitorApp.Domain;

namespace SystemMonitorApp
{
    public class SystemMonitorService : ISystemMonitorService
    {
        private bool isWindows;
        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;

        public SystemMonitorService()
        {
            isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            if (isWindows)
            {
                try
                {
                    cpuCounter = new PerformanceCounter("Processor", "% Processor Performance", "_Total");
                    ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                    cpuCounter.NextValue();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Init Error] " + ex.Message);
                }
            }
        }

        public async Task<SystemUsageData> GetSystemUsageAsync()
        {
            double cpu = 0, ramUsed = 0, totalRam = 0, diskUsed = 0, totalDisk = 0;

            if (isWindows)
            {
                cpu = await GetCpuUsageWindowsAsync();
                totalRam = GetWindowsTotalMemory();
                double availableRam = ramCounter.NextValue();
                ramUsed = totalRam - availableRam;

                (diskUsed, totalDisk) = GetDiskUsageWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                cpu = await GetCpuUsageLinuxAsync();
                totalRam = GetLinuxTotalMemory();
                ramUsed = GetLinuxUsedMemory();
                (diskUsed, totalDisk) = GetDiskUsageLinux();
            }

            return new SystemUsageData
            {
                CpuUsage = cpu,
                RamUsedMB = ramUsed,
                TotalRamMB = totalRam,
                DiskUsedMB = diskUsed,
                TotalDiskMB = totalDisk
            };
        }

        // Windows Monitoring
        private async Task<float> GetCpuUsageWindowsAsync()
        {
            try
            {
                cpuCounter.NextValue();
                float totalUsage = 0;
                const int samples = 3;

                var coreCount = Environment.ProcessorCount;
                var perCoreCounters = new PerformanceCounter[coreCount];

                for (int i = 0; i < coreCount; i++)
                {
                    perCoreCounters[i] = new PerformanceCounter("Processor Information", "% Processor Utility", i.ToString());
                }


                float averageUsage = totalUsage / samples;
                return Math.Min(averageUsage, 100); 

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching CPU usage: {ex.Message}");
                return -1; // Return -1 to indicate error
            }
        }


        private double GetWindowsTotalMemory()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (var obj in searcher.Get())
                {
                    double totalKb = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                    return totalKb / 1024.0;
                }
            }
            catch { }
            return 0;
        }

        private (double usedMB, double totalMB) GetDiskUsageWindows()
        {
            try
            {
                var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);
                if (drive != null)
                {
                    double used = drive.TotalSize - drive.TotalFreeSpace;
                    return (used / (1024 * 1024), drive.TotalSize / (1024 * 1024));
                }
            }
            catch { }
            return (0, 0);
        }

        // Linux Monitoring
        private async Task<float> GetCpuUsageLinuxAsync()
        {
            var (idle1, total1) = ReadLinuxCpuTimes();
            await Task.Delay(1500);
            var (idle2, total2) = ReadLinuxCpuTimes();

            ulong idleDelta = idle2 - idle1;
            ulong totalDelta = total2 - total1;

            if (totalDelta == 0) return 0;
            return 100f * (1f - ((float)idleDelta / totalDelta));
        }

        private (ulong idle, ulong total) ReadLinuxCpuTimes()
        {
            try
            {
                var line = File.ReadLines("/proc/stat").FirstOrDefault(l => l.StartsWith("cpu "));
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(ulong.Parse).ToArray();
                ulong idle = parts[3] + parts[4];
                ulong total = parts.Aggregate((a, b) => a + b);
                return (idle, total);
            }
            catch { return (0, 0); }
        }

        private double GetLinuxTotalMemory()
        {
            var lines = File.ReadAllLines("/proc/meminfo");
            var totalLine = lines.FirstOrDefault(l => l.StartsWith("MemTotal:"));
            double totalKb = double.Parse(totalLine.Split(':')[1].Trim().Split(' ')[0]);
            return totalKb / 1024.0;
        }

        private double GetLinuxUsedMemory()
        {
            var lines = File.ReadAllLines("/proc/meminfo");
            double total = 0, available = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("MemTotal:"))
                    total = double.Parse(line.Split(':')[1].Trim().Split(' ')[0]);
                else if (line.StartsWith("MemAvailable:"))
                    availab le = double.Parse(line.Split(':')[1].Trim().Split(' ')[0]);
            }
            return (total - available) / 1024.0;
        }

        private (double usedMB, double totalMB) GetDiskUsageLinux()
        {
            try
            {
                var info = new DriveInfo("/");
                double used = info.TotalSize - info.TotalFreeSpace;
                return (used / (1024 * 1024), info.TotalSize / (1024 * 1024));
            }
            catch { return (0, 0); }
        }
    }

   
}
