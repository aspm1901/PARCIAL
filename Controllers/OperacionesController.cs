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
    private readonly IRedisCacheService _cacheService;
    private readonly ILogger<OperacionesController> _logger;

    public OperacionesController(
        ApplicationDbContext context,
        IRedisCacheService cacheService,
        ILogger<OperacionesController> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
    }

    // GET: /Operaciones/Incidencias
    public async Task<IActionResult> Incidencias()
    {
        // 1. Intentar leer desde Redis
        var incidenciasEnCache = await _cacheService.GetIncidenciasAbiertasAsync();
        if (incidenciasEnCache != null)
        {
            _logger.LogInformation("Lectura desde Redis: se obtuvieron {Count} incidencias abiertas del caché.", incidenciasEnCache.Count);
            ViewData["OrigenDatos"] = "Redis (Caché 60s)";
            return View(incidenciasEnCache);
        }

        // 2. Si no está en caché, leer desde SQLite
        _logger.LogInformation("Lectura desde la base: listado de incidencias abiertas consultado de SQLite.");
        var incidenciasBase = await _context.Incidencias
            .Where(i => i.Estado == "Abierta")
            .OrderByDescending(i => i.FechaRegistro)
            .ToListAsync();

        // 3. Guardar en Redis por 60 segundos
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
            _logger.LogInformation("Incidencia {Id} cerrada en la base de datos.", id);

            // Invalidar la clave de Redis antes de volver a consultar
            await _cacheService.InvalidateIncidenciasAbiertasAsync();
            _logger.LogInformation("Clave de listado en Redis invalidada tras cerrar la incidencia {Id}.", id);
        }

        return RedirectToAction(nameof(Incidencias));
    }
}
