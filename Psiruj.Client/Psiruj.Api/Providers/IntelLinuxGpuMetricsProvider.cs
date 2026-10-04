using System.Diagnostics;
using System.Text.Json;
using Psiruj.Api.Abstractions;

namespace Psiruj.Api.Providers;

public class IntelLinuxGpuMetricsProvider : IGpuMetricsProvider
{
    private readonly ILogger<IntelLinuxGpuMetricsProvider> _logger;

    public IntelLinuxGpuMetricsProvider(ILogger<IntelLinuxGpuMetricsProvider> logger)
    {
        _logger = logger;
    }

    public double GetGpuPercentage()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "intel_gpu_top",
                    Arguments = "-J -s 1", 
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Output JSON wygląda tak: 
            // { "engines": { "Render/3D/0": { "busy": 15.5 } } }
            using var doc = JsonDocument.Parse(output);
            
            var engines = doc.RootElement.GetProperty("engines");
            if (engines.TryGetProperty("Render/3D/0", out var renderEngine))
            {
                if (renderEngine.TryGetProperty("busy", out var busyPercent))
                {
                    double used = busyPercent.GetDouble();
                    return Math.Max(0, 100.0 - used);
                }
            }
        }
        catch(Exception e)
        {
            _logger.LogError(e, e.Message);
        }

        return 0;
    }
}