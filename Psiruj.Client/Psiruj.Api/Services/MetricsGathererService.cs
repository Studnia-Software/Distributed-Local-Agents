using Psiruj.Api.Abstractions;
using Psiruj.Api.Models;

namespace Psiruj.Api.Services;

public class MetricsGathererService : IMetricsGathererService
{
    private readonly SystemMetricsListener _metricsListener;
    private readonly IGpuMetricsProvider _gpuProvider;
    private readonly ILogger<MetricsGathererService> _logger;

    public MetricsGathererService(IGpuMetricsProvider gpuProvider, ILogger<MetricsGathererService> logger, SystemMetricsListener metricsListener)
    {
        _gpuProvider = gpuProvider;
        _logger = logger;
        _metricsListener = metricsListener;
    }

    public ExternalMetricsPayloadDto GatherMetrics()
    {
        // 1. CPU
        // _metricsListener.CurrentCpuPercentage to zużycie dla naszego procesu.
        // Odejmujemy je od 100%, aby pokazać zewnętrznemu systemowi, ile "mocy" nam jeszcze zostało.
        var processCpuUsage = _metricsListener.CurrentCpuPercentage;
        var freeCpu = Math.Max(0, 100.0 - processCpuUsage);

        // 2. RAM
        // GC.GetGCMemoryInfo jest cross-platformowe i respektuje limity kontenerów (Docker/K8s).
        var gcInfo = GC.GetGCMemoryInfo();
        var totalMemoryLimitMb = gcInfo.TotalAvailableMemoryBytes / 1024.0 / 1024.0;
        var usedMemoryMb = gcInfo.MemoryLoadBytes / 1024.0 / 1024.0;
        var freeRamMb = Math.Max(0, totalMemoryLimitMb - usedMemoryMb);

        // 3. GPU
        // Dostawca GPU jest rozwiązywany przy starcie (Nvidia/AMD/Intel).
        var freeGpuPercentage = _gpuProvider.GetGpuPercentage();

        // 4. Dysk
        var freeDiskMb = GetFreeDiskSpaceMb();

        return new ExternalMetricsPayloadDto
        {
            Type = "heartbeat",
            NodeId = Environment.MachineName,
            FreeCpuPercentage = Math.Round(freeCpu, 2),
            FreeRamMb = Math.Round(freeRamMb, 2),
            FreeGpuPercentage = Math.Round(freeGpuPercentage, 2),
            FreeDiskMb = Math.Round(freeDiskMb, 2),
            TimestampUtc = DateTime.UtcNow
        };
    }
    
    private double GetFreeDiskSpaceMb()
    {
        try
        {
            // Sprawdzamy dysk/partycję, na której aktualnie działa aplikacja
            var currentDirectory = Directory.GetCurrentDirectory();
            var drive = new DriveInfo(currentDirectory);
            
            // AvailableFreeSpace zwraca dostępne bajty dla obecnego użytkownika
            return drive.AvailableFreeSpace / 1024.0 / 1024.0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się odczytać wolnego miejsca na dysku.");
            return 0;
        }
    }
}