using Psiruj.Api.Models;

namespace Psiruj.Api.Abstractions;

public interface ITaskExecutor
{
    Task<TaskResultDto> ExecuteAsync(TaskRequestDto request, CancellationToken cancellationToken);
}