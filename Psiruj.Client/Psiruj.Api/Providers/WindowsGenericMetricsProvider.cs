using System.Diagnostics;
using Psiruj.Api.Abstractions;

namespace Psiruj.Api.Providers;

public class WindowsGenericMetricsProvider : IGpuMetricsProvider
{
    private readonly  ILogger<WindowsGenericMetricsProvider> _logger;

    public WindowsGenericMetricsProvider(ILogger<WindowsGenericMetricsProvider> logger)
    {
        _logger = logger;
    }

    public double GetGpuPercentage()
    {
        try
        {
            var category = new PerformanceCounterCategory("GPU Engine");
            var instances = category.GetInstanceNames();
            
            float totalUsedPercentage = 0;

            var engine3DInstances = instances.Where(i => i.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var instance in engine3DInstances)
            {
                using var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance);
                
                counter.NextValue(); 
                System.Threading.Thread.Sleep(10); // Krótkie opóźnienie, aby zebrać próbkę
                
                totalUsedPercentage += counter.NextValue();
            }

            // Ograniczamy do 100% (Windows czasem raportuje anomalie przy wielu procesach)
            var clampedUsed = Math.Min(100.0, totalUsedPercentage);
            
            return Math.Max(0, 100.0 - clampedUsed);
        }
        catch(Exception e)
        {
            _logger.LogError(e, e.Message);
            return 0;
        }
    }
}