using System.Text;
using AppointEase_CatalogService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel.Embeddings;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing from configuration.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<ITextEmbeddingGenerationService, DescriptionEmbeddingService>();
builder.Services.AddSingleton(_ =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.")));
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
    var embeddingService = scope.ServiceProvider.GetRequiredService<ITextEmbeddingGenerationService>();
    var schemaPath = Path.Combine(app.Environment.ContentRootPath, "Data", "services.sql");

    if (File.Exists(schemaPath))
    {
        var schema = await File.ReadAllTextAsync(schemaPath);
        await using var command = dataSource.CreateCommand(schema);
        await command.ExecuteNonQueryAsync();
    }

    await using var missingEmbeddingsCommand = dataSource.CreateCommand(
        "SELECT id, description FROM services WHERE description_embedding IS NULL;");
    await using var reader = await missingEmbeddingsCommand.ExecuteReaderAsync();
    var missingEmbeddings = new List<(Guid Id, string Description)>();

    while (await reader.ReadAsync())
    {
        missingEmbeddings.Add((reader.GetGuid(0), reader.GetString(1)));
    }

    foreach (var service in missingEmbeddings)
    {
        var embedding = (await embeddingService.GenerateEmbeddingsAsync([service.Description])).Single();
        var embeddingText = $"[{string.Join(',', embedding.ToArray().Select(value => value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)))}]";

        await using var updateCommand = dataSource.CreateCommand(
            "UPDATE services SET description_embedding = CAST($1 AS vector) WHERE id = $2;");
        updateCommand.Parameters.AddWithValue(embeddingText);
        updateCommand.Parameters.AddWithValue(service.Id);
        await updateCommand.ExecuteNonQueryAsync();
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "ok - Catalog Service" }));
app.MapOpenApi();
app.MapScalarApiReference();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
