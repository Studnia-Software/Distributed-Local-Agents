using Psiruj.Api.Models;

namespace Psiruj.Api.Abstractions;

public interface IMetricsGathererService
{
    ExternalMetricsPayloadDto GatherMetrics();
}