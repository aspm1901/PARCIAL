using StackExchange.Redis;
using System.Text.Json;
using PlataformaIncidencias.Models;

namespace PlataformaIncidencias.Services;

public class RedisCacheService : IRedisCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private const string CacheKey = "incidencias:abiertas";
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromSeconds(60);

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<List<Incidencia>?> GetIncidenciasAbiertasAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            var json = await db.StringGetAsync(CacheKey);
            if (!json.HasValue)
            {
                return null;
            }

            return JsonSerializer.Deserialize<List<Incidencia>>(json.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al leer incidencias desde Redis.");
            return null;
        }
    }

    public async Task SetIncidenciasAbiertasAsync(List<Incidencia> incidencias)
    {
        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(incidencias);
            await db.StringSetAsync(CacheKey, json, CacheExpiry);
            _logger.LogInformation("Caché guardado en Redis con clave '{Key}' y TTL de 60 segundos.", CacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al escribir incidencias en Redis.");
        }
    }

    public async Task InvalidateIncidenciasAbiertasAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            var deleted = await db.KeyDeleteAsync(CacheKey);
            _logger.LogInformation("Caché de Redis con clave '{Key}' invalidado (eliminado: {Deleted}).", CacheKey, deleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al invalidar caché en Redis.");
        }
    }
}
