using Psiruj.Api.Abstractions;

namespace Psiruj.Api.Providers;

public class NullGpuMetricsProvider : IGpuMetricsProvider
{
    // Intel GPUs are difficult to read uniformly across Windows and Linux without 
    // heavy C++ wrappers or performance counters. For an API, usually returning 0 or -1 is safest.
    public double GetGpuPercentage()
    {
        return -1;
    }
}