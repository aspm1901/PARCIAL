using System.Text;
using System.Text.Json;

namespace PlataformaIncidencias.Services;

public class PieSocketService : IPieSocketService
{
    private readonly HttpClient _httpClient;
    private readonly string _clusterId;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly string _channelId;
    private readonly ILogger<PieSocketService> _logger;

    public PieSocketService(HttpClient httpClient, IConfiguration configuration, ILogger<PieSocketService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var cluster = configuration["PieSocket:ClusterId"];
        if (string.IsNullOrWhiteSpace(cluster)) cluster = Environment.GetEnvironmentVariable("PieSocket__ClusterId");
        _clusterId = !string.IsNullOrWhiteSpace(cluster) ? cluster : "free.blr2";

        var key = configuration["PieSocket:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("PieSocket__ApiKey");
        _apiKey = !string.IsNullOrWhiteSpace(key) ? key : "lRB02NXqYlCqJjfhUb7cthtoi85WjG7KNAbuCtfu";

        var secret = configuration["PieSocket:ApiSecret"];
        if (string.IsNullOrWhiteSpace(secret)) secret = Environment.GetEnvironmentVariable("PieSocket__ApiSecret");
        _apiSecret = !string.IsNullOrWhiteSpace(secret) ? secret : "oNREJjksdhVaOkxZodJr7rUyrkHGhRxW";

        var channel = configuration["PieSocket:ChannelId"];
        if (string.IsNullOrWhiteSpace(channel)) channel = Environment.GetEnvironmentVariable("PieSocket__ChannelId");
        _channelId = !string.IsNullOrWhiteSpace(channel) ? channel : "incidencias-channel";
    }

    public async Task PublicarEventoAsync(string evento, int id, string estado)
    {
        try
        {
            var payload = new
            {
                key = _apiKey,
                secret = _apiSecret,
                channelId = _channelId,
                message = JsonSerializer.Serialize(new
                {
                    evento = evento,
                    id = id,
                    estado = estado,
                    timestamp = DateTime.UtcNow
                })
            };

            var url = $"https://{_clusterId}.piesocket.com/api/publish";
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Evento '{Evento}' publicado con éxito a PieHost para incidencia {Id} ({Estado}).", evento, id, estado);
            }
            else
            {
                _logger.LogWarning("PieHost retornó código de estado {StatusCode} al publicar evento para incidencia {Id}.", response.StatusCode, id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al publicar evento en PieHost.");
        }
    }
}
