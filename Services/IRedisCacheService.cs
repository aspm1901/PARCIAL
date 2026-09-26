using PlataformaIncidencias.Models;

namespace PlataformaIncidencias.Services;

public interface IRedisCacheService
{
    Task<List<Incidencia>?> GetIncidenciasAbiertasAsync();
    Task SetIncidenciasAbiertasAsync(List<Incidencia> incidencias);
    Task InvalidateIncidenciasAbiertasAsync();
}
