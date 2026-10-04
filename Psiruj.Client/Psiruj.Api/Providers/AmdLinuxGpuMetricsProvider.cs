using System.Diagnostics;
using System.Text.RegularExpressions;
using Psiruj.Api.Abstractions;

namespace Psiruj.Api.Providers;

public class AmdLinuxGpuMetricsProvider : IGpuMetricsProvider
{
    private ILogger<AmdLinuxGpuMetricsProvider> _logger;

    public AmdLinuxGpuMetricsProvider(ILogger<AmdLinuxGpuMetricsProvider> logger)
    {
        _logger = logger;
    }

    public double GetGpuPercentage()
    {
        try
        {
            for (int i = 0; i < 16; i++)
            {
                var path = $"/sys/class/drm/card{i}/device/gpu_busy_percent";
                
                if (File.Exists(path))
                {
                    var usageText = File.ReadAllText(path).Trim();
                    
                    if (double.TryParse(usageText, out double usedPercentage))
                    {
                        return Math.Max(0, 100.0 - usedPercentage);
                    }
                }
            }
        }
        catch (Exception e)
        { 
            // Permission denied or other IO exception; suppress and fall back to CLI
            _logger.LogError(e, e.Message);
        }

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "rocm-smi",
                    Arguments = "--showuse", // Outputs GPU usage percentage
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var match = Regex.Match(output, @"GPU use\s*\(\%\)\s*:\s*(\d+(\.\d+)?)", RegexOptions.IgnoreCase);
            
            if (match.Success && double.TryParse(match.Groups[1].Value, out double usedGpu))
            {
                return Math.Max(0, 100.0 - usedGpu);
            }
        }
        catch(Exception e)
        {
            _logger.LogError(e, e.Message);
        }

        return 0;
    }
}