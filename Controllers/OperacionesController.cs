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
    private readonly IPieSocketService _pieSocketService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OperacionesController> _logger;

    public OperacionesController(
        ApplicationDbContext context,
        IPieSocketService pieSocketService,
        IConfiguration configuration,
        ILogger<OperacionesController> logger)
    {
        _context = context;
        _pieSocketService = pieSocketService;
        _configuration = configuration;
        _logger = logger;
    }

    // GET: /Operaciones/Incidencias
    public async Task<IActionResult> Incidencias()
    {
        ViewData["PieSocketCluster"] = _configuration["PieSocket:ClusterId"] ?? "free.blr2";
        ViewData["PieSocketApiKey"] = _configuration["PieSocket:ApiKey"] ?? "lRB02NXqYlCqJjfhUb7cthtoi85WjG7KNAbuCtfu";
        ViewData["PieSocketChannel"] = _configuration["PieSocket:ChannelId"] ?? "incidencias-channel";

        var incidencias = await _context.Incidencias
            .Where(i => i.Estado == "Abierta")
            .OrderByDescending(i => i.FechaRegistro)
            .ToListAsync();

        return View(incidencias);
    }

    // GET: /Operaciones/EstadoVigente
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

    // POST: /Operaciones/Cerrar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(int id)
    {
        var incidencia = await _context.Incidencias.FindAsync(id);
        if (incidencia != null && incidencia.Estado == "Abierta")
        {
            // 1. Guardar primero el estado en la base de datos
            incidencia.Estado = "Cerrada";
            await _context.SaveChangesAsync();
            _logger.LogInformation("Incidencia {Id} cerrada en base de datos.", id);

            // 2. Publicar desde el servidor el evento IncidenciaActualizada con Id y Estado en PieHost
            await _pieSocketService.PublicarEventoAsync("IncidenciaActualizada", incidencia.Id, incidencia.Estado);
            _logger.LogInformation("Evento IncidenciaActualizada emitido a PieHost para incidencia {Id}.", id);
        }

        return RedirectToAction(nameof(Incidencias));
    }
}
