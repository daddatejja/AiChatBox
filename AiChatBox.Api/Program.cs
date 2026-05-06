using AiChatBox.Api.Data;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Services;
using AiChatBox.Api.Services.Tools;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
// We register the factory as Scoped to avoid the "Cannot consume scoped service from singleton" error
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContextFactory<ChatDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")),
    ServiceLifetime.Scoped);

// AI Services
builder.Services.AddHttpClient();
builder.Services.AddScoped<GeminiServerService>();
builder.Services.AddScoped<GrokServerService>();
builder.Services.AddScoped<LlmProviderFactory>();
builder.Services.AddScoped<IChatContextService, ChatContextService>();
builder.Services.AddScoped<IAiLoggingService, AiLoggingService>();
builder.Services.AddScoped<GroqAudioService>();
builder.Services.AddScoped<GeminiTtsService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<FileProcessingService>();

// Agent & Tools
builder.Services.AddScoped<ITool, SqlTool>();
builder.Services.AddScoped<ToolRegistry>();
builder.Services.AddScoped<AgentService>();

builder.Services.AddScoped<IAiChatService, AiChatService>();

// Live Mode
builder.Services.AddSingleton<LiveSessionManager>();
builder.Services.AddScoped<IGeminiLiveService, GeminiLiveService>();
builder.Services.AddSignalR(options => {
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
});

builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration);
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Ensure Database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");
    }
});
app.UseHttpsRedirection();

app.UseSerilogIngestion();
app.UseSerilogRequestLogging();

app.UseAuthorization();


app.MapControllers();
app.MapHub<LiveAudioHub>("/liveAudioHub").DisableAntiforgery();

app.Run();
