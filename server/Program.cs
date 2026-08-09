using Microsoft.EntityFrameworkCore;
using RagKnowledgeService.Data;
using RagKnowledgeService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Client", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Persistence: SQLite file next to the project so documents/chunks/conversations
// survive restarts. AddDbContextFactory (not AddDbContext) because the
// singleton ISearchIndexService needs to create its own DbContext on demand.
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "rag.db");
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<AppDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

// RAG pipeline wiring. Each stage is a swappable interface:
//   - IEmbeddingService / ILlmService / IRerankerService: stubs now, real
//     providers later (one-line DI swap each)
//   - ISearchIndexService: in-memory read model now, vector store later
builder.Services.AddSingleton<ISearchIndexService, SearchIndexService>();
builder.Services.AddSingleton<IEmbeddingService, HashingEmbeddingService>();
builder.Services.AddSingleton<ILlmService, ExtractiveStubLlmService>();
builder.Services.AddSingleton<IRerankerService, HeuristicRerankerService>();

builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IChunkService, ChunkService>();
builder.Services.AddScoped<IHybridRetrievalService, HybridRetrievalService>();
builder.Services.AddScoped<IRagQueryService, RagQueryService>();
builder.Services.AddScoped<IChatService, ChatService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Client");
app.UseAuthorization();
app.MapControllers();

// Create the DB schema and seed from /docs on first run only. On later runs
// (or after edits through the CRUD API) the DB is the source of truth and the
// seed step is skipped - see DocumentService.SeedFromFolderIfEmptyAsync.
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();

    var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();
    var docsPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "docs"));
    await documentService.SeedFromFolderIfEmptyAsync(docsPath);
}

app.Run();
