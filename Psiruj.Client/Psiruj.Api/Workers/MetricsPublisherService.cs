using System.Text.Json;
using Psiruj.Api.Abstractions;

namespace Psiruj.Api.Workers;

public class MetricsPublisherService : BackgroundService
{
    private readonly ILogger<MetricsPublisherService> _logger;
    private IMetricsGathererService _gathererService;
    
    public MetricsPublisherService(ILogger<MetricsPublisherService> logger, IMetricsGathererService gathererService)
    {
        _logger = logger;
        _gathererService = gathererService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        _logger.LogInformation("Usługa publikowania metryk została uruchomiona.");

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var payload = _gathererService.GatherMetrics();
                
                _logger.LogInformation(JsonSerializer.Serialize(payload));
                
                // var response = await _httpClient.PostAsJsonAsync("https://twoje-zewnetrzne-api.com/api/metrics", payload, stoppingToken);
                
                // if (!response.IsSuccessStatusCode)
                // {
                //     _logger.LogWarning("Nie udało się wysłać metryk. Kod HTTP: {StatusCode}", response.StatusCode);
                // }
            }
            catch (HttpRequestException ex)
            {
                // Łapiemy błędy sieciowe, żeby brak połączenia nie wywalił całej usługi w tle
                _logger.LogError(ex, "Błąd sieci podczas wysyłania metryk do zewnętrznego API.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wystąpił nieoczekiwany błąd podczas zbierania lub wysyłania metryk.");
            }
        }
    }
}