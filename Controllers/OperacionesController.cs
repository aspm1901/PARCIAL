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
    private readonly IPieSocketService _pieSocketService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OperacionesController> _logger;

    public OperacionesController(
        ApplicationDbContext context,
        IAlgoliaSearchService algoliaService,
        IRedisCacheService cacheService,
        IPieSocketService pieSocketService,
        IConfiguration configuration,
        ILogger<OperacionesController> logger)
    {
        _context = context;
        _algoliaService = algoliaService;
        _cacheService = cacheService;
        _pieSocketService = pieSocketService;
        _configuration = configuration;
        _logger = logger;
    }

    // GET: /Operaciones/Incidencias?q=estacion
    public async Task<IActionResult> Incidencias(string? q)
    {
        ViewData["Busqueda"] = q;
        var cluster = _configuration["PieSocket:ClusterId"];
        if (string.IsNullOrWhiteSpace(cluster)) cluster = Environment.GetEnvironmentVariable("PieSocket__ClusterId");
        ViewData["PieSocketCluster"] = !string.IsNullOrWhiteSpace(cluster) ? cluster : "free.blr2";

        var apiKey = _configuration["PieSocket:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) apiKey = Environment.GetEnvironmentVariable("PieSocket__ApiKey");
        ViewData["PieSocketApiKey"] = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : "lRB02NXqYlCqJjfhUb7cthtoi85WjG7KNAbuCtfu";

        var channel = _configuration["PieSocket:ChannelId"];
        if (string.IsNullOrWhiteSpace(channel)) channel = Environment.GetEnvironmentVariable("PieSocket__ChannelId");
        ViewData["PieSocketChannel"] = !string.IsNullOrWhiteSpace(channel) ? channel : "incidencias-channel";

        // 1. Si hay texto de búsqueda, consultar Algolia directamente sin usar caché de Redis
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ids = await _algoliaService.SearchIncidenciaIdsAsync(q);

            // Filtrar solo incidencias abiertas existentes en la base (las cerradas nunca se muestran)
            var incidenciasEncontradas = await _context.Incidencias
                .Where(i => ids.Contains(i.Id) && i.Estado == "Abierta")
                .OrderByDescending(i => i.FechaRegistro)
                .ToListAsync();

            ViewData["OrigenDatos"] = "Búsqueda directa Algolia";
            _logger.LogInformation("Búsqueda en Algolia '{Query}' retornó {Count} incidencias abiertas en BD (sin caché).", q, incidenciasEncontradas.Count);
            return View(incidenciasEncontradas);
        }

        // 2. Si la búsqueda es vacía, consultar listado general con caché distribuido Redis (60s)
        var incidenciasEnCache = await _cacheService.GetIncidenciasAbiertasAsync();
        if (incidenciasEnCache != null)
        {
            _logger.LogInformation("Lectura desde Redis: se obtuvieron {Count} incidencias abiertas del caché.", incidenciasEnCache.Count);
            ViewData["OrigenDatos"] = "Redis (Caché 60s)";
            return View(incidenciasEnCache);
        }

        // 3. Si expiró o no está en caché, leer de la base de datos (SQLite)
        _logger.LogInformation("Lectura desde la base: listado general de incidencias abiertas consultado de SQLite.");
        var incidenciasBase = await _context.Incidencias
            .Where(i => i.Estado == "Abierta")
            .OrderByDescending(i => i.FechaRegistro)
            .ToListAsync();

        await _cacheService.SetIncidenciasAbiertasAsync(incidenciasBase);
        ViewData["OrigenDatos"] = "Base de Datos (SQLite)";

        return View(incidenciasBase);
    }

    // GET: /Operaciones/EstadoVigente (para consultar estado vigente al reconectar WebSocket)
    [HttpGet]
    public async Task<IActionResult> EstadoVigente()
    {
        var incidencias = await _context.Incidencias
            .Where(i => i.Estado == "Abierta")
            .OrderByDescending(i => i.FechaRegistro)
            .Select(i => new
            {
                i.Id,
                i.Estacion,
                i.Descripcion,
                i.Prioridad,
                i.Estado,
                FechaRegistro = i.FechaRegistro.ToString("yyyy-MM-dd HH:mm")
            })
            .ToListAsync();

        return Json(incidencias);
    }

    // POST / GET: /Operaciones/Cerrar/5
    // Secuencia requerida: 1. Cierre en base -> 2. Invalidación de Redis -> 3. Publicación por PieHost
    [HttpPost, HttpGet]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Cerrar(int id)
    {
        var incidencia = await _context.Incidencias.FindAsync(id);
        if (incidencia != null && incidencia.Estado == "Abierta")
        {
            // Secuencia 1: Cierre en base
            incidencia.Estado = "Cerrada";
            await _context.SaveChangesAsync();
            _logger.LogInformation("Secuencia 1: Incidencia {Id} cerrada y guardada en base de datos SQLite.", id);

            // Secuencia 2: Invalidación de Redis antes de volver a consultarlo
            await _cacheService.InvalidateIncidenciasAbiertasAsync();
            _logger.LogInformation("Secuencia 2: Clave de listado en Redis invalidada tras cierre de incidencia {Id}.", id);

            // Secuencia 3: Publicación por PieHost
            await _pieSocketService.PublicarEventoAsync("IncidenciaActualizada", incidencia.Id, incidencia.Estado);
            _logger.LogInformation("Secuencia 3: Evento IncidenciaActualizada publicado a PieHost para incidencia {Id} con estado Cerrada.", id);
        }

        return RedirectToAction(nameof(Incidencias));
    }

    // GET / POST: /Operaciones/ReabrirTodas (utilidad para restaurar datos de prueba si se cerraron todas)
    [HttpGet, HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ReabrirTodas()
    {
        var incidencias = await _context.Incidencias.ToListAsync();
        foreach (var inc in incidencias)
        {
            inc.Estado = "Abierta";
        }
        await _context.SaveChangesAsync();
        await _cacheService.InvalidateIncidenciasAbiertasAsync();
        _logger.LogInformation("Todas las incidencias fueron reabiertas para pruebas.");
        return RedirectToAction(nameof(Incidencias));
    }
}
