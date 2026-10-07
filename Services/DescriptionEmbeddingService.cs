using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

namespace AppointEase_CatalogService.Services;

public sealed class DescriptionEmbeddingService : ITextEmbeddingGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public DescriptionEmbeddingService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public IReadOnlyDictionary<string, object?> Attributes { get; } =
        new Dictionary<string, object?>();

    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> data,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey is missing from configuration.");
        var model = _configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/embeddings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new EmbeddingRequest(model, data));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"OpenAI embeddings request failed ({(int)response.StatusCode}): {details}");
        }

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken)
            ?? throw new InvalidOperationException("OpenAI returned an empty embeddings response.");

        return result.Data
            .OrderBy(item => item.Index)
            .Select(item => (ReadOnlyMemory<float>)item.Embedding)
            .ToList();
    }

    private sealed record EmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] IList<string> Input);

    private sealed record EmbeddingResponse(
        [property: JsonPropertyName("data")] List<EmbeddingData> Data);

    private sealed record EmbeddingData(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("embedding")] float[] Embedding);
}
