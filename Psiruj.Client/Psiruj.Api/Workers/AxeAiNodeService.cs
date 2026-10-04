using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Psiruj.Api.Abstractions;
using Psiruj.Api.Models;

namespace Psiruj.Api.Workers;

public sealed class AxeAiNodeService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<AxeAiNodeService> _logger;
    private readonly IMetricsGathererService _metricsGatherer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly object _assignmentLock = new();
    private int _rank = -1;
    private int _startLayer;
    private int _endLayer;

    public AxeAiNodeService(
        ILogger<AxeAiNodeService> logger,
        IMetricsGathererService metricsGatherer,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _metricsGatherer = metricsGatherer;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var socket = new UdpClient(0);
        var coordinator = new IPEndPoint(
            IPAddress.Parse(_configuration["AxeAi:UdpHost"] ?? "127.0.0.1"),
            _configuration.GetValue("AxeAi:UdpPort", 9090));

        _logger.LogInformation("Axe AI node listening on UDP port {Port}",
            ((IPEndPoint)socket.Client.LocalEndPoint!).Port);

        var heartbeatTask = SendHeartbeatsAsync(socket, coordinator, stoppingToken);
        var receiveTask = ReceiveMessagesAsync(socket, stoppingToken);
        await Task.WhenAny(heartbeatTask, receiveTask);
    }

    private async Task SendHeartbeatsAsync(
        UdpClient socket,
        IPEndPoint coordinator,
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var payload = JsonSerializer.SerializeToUtf8Bytes(
                    _metricsGatherer.GatherMetrics(), JsonOptions);
                await socket.SendAsync(payload, coordinator, stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not send Axe AI heartbeat.");
            }
        }
    }

    private async Task ReceiveMessagesAsync(UdpClient socket, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var packet = await socket.ReceiveAsync(stoppingToken);
                using var document = JsonDocument.Parse(packet.Buffer);
                var root = document.RootElement;
                var type = root.TryGetProperty("type", out var typeProperty)
                    ? typeProperty.GetString()
                    : null;

                if (type == "assign")
                {
                    lock (_assignmentLock)
                    {
                        _rank = root.GetProperty("rank").GetInt32();
                        _startLayer = root.GetProperty("start_layer").GetInt32();
                        _endLayer = root.GetProperty("end_layer").GetInt32();
                    }
                    _logger.LogInformation("Assigned layers [{Start}, {End}) at rank {Rank}",
                        _startLayer, _endLayer, _rank);
                }
                else if (type == "task")
                {
                    _ = ProcessTaskAsync(socket, packet.RemoteEndPoint, packet.Buffer, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not receive Axe AI UDP message.");
            }
        }
    }

    private async Task ProcessTaskAsync(
        UdpClient socket,
        IPEndPoint remoteEndPoint,
        byte[] payload,
        CancellationToken stoppingToken)
    {
        TaskRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<TaskRequestDto>(payload, JsonOptions);
            if (request is null)
            {
                return;
            }
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Ignoring malformed Axe AI task.");
            return;
        }

        int rank;
        int startLayer;
        int endLayer;
        lock (_assignmentLock)
        {
            rank = _rank;
            startLayer = _startLayer;
            endLayer = _endLayer;
        }

        if (rank < 0 || request.Stage != rank ||
            request.StartLayer != startLayer || request.EndLayer != endLayer)
        {
            _logger.LogWarning("Ignoring task {TaskId} for unassigned layer range.", request.TaskId);
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var executor = scope.ServiceProvider.GetRequiredService<ITaskExecutor>();
            var result = await executor.ExecuteAsync(request, stoppingToken);
            var response = JsonSerializer.SerializeToUtf8Bytes(new
            {
                type = "result",
                task_id = result.TaskId,
                stage = result.Stage,
                payload = result.Payload
            }, JsonOptions);
            await socket.SendAsync(response, remoteEndPoint, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Task {TaskId} failed.", request.TaskId);
        }
    }
}