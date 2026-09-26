using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaIncidencias.Data;
using PlataformaIncidencias.Models;
using PlataformaIncidencias.Services;

namespace PlataformaIncidencias.Controllers;

[Authorize]
public class OperacionesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAlgoliaSearchService _algoliaService;
    private readonly IRedisCacheService _cacheService;
    private readonly ILogger<OperacionesController> _logger;

    public OperacionesController(
        ApplicationDbContext context,
        IAlgoliaSearchService algoliaService,
        IRedisCacheService cacheService,
        ILogger<OperacionesController> logger)
    {
        _context = context;
        _algoliaService = algoliaService;
        _cacheService = cacheService;
        _logger = logger;
    }

    // GET: /Operaciones/Incidencias?q=cadena
    public async Task<IActionResult> Incidencias(string? q)
    {
        ViewData["Busqueda"] = q;

        // Si hay búsqueda por texto, consultar directamente a Algolia sin usar caché Redis
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ids = await _algoliaService.SearchIncidenciaIdsAsync(q);

            var incidenciasEncontradas = await _context.Incidencias
                .Where(i => ids.Contains(i.Id) && i.Estado == "Abierta")
                .OrderByDescending(i => i.FechaRegistro)
                .ToListAsync();

            ViewData["OrigenDatos"] = "Búsqueda directa Algolia";
            _logger.LogInformation("Búsqueda en Algolia '{Query}' retornó {Count} incidencias abiertas (sin caché).", q, incidenciasEncontradas.Count);
            return View(incidenciasEncontradas);
        }

        // Si la búsqueda es vacía, usar caché de Redis por 60 segundos
        var incidenciasEnCache = await _cacheService.GetIncidenciasAbiertasAsync();
        if (incidenciasEnCache != null)
        {
            _logger.LogInformation("Lectura desde Redis: se obtuvieron {Count} incidencias abiertas del caché.", incidenciasEnCache.Count);
            ViewData["OrigenDatos"] = "Redis (Caché 60s)";
            return View(incidenciasEnCache);
        }

        // Si expiró o no está en caché, leer de SQLite
        _logger.LogInformation("Lectura desde la base: listado general de incidencias abiertas consultado de SQLite.");
        var incidenciasBase = await _context.Incidencias
            .Where(i => i.Estado == "Abierta")
            .OrderByDescending(i => i.FechaRegistro)
            .ToListAsync();

        await _cacheService.SetIncidenciasAbiertasAsync(incidenciasBase);
        ViewData["OrigenDatos"] = "Base de Datos (SQLite)";

        return View(incidenciasBase);
    }

    // POST: /Operaciones/Cerrar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(int id)
    {
        var incidencia = await _context.Incidencias.FindAsync(id);
        if (incidencia != null && incidencia.Estado == "Abierta")
        {
            incidencia.Estado = "Cerrada";
            await _context.SaveChangesAsync();
            _logger.LogInformation("Incidencia {Id} cerrada en base de datos.", id);

            // Invalidar caché en Redis al modificar datos
            await _cacheService.InvalidateIncidenciasAbiertasAsync();
            _logger.LogInformation("Clave de listado en Redis invalidada tras cerrar la incidencia {Id}.", id);
        }

        return RedirectToAction(nameof(Incidencias));
    }
}
