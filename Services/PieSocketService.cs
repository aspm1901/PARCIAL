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
        _clusterId = configuration["PieSocket:ClusterId"] ?? "free.blr2";
        _apiKey = configuration["PieSocket:ApiKey"] ?? "lRB02NXqYlCqJjfhUb7cthtoi85WjG7KNAbuCtfu";
        _apiSecret = configuration["PieSocket:ApiSecret"] ?? "oNREJjksdhVaOkxZodJr7rUyrkHGhRxW";
        _channelId = configuration["PieSocket:ChannelId"] ?? "incidencias-channel";
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
