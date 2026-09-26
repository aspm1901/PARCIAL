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

    // GET / POST: /Operaciones/ReabrirTodas (utilidad para restaurar datos de prueba con 20 incidencias)
    [HttpGet, HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ReabrirTodas()
    {
        var count = await _context.Incidencias.CountAsync();
        if (count < 20)
        {
            _context.Incidencias.RemoveRange(_context.Incidencias);
            await _context.SaveChangesAsync();

            var list = new List<Incidencia>
            {
                new Incidencia { Estacion = "Estación Central", Descripcion = "Cadena suelta en bicicleta #101", Prioridad = "Alta", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-50) },
                new Incidencia { Estacion = "Estación San Isidro", Descripcion = "Freno delantero desgastado en bicicleta #204", Prioridad = "Media", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-45) },
                new Incidencia { Estacion = "Estación Miraflores", Descripcion = "Pinchazo de neumático en bicicleta #305", Prioridad = "Alta", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-40) },
                new Incidencia { Estacion = "Estación Larcomar", Descripcion = "Sillín dañado en bicicleta #412", Prioridad = "Baja", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-35) },
                new Incidencia { Estacion = "Estación Javier Prado", Descripcion = "Anclaje #5 trabado con bicicleta #519", Prioridad = "Alta", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-30) },
                new Incidencia { Estacion = "Estación Kennedy", Descripcion = "Pedal flojo en bicicleta #602", Prioridad = "Media", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-25) },
                new Incidencia { Estacion = "Estación Barranco", Descripcion = "Luz LED delantera no enciende en bicicleta #108", Prioridad = "Baja", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-20) },
                new Incidencia { Estacion = "Estación Surco", Descripcion = "Manubrio desalineado en bicicleta #315", Prioridad = "Media", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-18) },
                new Incidencia { Estacion = "Estación San Borja", Descripcion = "Cambio de marcha atascado en 3ra posición bicicleta #522", Prioridad = "Alta", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-15) },
                new Incidencia { Estacion = "Estación Centro Cívico", Descripcion = "Freno trasero sin tensión en bicicleta #209", Prioridad = "Alta", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-12) },
                new Incidencia { Estacion = "Estación Los Olivos", Descripcion = "Panel solar de la estación con suciedad y baja carga", Prioridad = "Baja", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-10) },
                new Incidencia { Estacion = "Estación Magdalena", Descripcion = "Sensor de anclaje #12 no detecta devolución bicicleta #407", Prioridad = "Media", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-9) },
                new Incidencia { Estacion = "Estación Pueblo Libre", Descripcion = "Neumático trasero bajo de aire en bicicleta #710", Prioridad = "Baja", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-8) },
                new Incidencia { Estacion = "Estación Jesús María", Descripcion = "Cadena oxidada con fricción en bicicleta #815", Prioridad = "Media", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-7) },
                new Incidencia { Estacion = "Estación Lince", Descripcion = "Cesta delantera doblada por impacto en bicicleta #120", Prioridad = "Baja", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-6) },
                new Incidencia { Estacion = "Estación Salaverry", Descripcion = "Pantalla digital de bloqueo no responde en estación", Prioridad = "Alta", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-5) },
                new Incidencia { Estacion = "Estación Angamos", Descripcion = "Rayos de rueda delantera torcidos en bicicleta #633", Prioridad = "Media", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-4) },
                new Incidencia { Estacion = "Estación Benavides", Descripcion = "Candado de seguridad trabado en rueda trasera bicicleta #904", Prioridad = "Alta", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-3) },
                new Incidencia { Estacion = "Estación Chorrillos", Descripcion = "Cables de freno desgastados y deshilachados en bicicleta #218", Prioridad = "Alta", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-2) },
                new Incidencia { Estacion = "Estación Pardo", Descripcion = "Bicicleta #540 con holgura excesiva en el eje central", Prioridad = "Media", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-1) }
            };
            await _context.Incidencias.AddRangeAsync(list);
            await _context.SaveChangesAsync();
        }
        else
        {
            var incidencias = await _context.Incidencias.ToListAsync();
            foreach (var inc in incidencias)
            {
                inc.Estado = "Abierta";
            }
            await _context.SaveChangesAsync();
        }

        await _cacheService.InvalidateIncidenciasAbiertasAsync();
        _logger.LogInformation("20 incidencias restauradas y abiertas para pruebas.");
        return RedirectToAction(nameof(Incidencias));
    }
}
