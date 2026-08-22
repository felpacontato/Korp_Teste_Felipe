using System.Net;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<FaturamentoDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("FaturamentoDb")));
builder.Services.AddHttpClient("Estoque", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["EstoqueService:BaseUrl"] ?? "http://localhost:5001");
});

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FaturamentoDbContext>();
    db.Database.Migrate();
}

var notas = app.MapGroup("/notas");

notas.MapGet("/", async (FaturamentoDbContext db) =>
    await db.Notas.Include(n => n.Itens).AsNoTracking().OrderByDescending(n => n.Numero).ToListAsync());

notas.MapGet("/{id:int}", async (int id, FaturamentoDbContext db) =>
{
    var nota = await db.Notas.Include(n => n.Itens).AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);
    return nota is null ? Results.NotFound("Nota fiscal nao encontrada.") : Results.Ok(nota);
});

notas.MapPost("/", async (NotaRequest request, FaturamentoDbContext db) =>
{
    if (request.Itens.Count == 0)
        return Results.BadRequest("Informe ao menos um item.");

    if (request.Itens.Any(i => string.IsNullOrWhiteSpace(i.ProdutoCodigo) || i.Quantidade <= 0))
        return Results.BadRequest("Produtos e quantidades devem ser validos.");

    var proximoNumero = await db.Notas.AnyAsync() ? await db.Notas.MaxAsync(n => n.Numero) + 1 : 1;
    var nota = new NotaFiscal
    {
        Numero = proximoNumero,
        Status = StatusNota.Aberta,
        Itens = request.Itens.Select(i => new NotaItem
        {
            ProdutoCodigo = i.ProdutoCodigo.Trim(),
            Quantidade = i.Quantidade
        }).ToList()
    };

    db.Notas.Add(nota);
    await db.SaveChangesAsync();

    return Results.Created($"/notas/{nota.Id}", nota);
});

notas.MapPost("/{id:int}/imprimir", async (
    int id,
    ImprimirNotaRequest request,
    FaturamentoDbContext db,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("ImpressaoNota");
    var nota = await db.Notas.Include(n => n.Itens).FirstOrDefaultAsync(n => n.Id == id);

    if (nota is null)
        return Results.NotFound("Nota fiscal nao encontrada.");

    if (nota.Status != StatusNota.Aberta)
        return Results.Conflict("Apenas notas abertas podem ser impressas.");

    var client = httpClientFactory.CreateClient("Estoque");
    var baixa = new BaixaEstoqueRequest(nota.Itens.Select(i => new ItemEstoqueRequest(i.ProdutoCodigo, i.Quantidade)).ToList(), request.SimularFalha);

    try
    {
        var response = await client.PostAsJsonAsync("/produtos/baixar-saldo", baixa);
        if (!response.IsSuccessStatusCode)
        {
            var detalhe = await response.Content.ReadAsStringAsync();
            return response.StatusCode switch
            {
                HttpStatusCode.Conflict => Results.Conflict(detalhe),
                HttpStatusCode.NotFound => Results.NotFound(detalhe),
                _ => Results.Problem("Nao foi possivel atualizar o estoque. A nota permanece aberta.", statusCode: 503)
            };
        }

        nota.Status = StatusNota.Fechada;
        nota.ImpressoEm = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(nota);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Falha ao chamar o servico de estoque");
        return Results.Problem("Servico de estoque indisponivel. A nota permanece aberta.", statusCode: 503);
    }
});

app.Run();

public sealed class FaturamentoDbContext(DbContextOptions<FaturamentoDbContext> options) : DbContext(options)
{
    public DbSet<NotaFiscal> Notas => Set<NotaFiscal>();
    public DbSet<NotaItem> NotaItens => Set<NotaItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotaFiscal>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.HasIndex(n => n.Numero).IsUnique();
            entity.Property(n => n.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasMany(n => n.Itens).WithOne().HasForeignKey(i => i.NotaFiscalId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotaItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.ProdutoCodigo).HasMaxLength(30).IsRequired();
        });
    }
}

public sealed class NotaFiscal
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public StatusNota Status { get; set; } = StatusNota.Aberta;
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ImpressoEm { get; set; }
    public List<NotaItem> Itens { get; set; } = [];
}

public sealed class NotaItem
{
    public int Id { get; set; }
    public int NotaFiscalId { get; set; }
    public string ProdutoCodigo { get; set; } = "";
    public int Quantidade { get; set; }
}

public enum StatusNota
{
    Aberta,
    Fechada
}

public sealed record NotaRequest(List<NotaItemRequest> Itens);
public sealed record NotaItemRequest(string ProdutoCodigo, int Quantidade);
public sealed record ImprimirNotaRequest(bool SimularFalha = false);
public sealed record BaixaEstoqueRequest(List<ItemEstoqueRequest> Itens, bool SimularFalha = false);
public sealed record ItemEstoqueRequest(string ProdutoCodigo, int Quantidade);
