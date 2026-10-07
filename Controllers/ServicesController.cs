using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel.Embeddings;
using Npgsql;

namespace AppointEase_CatalogService.Controllers;

[ApiController]
[Route("api/catalog")]
[Authorize]
public sealed class ServicesController : ControllerBase
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ITextEmbeddingGenerationService _embeddingService;

    public ServicesController(
        NpgsqlDataSource dataSource,
        ITextEmbeddingGenerationService embeddingService)
    {
        _dataSource = dataSource;
        _embeddingService = embeddingService;
    }

    [HttpGet("services")]
    public async Task<ActionResult<List<ServiceItem>>> GetServices(CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand("""
            SELECT id, name, description, price, duration_minutes, created_at
            FROM services
            ORDER BY name ASC;
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var services = new List<ServiceItem>();

        while (await reader.ReadAsync(cancellationToken))
        {
            services.Add(new ServiceItem(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetInt32(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return services;
    }

    [HttpGet("services/{id:guid}")]
    public async Task<ActionResult<ServiceItem>> GetServiceById(Guid id, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand("""
            SELECT id, name, description, price, duration_minutes, created_at
            FROM services
            WHERE id = $1;
            """);
        command.Parameters.AddWithValue(id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return NotFound(new { message = "Service not found." });
        }

        return new ServiceItem(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.GetInt32(4),
            reader.GetFieldValue<DateTimeOffset>(5));
    }

    [HttpPost("services")]
    public async Task<ActionResult<ServiceItem>> AddService(
        [FromBody] AddServiceRequest request,
        CancellationToken cancellationToken)
    {
        var embeddingInput = $"Service name: {request.Name}. Description: {request.Description}";
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(
            [embeddingInput],
            cancellationToken: cancellationToken);
        var embedding = embeddings.Single();
        var embeddingText = $"[{string.Join(',', embedding.ToArray().Select(value => value.ToString("0.######", CultureInfo.InvariantCulture)))}]";

        await using var command = _dataSource.CreateCommand("""
            INSERT INTO services (name, description, price, duration_minutes, description_embedding, embedding_model)
            VALUES ($1, $2, $3, $4, CAST($5 AS vector), $6)
            RETURNING id, name, description, price, duration_minutes, created_at;
            """);

        command.Parameters.AddWithValue(request.Name);
        command.Parameters.AddWithValue(request.Description);
        command.Parameters.AddWithValue(request.Price);
        command.Parameters.AddWithValue(request.DurationMinutes);
        command.Parameters.AddWithValue(embeddingText);
        command.Parameters.AddWithValue("text-embedding-3-small");

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);

            var response = new ServiceItem(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetInt32(4),
                reader.GetFieldValue<DateTimeOffset>(5));

            return Created($"/api/catalog/services/{response.Id}", response);
        }
        catch (PostgresException exception) when (exception.SqlState == "23505")
        {
            return Conflict(new { message = "A service with this name already exists." });
        }
    }
}

public sealed record AddServiceRequest(
    string Name,
    string Description,
    decimal Price,
    int DurationMinutes);

public sealed record ServiceItem(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int DurationMinutes,
    DateTimeOffset CreatedAt);
