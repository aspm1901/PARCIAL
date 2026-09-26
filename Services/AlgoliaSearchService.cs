using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlataformaIncidencias.Services;

public class AlgoliaSearchService : IAlgoliaSearchService
{
    private readonly HttpClient _httpClient;
    private readonly string _applicationId;
    private readonly string _searchApiKey;
    private readonly string _indexName;
    private readonly ILogger<AlgoliaSearchService> _logger;

    public AlgoliaSearchService(HttpClient httpClient, IConfiguration configuration, ILogger<AlgoliaSearchService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _applicationId = configuration["Algolia:ApplicationId"] ?? "MVF8QKCJK3";
        _searchApiKey = configuration["Algolia:SearchApiKey"] ?? "1c6b471a1ddbdeb574e58451331bd140";
        _indexName = configuration["Algolia:IndexName"] ?? "incidencias";
    }

    public async Task<List<int>> SearchIncidenciaIdsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<int>();
        }

        try
        {
            var url = $"https://{_applicationId}-dsn.algolia.net/1/indexes/{_indexName}?query={Uri.EscapeDataString(query)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Algolia-Application-Id", _applicationId);
            request.Headers.Add("X-Algolia-API-Key", _searchApiKey);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Algolia search returned status code {StatusCode}", response.StatusCode);
                return new List<int>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var searchResult = JsonSerializer.Deserialize<AlgoliaResponse>(content);

            var ids = new List<int>();
            if (searchResult?.Hits != null)
            {
                foreach (var hit in searchResult.Hits)
                {
                    if (int.TryParse(hit.ObjectID, out var id))
                    {
                        ids.Add(id);
                    }
                }
            }

            _logger.LogInformation("Algolia search for '{Query}' found {Count} matching objectIDs.", query, ids.Count);
            return ids;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying Algolia for query: {Query}", query);
            return new List<int>();
        }
    }

    private class AlgoliaResponse
    {
        [JsonPropertyName("hits")]
        public List<AlgoliaHit>? Hits { get; set; }
    }

    private class AlgoliaHit
    {
        [JsonPropertyName("objectID")]
        public string? ObjectID { get; set; }
    }
}
