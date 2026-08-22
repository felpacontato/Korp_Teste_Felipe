using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<EstoqueDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("EstoqueDb")));

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EstoqueDbContext>();
    db.Database.Migrate();
}

var produtos = app.MapGroup("/produtos");

produtos.MapGet("/", async (EstoqueDbContext db) =>
    await db.Produtos.AsNoTracking().OrderBy(p => p.Codigo).ToListAsync());

produtos.MapGet("/{codigo}", async (string codigo, EstoqueDbContext db) =>
{
    var produto = await db.Produtos.AsNoTracking().FirstOrDefaultAsync(p => p.Codigo == codigo);
    return produto is null ? Results.NotFound("Produto nao encontrado.") : Results.Ok(produto);
});

produtos.MapPost("/", async (ProdutoRequest request, EstoqueDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.Descricao))
        return Results.BadRequest("Codigo e descricao sao obrigatorios.");

    if (request.Saldo < 0)
        return Results.BadRequest("Saldo nao pode ser negativo.");

    var exists = await db.Produtos.AnyAsync(p => p.Codigo == request.Codigo);
    if (exists)
        return Results.Conflict("Ja existe produto com esse codigo.");

    var produto = new Produto
    {
        Codigo = request.Codigo.Trim(),
        Descricao = request.Descricao.Trim(),
        Saldo = request.Saldo
    };

    db.Produtos.Add(produto);
    await db.SaveChangesAsync();

    return Results.Created($"/produtos/{produto.Codigo}", produto);
});

produtos.MapPost("/baixar-saldo", async (BaixaEstoqueRequest request, EstoqueDbContext db) =>
{
    if (request.SimularFalha)
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

    await using var transaction = await db.Database.BeginTransactionAsync();

    foreach (var item in request.Itens)
    {
        var produto = await db.Produtos.FirstOrDefaultAsync(p => p.Codigo == item.ProdutoCodigo);
        if (produto is null)
            return Results.NotFound($"Produto {item.ProdutoCodigo} nao encontrado.");

        if (item.Quantidade <= 0)
            return Results.BadRequest("Quantidade deve ser maior que zero.");

        if (produto.Saldo < item.Quantidade)
            return Results.Conflict($"Saldo insuficiente para o produto {produto.Codigo}.");

        produto.Saldo -= item.Quantidade;
    }

    await db.SaveChangesAsync();
    await transaction.CommitAsync();

    return Results.Ok(new { mensagem = "Estoque atualizado com sucesso." });
});

app.Run();

public sealed class EstoqueDbContext(DbContextOptions<EstoqueDbContext> options) : DbContext(options)
{
    public DbSet<Produto> Produtos => Set<Produto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Produto>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.Codigo).IsUnique();
            entity.Property(p => p.Codigo).HasMaxLength(30).IsRequired();
            entity.Property(p => p.Descricao).HasMaxLength(160).IsRequired();
        });
    }
}

public sealed class Produto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public int Saldo { get; set; }
}

public sealed record ProdutoRequest(string Codigo, string Descricao, int Saldo);
public sealed record BaixaEstoqueRequest(List<ItemEstoqueRequest> Itens, bool SimularFalha = false);
public sealed record ItemEstoqueRequest(string ProdutoCodigo, int Quantidade);
