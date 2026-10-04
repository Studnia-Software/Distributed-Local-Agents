using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Psiruj.Api.Abstractions;

namespace Psiruj.Api.Workers;

public class MetricsPublisherService : BackgroundService
{
    private readonly ILogger<MetricsPublisherService> _logger;
    private IMetricsGathererService _gathererService;
    private readonly IConfiguration _configuration;
    
    public MetricsPublisherService(
        ILogger<MetricsPublisherService> logger,
        IMetricsGathererService gathererService,
        IConfiguration configuration)
    {
        _logger = logger;
        _gathererService = gathererService;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        _logger.LogInformation("Usługa publikowania metryk została uruchomiona.");

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var payload = JsonSerializer.Serialize(
                    _gathererService.GatherMetrics(),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                
                _logger.LogInformation(payload);

                var host = _configuration["AxeAi:UdpHost"] ?? "127.0.0.1";
                var port = _configuration.GetValue("AxeAi:UdpPort", 9090);
                var endpoint = new IPEndPoint(IPAddress.Parse(host), port);
                using var server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                
                var data = Encoding.ASCII.GetBytes(payload);
                server.SendTo(data, data.Length, SocketFlags.None, endpoint);
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