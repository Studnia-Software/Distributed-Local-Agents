using System.Text.Json.Serialization;

namespace Psiruj.Api.Models;

public record ExternalMetricsPayloadDto
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    [JsonPropertyName("node_id")]
    public required string NodeId { get; init; }
    public required double FreeCpuPercentage { get; init; }
    public required double FreeRamMb { get; init; }
    public required double FreeGpuPercentage { get; init; } 
    public required double FreeDiskMb { get; init; } 
    public required DateTime TimestampUtc { get; init; }
}