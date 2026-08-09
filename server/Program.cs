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

// RAG pipeline wiring. Each stage is a swappable interface:
//   - IEmbeddingService / ILlmService: stub now, real provider later (one-line swap)
//   - IKnowledgeStore: in-memory now, vector DB later
builder.Services.AddSingleton<IKnowledgeStore, InMemoryKnowledgeStore>();
builder.Services.AddSingleton<IEmbeddingService, HashingEmbeddingService>();
builder.Services.AddSingleton<ILlmService, ExtractiveStubLlmService>();
builder.Services.AddSingleton<IIngestionService, IngestionService>();
builder.Services.AddScoped<IRetrievalService, RetrievalService>();
builder.Services.AddScoped<IAskService, AskService>();

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

// Ingest the sample docs folder once at startup so the index is warm
// before the first /api/ask request.
var docsPath = Path.Combine(builder.Environment.ContentRootPath, "..", "docs");
using (var scope = app.Services.CreateScope())
{
    var ingestionService = scope.ServiceProvider.GetRequiredService<IIngestionService>();
    await ingestionService.IngestFolderAsync(Path.GetFullPath(docsPath));
}

app.Run();
