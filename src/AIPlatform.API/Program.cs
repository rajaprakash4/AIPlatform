using AIPlatform.Core.Interfaces;
using AIPlatform.Infrastructure.Data;
using AIPlatform.Infrastructure.Factories;
using AIPlatform.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient<GeminiService>(client =>
{
    // Optional: Set default timeouts here
    client.Timeout = TimeSpan.FromSeconds(30);
});
// 1. Register the Concrete Services (The Adapters)
// Note: We register them as their *concrete* types so the Factory can grab them.
builder.Services.AddHttpClient<OpenAIService>(client =>
{
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {builder.Configuration["AI:OpenAI:ApiKey"]}");
});

// 2. Register the Factory
builder.Services.AddScoped<IAIServiceFactory, AIServiceFactory>();
builder.Services.AddControllers();
// 3. (Optional) Register a "Default" IAIService if you just want simple injection elsewhere
builder.Services.AddScoped<IAIService>(sp =>
    sp.GetRequiredService<IAIServiceFactory>().GetDefaultService());

// 4. Register Concrete Repositories (Adapters)
builder.Services.AddSingleton<MongoChatRepository>();

// 5. Register the Factory
builder.Services.AddSingleton<IRepositoryFactory, RepositoryFactory>();

// 6. Register the Default Interface (Convenience)
// This asks the factory to give us the configured repo immediately
builder.Services.AddSingleton<IChatRepository>(sp =>
    sp.GetRequiredService<IRepositoryFactory>().GetRepository());
// 7. Inject smart text chunker to split the file for knowledgebase
builder.Services.AddSingleton<ITextChunker, SmartTextChunker>();
builder.Services.AddScoped<ToolRegistry>();
// Infrastructure
builder.Services.AddSingleton<IVectorStore, QdrantVectorStore>();
// 1. Get the settings from appsettings.json
var mongoConn = builder.Configuration["Database:ConnectionString"];
var mongoDbName = builder.Configuration["Database:Name"] ?? "ai_platform";

// 2. Register the INTERFACE (IToolRepository)
// This fixes AgentOrchestrator and PlannerService, which ask for the Interface.
builder.Services.AddScoped<AIPlatform.Core.Interfaces.IToolRepository>(sp =>
{
    return new AIPlatform.Infrastructure.Data.MongoToolRepository(mongoConn, mongoDbName);
});

// 3. Register the CONCRETE CLASS (MongoToolRepository)
// This fixes the specific error "Unable to resolve service for type System.String".
builder.Services.AddScoped<AIPlatform.Infrastructure.Data.MongoToolRepository>(sp =>
{
    return new AIPlatform.Infrastructure.Data.MongoToolRepository(mongoConn, mongoDbName);
}); builder.Services.AddScoped<DynamicToolHandler>();
builder.Services.AddScoped<AgentOrchestrator>();
builder.Services.AddScoped<InputValidationService>();
builder.Services.AddScoped<PlannerService>();
// Allow React (running on localhost) to call this API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173") // Vite's default port
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())

{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowReactApp");
app.UseHttpsRedirection();

app.MapControllers();
app.Run();

