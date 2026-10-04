using Microsoft.AspNetCore.Mvc;
using Psiruj.Api.Abstractions;
using Psiruj.Api.Models;

namespace Psiruj.Api.Controllers;

[ApiController]
[Route("v1/tasks")]
public sealed class TasksController : ControllerBase
{
    private readonly ITaskExecutor _taskExecutor;

    public TasksController(ITaskExecutor taskExecutor)
    {
        _taskExecutor = taskExecutor;
    }

    [HttpPost]
    [ProducesResponseType(typeof(TaskResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<TaskResultDto>> Execute(
        [FromBody] TaskRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TaskId) ||
            string.IsNullOrWhiteSpace(request.Payload) ||
            request.Stage < 0 ||
            request.StartLayer < 0 ||
            request.EndLayer <= request.StartLayer)
        {
            return BadRequest("task_id, payload and a valid layer range are required.");
        }

        try
        {
            return Ok(await _taskExecutor.ExecuteAsync(request, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Problem(
                detail: "The Ollama task could not be completed.",
                statusCode: StatusCodes.Status502BadGateway,
                title: "Task execution failed",
                extensions: new Dictionary<string, object?>
                {
                    ["exception"] = exception.Message
                });
        }
    }
}