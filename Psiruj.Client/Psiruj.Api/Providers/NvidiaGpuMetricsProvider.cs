using System.Diagnostics;
using Psiruj.Api.Abstractions;

namespace Psiruj.Api.Providers;

public class NvidiaGpuMetricsProvider : IGpuMetricsProvider
{
    private ILogger<NvidiaGpuMetricsProvider> _logger;

    public NvidiaGpuMetricsProvider(ILogger<NvidiaGpuMetricsProvider> logger)
    {
        _logger = logger;
    }

    public double GetGpuPercentage()
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=utilization.gpu --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            var output = process!.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (double.TryParse(output.Trim(), out double used)) return Math.Max(0, 100.0 - used);
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);   
        }
        
        return 0;
    }
}