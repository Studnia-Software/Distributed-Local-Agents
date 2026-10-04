using System.Diagnostics.Tracing;

namespace Psiruj.Api.Services;

public class SystemMetricsListener : EventListener
{
    public double CurrentCpuPercentage { get; private set; }
    public double CurrentMemoryWorkingSetMb { get; private set; }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "System.Runtime")
        {
            var args = new Dictionary<string, string>
            {
                { "EventCounterIntervalSec", "1" }
            };
            
            EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All, args);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventId == -1 && eventData.Payload?[0] is IDictionary<string, object> metricData)
        {
            if (metricData.TryGetValue("Name", out var nameValue) && nameValue is string metricName)
            {
                if (metricName == "cpu-usage")
                {
                    CurrentCpuPercentage = Math.Round(Convert.ToDouble(metricData["Mean"]), 2);
                }
                else if (metricName == "working-set")
                {
                    CurrentMemoryWorkingSetMb = Math.Round(Convert.ToDouble(metricData["Mean"]) / 1024 / 1024, 2);
                }
            }
        }
    }
}