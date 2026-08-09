using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Qdrant.Client;
using RagKnowledgeService.Data;
using RagKnowledgeService.Options;
using RagKnowledgeService.Repositories;
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

builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.SectionName));
builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection(QdrantOptions.SectionName));

// Persistence: SQLite file next to the project so documents/chunks/conversations
// survive restarts. AddDbContextFactory (not AddDbContext) because the
// singleton ISearchIndexService needs to create its own DbContext on demand.
var dbPath = builder.Configuration["DatabasePath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "rag.db");
var dbDirectory = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrWhiteSpace(dbDirectory)) Directory.CreateDirectory(dbDirectory);
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<AppDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

// Qdrant: gRPC client against the local docker-compose container (see docker-compose.yml).
var qdrantSection = builder.Configuration.GetSection(QdrantOptions.SectionName).Get<QdrantOptions>()
    ?? throw new InvalidOperationException("Missing Qdrant configuration section.");
builder.Services.AddSingleton(new QdrantClient(qdrantSection.Host, qdrantSection.GrpcPort));
builder.Services.AddSingleton<IVectorStore, QdrantVectorStore>();

// Ollama: local model server, no API key. Generous timeout since a CPU-bound
// gemma3 chat completion can take well longer than the default 100s.
var ollamaSection = builder.Configuration.GetSection(OllamaOptions.SectionName).Get<OllamaOptions>()
    ?? throw new InvalidOperationException("Missing Ollama configuration section.");
builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(client =>
{
    client.BaseAddress = new Uri(ollamaSection.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient<ILlmService, OllamaLlmService>(client =>
{
    client.BaseAddress = new Uri(ollamaSection.BaseUrl);
    client.Timeout = TimeSpan.FromMinutes(3);
});

// Data-access layer: Controllers -> Services (business logic) -> Repositories
// (EF Core access) -> AppDbContext. Kept as thin, single-entity-focused
// repositories rather than one generic repository - each maps to exactly the
// queries its service actually needs.
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IChunkRepository, ChunkRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();

// RAG pipeline wiring. Retrieval is hybrid BM25 (in-process) + semantic
// (Qdrant), fused with RRF, then reranked - see HybridRetrievalService.
builder.Services.AddSingleton<ISearchIndexService, SearchIndexService>();
builder.Services.AddSingleton<IRerankerService, HeuristicRerankerService>();

builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IDocumentFileExtractor, DocumentFileExtractor>();
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

// A local-model call failing (Ollama/Qdrant unreachable, model not pulled) is
// an upstream-dependency failure, not a bug in this service - map it to 502
// centrally instead of a try/catch in every controller that happens to touch
// a model call.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (error is ModelServiceUnavailableException modelEx)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsJsonAsync(new { error = modelEx.Message });
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
    });
});

app.UseCors("Client");
app.UseAuthorization();
app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));
app.MapControllers();

// Create the DB schema, ensure the Qdrant collection exists, and seed from
// /docs on first run only. On later runs (or after edits through the CRUD
// API) the DB + Qdrant are the source of truth and seeding is skipped - see
// DocumentService.SeedFromFolderIfEmptyAsync.
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();

    var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
    await vectorStore.EnsureCollectionAsync();

    var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();
    var docsPath = builder.Configuration["SeedFolderPath"]
        ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "docs"));
    await documentService.SeedFromFolderIfEmptyAsync(docsPath);
}

app.Run();
