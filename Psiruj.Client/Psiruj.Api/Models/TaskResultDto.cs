using System.Text.Json.Serialization;

namespace Psiruj.Api.Models;

public sealed record TaskResultDto
{
    [JsonPropertyName("task_id")]
    public required string TaskId { get; init; }

    public required int Stage { get; init; }
    public required string Payload { get; init; }
}