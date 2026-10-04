using System.Text.Json.Serialization;

namespace Psiruj.Api.Models;

public sealed record TaskRequestDto
{
    [JsonPropertyName("task_id")]
    public required string TaskId { get; init; }

    public required int Stage { get; init; }

    [JsonPropertyName("start_layer")]
    public required int StartLayer { get; init; }

    [JsonPropertyName("end_layer")]
    public required int EndLayer { get; init; }

    public required string Payload { get; init; }

    public string? Model { get; init; }
}