namespace Psiruj.Api.Models;

public record ExternalMetricsPayloadDto
{
    public required double FreeCpuPercentage { get; init; }
    public required double FreeRamMb { get; init; }
    public required double FreeGpuPercentage { get; init; } 
    public required double FreeDiskMb { get; init; } 
    public required DateTime TimestampUtc { get; init; }
}