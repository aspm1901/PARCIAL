using System.ComponentModel.DataAnnotations;

namespace PlataformaIncidencias.Models;

public class Incidencia
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Estacion { get; set; } = string.Empty;

    [Required]
    [StringLength(250)]
    public string Descripcion { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Prioridad { get; set; } = "Media"; // Alta, Media, Baja

    [Required]
    [StringLength(20)]
    public string Estado { get; set; } = "Abierta"; // Abierta, Cerrada

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}
