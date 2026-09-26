using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaIncidencias.Data;
using PlataformaIncidencias.Services;

namespace PlataformaIncidencias.Controllers;

[Authorize]
public class OperacionesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAlgoliaSearchService _algoliaService;
    private readonly ILogger<OperacionesController> _logger;

    public OperacionesController(
        ApplicationDbContext context,
        IAlgoliaSearchService algoliaService,
        ILogger<OperacionesController> logger)
    {
        _context = context;
        _algoliaService = algoliaService;
        _logger = logger;
    }

    // GET: /Operaciones/Incidencias?q=estacion
    public async Task<IActionResult> Incidencias(string? q)
    {
        ViewData["Busqueda"] = q;

        if (!string.IsNullOrWhiteSpace(q))
        {
            var ids = await _algoliaService.SearchIncidenciaIdsAsync(q);

            var incidenciasEncontradas = await _context.Incidencias
                .Where(i => ids.Contains(i.Id) && i.Estado == "Abierta")
                .OrderByDescending(i => i.FechaRegistro)
                .ToListAsync();

            _logger.LogInformation("Búsqueda en Algolia '{Query}' retornó {Count} incidencias abiertas en BD.", q, incidenciasEncontradas.Count);
            return View(incidenciasEncontradas);
        }

        var incidenciasHabituales = await _context.Incidencias
            .Where(i => i.Estado == "Abierta")
            .OrderByDescending(i => i.FechaRegistro)
            .ToListAsync();

        return View(incidenciasHabituales);
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
            _logger.LogInformation("Incidencia {Id} cerrada correctamente.", id);
        }

        return RedirectToAction(nameof(Incidencias));
    }
}
