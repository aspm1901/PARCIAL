namespace PlataformaIncidencias.Services;

public interface IAlgoliaSearchService
{
    Task<List<int>> SearchIncidenciaIdsAsync(string query);
}
