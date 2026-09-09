using System.Security.Cryptography;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

namespace AppointEase_CatalogService.Services;

public sealed class DescriptionEmbeddingService : ITextEmbeddingGenerationService
{
    public IReadOnlyDictionary<string, object?> Attributes { get; } =
        new Dictionary<string, object?>();

    public Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> data,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        IList<ReadOnlyMemory<float>> embeddings = data
            .Select(CreateEmbedding)
            .ToList();

        return Task.FromResult(embeddings);
    }

    private static ReadOnlyMemory<float> CreateEmbedding(string description)
    {
        var values = new float[1536];
        var descriptionBytes = Encoding.UTF8.GetBytes(description);

        for (var index = 0; index < values.Length; index++)
        {
            var input = new byte[descriptionBytes.Length + sizeof(int)];
            Buffer.BlockCopy(descriptionBytes, 0, input, 0, descriptionBytes.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(index), 0, input, descriptionBytes.Length, sizeof(int));
            var hash = SHA256.HashData(input);
            values[index] = (float)((BitConverter.ToUInt16(hash, 0) / (double)ushort.MaxValue * 2d) - 1d);
        }

        return values;
    }
}
