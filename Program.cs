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
builder.Services.AddHttpClient<ITextEmbeddingGenerationService, DescriptionEmbeddingService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/");
});
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

    var embeddingModel = builder.Configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
    await using var missingEmbeddingsCommand = dataSource.CreateCommand(
        "SELECT id, name, description FROM services WHERE description_embedding IS NULL OR embedding_model IS DISTINCT FROM $1;");
    missingEmbeddingsCommand.Parameters.AddWithValue(embeddingModel);
    await using var reader = await missingEmbeddingsCommand.ExecuteReaderAsync();
    var missingEmbeddings = new List<(Guid Id, string Name, string Description)>();

    while (await reader.ReadAsync())
    {
        missingEmbeddings.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
    }

    foreach (var service in missingEmbeddings)
    {
        var embeddingInput = $"Service name: {service.Name}. Description: {service.Description}";
        var embedding = (await embeddingService.GenerateEmbeddingsAsync([embeddingInput])).Single();
        var embeddingText = $"[{string.Join(',', embedding.ToArray().Select(value => value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)))}]";

        await using var updateCommand = dataSource.CreateCommand(
            "UPDATE services SET description_embedding = CAST($1 AS vector), embedding_model = $2 WHERE id = $3;");
        updateCommand.Parameters.AddWithValue(embeddingText);
        updateCommand.Parameters.AddWithValue(embeddingModel);
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
