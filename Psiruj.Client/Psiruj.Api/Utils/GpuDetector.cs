using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;

namespace Psiruj.Api.Utils;

public enum GpuVendor { Unknown, Nvidia, Amd, Intel }

public static class GpuDetector
{
    public static GpuVendor DetectGpu()
    {
        if (OperatingSystem.IsWindows())
        {
            return DetectWindowsGpu();
        }
        
        if (OperatingSystem.IsLinux())
        {
            return DetectLinuxGpu();
        }

        return GpuVendor.Unknown;
    }

    [SupportedOSPlatform("windows")]
    private static GpuVendor DetectWindowsGpu()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("select AdapterCompatibility from Win32_VideoController");
            bool hasIntel = false;

            foreach (var obj in searcher.Get())
            {
                var vendor = obj["AdapterCompatibility"]?.ToString()?.ToLower() ?? "";
                
                // Od razu zwracamy dedykowaną kartę, jeśli istnieje (NVIDIA/AMD)
                if (vendor.Contains("nvidia"))
                {
                    return GpuVendor.Nvidia;
                }
                
                if (vendor.Contains("amd") || vendor.Contains("advanced micro devices"))
                {
                    return GpuVendor.Amd;
                }
                
                if (vendor.Contains("intel"))
                {
                    hasIntel = true;
                }
            }

            if (hasIntel)
            {
                return GpuVendor.Intel;
            }
        }
        catch 
        { 
            // Ignorujemy błędy dostępu do WMI
        }
        
        return GpuVendor.Unknown;
    }

    [SupportedOSPlatform("linux")]
    private static GpuVendor DetectLinuxGpu()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "lspci",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            var output = process.StandardOutput.ReadToEnd().ToLower();
            process.WaitForExit();

           if (output.Contains("nvidia"))
            {
                return GpuVendor.Nvidia;
            }
            
            if (output.Contains("amd") || output.Contains("radeon"))
            {
                return GpuVendor.Amd;
            }
            
            if (output.Contains("intel"))
            {
                return GpuVendor.Intel;
            }
        }
        catch 
        { 
            // Ignorujemy błędy, np. gdy pakiet pciutils (lspci) nie jest zainstalowany
        }
        
        return GpuVendor.Unknown;
    }
}