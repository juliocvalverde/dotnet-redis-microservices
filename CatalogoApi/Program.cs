using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 1. Configurar o Redis como um Singleton na aplicação
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// 2. Endpoint para SALVAR dados no Redis (Cache)
app.MapPost("/catalogo/produto", async (
    [FromBody] ProdutoDto produto, 
    [FromServices] IConnectionMultiplexer redis) =>
{
    var db = redis.GetDatabase();
    
    // Serializamos o objeto simples para JSON para guardar no Redis
    string produtoJson = System.Text.Json.JsonSerializer.Serialize(produto);

    // Salvamos a chave com um TTL (Tempo de Expiração) de 60 segundos
    await db.StringSetAsync($"produto:{produto.Id}", produtoJson, TimeSpan.FromSeconds(60));

    return Results.Ok(new { mensagem = "Produto salvo no Redis com sucesso!", produto });
});

// 3. Endpoint para BUSCAR dados do Redis
app.MapGet("/catalogo/produto/{id}", async (
    int id, 
    [FromServices] IConnectionMultiplexer redis) =>
{
    var db = redis.GetDatabase();
    
    // Buscamos a chave no Redis
    string? produtoJson = await db.StringGetAsync($"produto:{id}");

    if (string.IsNullOrEmpty(produtoJson))
    {
        return Results.NotFound(new { mensagem = "Cache Miss: Produto não encontrado no Redis ou expirado." });
    }

    var produto = System.Text.Json.JsonSerializer.Deserialize<ProdutoDto>(produtoJson);
    return Results.Ok(new { origem = "Cache (Redis)", produto });
});

app.Run();

// DTO de exemplo para o produto
public record ProdutoDto(int Id, string Nome, decimal Preco);