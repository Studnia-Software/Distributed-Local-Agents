using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Psiruj.Api.Abstractions;
using Psiruj.Api.Models;

namespace Psiruj.Api.Services;

public sealed class OllamaTaskExecutor : ITaskExecutor
{
    private const int OllamaPort = 11434;
    private static readonly SemaphoreSlim ContainerStartupLock = new(1, 1);
    private readonly IConfiguration _configuration;
    private readonly ILogger<OllamaTaskExecutor> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public OllamaTaskExecutor(
        IConfiguration configuration,
        ILogger<OllamaTaskExecutor> logger,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<TaskResultDto> ExecuteAsync(TaskRequestDto request, CancellationToken cancellationToken)
    {
        var image = _configuration["Ollama:Image"] ?? "ollama/ollama:latest";
        var model = request.Model ?? _configuration["Ollama:Model"] ?? "llama3.2:1b";
        var modelsPath = _configuration["Ollama:ModelsPath"];
        
        var containerBuilder = new ContainerBuilder()
            .WithImage(image)
            .WithPortBinding(OllamaPort, true)
            .WithEnvironment("OLLAMA_MODELS", "/home/.ollama")
            
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
                request => request.ForPort(OllamaPort).ForPath("/api/tags")));

        if (!string.IsNullOrWhiteSpace(modelsPath))
        {
            if (!Directory.Exists(modelsPath))
            {
                Directory.CreateDirectory(modelsPath);
            }
            
            containerBuilder = containerBuilder.WithBindMount(modelsPath, "/home/.ollama");
        }

        var enableGpu = _configuration.GetValue("Ollama:EnableGpu", true);
        var gpuDeviceId = _configuration["Ollama:GpuDeviceId"] ?? "0";
        if (enableGpu)
        {
            containerBuilder = containerBuilder.WithCreateParameterModifier(parameters =>
            {
                parameters.HostConfig ??= new HostConfig();
                parameters.HostConfig ??= new HostConfig();
                parameters.HostConfig.DeviceRequests = new List<DeviceRequest>
                {
                    new()
                    {
                        Driver = "nvidia",
                        DeviceIDs = new List<string> { gpuDeviceId }, // Target specific GPU (e.g., "0")
                        Capabilities = new List<IList<string>>
                        {
                            new List<string> { "gpu" }
                        }
                    }
                };
            });
        }

        await ContainerStartupLock.WaitAsync(cancellationToken);
        try
        {
            await using var container = containerBuilder.Build();

            _logger.LogInformation("Starting Ollama container {Image} for task {TaskId} using model {Model}",
                image, request.TaskId, model);
            _logger.LogInformation("Ollama GPU acceleration requested: {EnableGpu}", enableGpu);
            await container.StartAsync(cancellationToken);

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(
                _configuration.GetValue("Ollama:RequestTimeoutSeconds", 900));
            client.BaseAddress = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(OllamaPort)}");

            _logger.LogInformation("Ollama container is ready for task {TaskId}; pulling model {Model}",
                request.TaskId, model);
            await PullModelAsync(client, model, cancellationToken);
            _logger.LogInformation("Model {Model} is ready for task {TaskId}; generating response", model, request.TaskId);
            var result = await GenerateAsync(client, model, request.Payload, request.TaskId, cancellationToken);

            _logger.LogInformation("Ollama completed task {TaskId}", request.TaskId);

            return new TaskResultDto
            {
                TaskId = request.TaskId,
                Stage = request.Stage,
                Payload = result
            };
        }
        finally
        {
            ContainerStartupLock.Release();
        }
    }

    private static async Task PullModelAsync(HttpClient client, string model, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/pull",
            new OllamaPullRequest(model, false),
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> GenerateAsync(
        HttpClient client,
        string model,
        string prompt,
        string taskId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
        {
            Content = JsonContent.Create(new OllamaGenerateRequest(model, prompt, true))
        };
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream);
        var output = new StringBuilder();
        var chunks = 0;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            var chunk = JsonSerializer.Deserialize<OllamaGenerateResponse>(line);
            if (!string.IsNullOrEmpty(chunk?.Response))
            {
                output.Append(chunk.Response);
                chunks++;
                if (chunks % 10 == 0)
                {
                    _logger.LogInformation("Ollama task {TaskId}: received {Chunks} response chunks",
                        taskId, chunks);
                }
            }
        }

        if (output.Length == 0)
        {
            throw new InvalidOperationException("Ollama returned an empty response.");
        }

        return output.ToString();
    }

    private sealed record OllamaPullRequest(string Model, bool Stream);

    private sealed record OllamaGenerateRequest(string Model, string Prompt, bool Stream);

    private sealed record OllamaGenerateResponse(
        [property: JsonPropertyName("response")] string? Response);
}