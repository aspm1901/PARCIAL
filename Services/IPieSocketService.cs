namespace PlataformaIncidencias.Services;

public interface IPieSocketService
{
    Task PublicarEventoAsync(string evento, int id, string estado);
}
