using AiChatBox.Api.Data;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Middleware;
using AiChatBox.Api.Models;
using AiChatBox.Api.Services;
using AiChatBox.Api.Services.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Hangfire;
using Hangfire.PostgreSql;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration);
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContextFactory<ChatDbContext>(options => {
    options.UseNpgsql(connectionString, o => o.UseVector());
});

builder.Services.AddDbContext<ChatDbContext>(options => {
    options.UseNpgsql(connectionString, o => o.UseVector());
}, ServiceLifetime.Scoped, ServiceLifetime.Singleton);

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<ChatDbContext>()
.AddDefaultTokenProviders();

// Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSettings["Key"] ?? "super_secret_key_change_this_in_production_123456!!");

builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options => {
    options.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/liveAudioHub"))
            {
                context.Token = accessToken;
            }
            else if (path.StartsWithSegments("/hangfire"))
            {
                if (context.HttpContext.Request.Cookies.TryGetValue("hangfire_auth", out var cookieToken))
                {
                    context.Token = cookieToken;
                }
            }
            return Task.CompletedTask;
        }
    };
})
.AddGoogle(options => {
    var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
    options.ClientId = string.IsNullOrEmpty(googleClientId) ? "placeholder" : googleClientId;
    
    var googleSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    options.ClientSecret = string.IsNullOrEmpty(googleSecret) ? "placeholder" : googleSecret;
})
.AddGitHub(options => {
    var githubClientId = builder.Configuration["Authentication:GitHub:ClientId"];
    options.ClientId = string.IsNullOrEmpty(githubClientId) ? "placeholder" : githubClientId;
    
    var githubSecret = builder.Configuration["Authentication:GitHub:ClientSecret"];
    options.ClientSecret = string.IsNullOrEmpty(githubSecret) ? "placeholder" : githubSecret;
});

// Encryption
builder.Services.AddSingleton<EncryptionService>();
builder.Services.AddScoped<ExportService>();

// AI Services
builder.Services.AddHttpClient();
builder.Services.AddScoped<GeminiServerService>();
builder.Services.AddScoped<GrokServerService>();
builder.Services.AddScoped<LlmProviderFactory>();
builder.Services.AddScoped<IChatContextService, ChatContextService>();
builder.Services.AddScoped<IAiLoggingService, AiLoggingService>();
builder.Services.AddScoped<GroqAudioService>();
builder.Services.AddScoped<GeminiTtsService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddHttpClient<EmbeddingService>();
builder.Services.AddHttpClient<GeminiServerService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<FileProcessingService>();
builder.Services.AddScoped<FirecrawlService>();
builder.Services.AddHttpClient<FirecrawlService>();

// Agent & Tools
builder.Services.AddScoped<ITool, InternalSqlTool>();
builder.Services.AddScoped<ToolRegistry>();
builder.Services.AddScoped<AgentService>();

builder.Services.AddScoped<IAiChatService, AiChatService>();
builder.Services.AddScoped<ApiKeyService>();
builder.Services.AddScoped<WebhookService>();

// Hangfire configuration
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer(options => {
    options.WorkerCount = Environment.ProcessorCount * 2;
});

builder.Services.AddScoped<FirecrawlBackgroundService>();
builder.Services.AddScoped<LogPruningService>();

// Live Mode
builder.Services.AddSingleton<LiveSessionManager>();
builder.Services.AddScoped<IGeminiLiveService, GeminiLiveService>();
builder.Services.AddSignalR(options => {
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
});

builder.Services.AddSingleton<IAuthorizationHandler, ApiKeyOrJwtHandler>();
builder.Services.AddAuthorizationBuilder().AddPolicy("ApiKeyOrJwt", policy => policy.AddRequirements(new ApiKeyOrJwtRequirement()));

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

// Enable Forwarded Headers for reverse proxy support (Caddy)
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

// Apply migrations at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    try 
    {
        Console.WriteLine("[Startup] Running Migrations...");
        db.Database.Migrate();
        Console.WriteLine("[Startup] Migrations completed successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Critical database error: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseStaticFiles(); // Default wwwroot
var widgetPath = Path.Combine(app.Environment.ContentRootPath, "..", "AiChatBox.Widget");
if (!Directory.Exists(widgetPath))
{
    // Fallback for Docker/Production structure
    widgetPath = Path.Combine(app.Environment.ContentRootPath, "widget");
}

if (Directory.Exists(widgetPath))
{
    Console.WriteLine($"[Startup] Serving widget files from: {widgetPath}");
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(widgetPath),
        RequestPath = "/widget",
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
            ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");
        }
    });
}
else
{
    Console.WriteLine($"[Startup] Warning: Widget directory not found at {widgetPath}. Widget may not be served correctly.");
}

app.UseHttpsRedirection();

app.UseSerilogIngestion();
app.UseSerilogRequestLogging();

app.UseMiddleware<ApiKeyMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<LiveAudioHub>("/liveAudioHub").DisableAntiforgery();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new AiChatBox.Api.Filters.HangfireAuthorizationFilter() }
});

RecurringJob.AddOrUpdate<LogPruningService>("log-pruning", service => service.ExecuteAsync(), Cron.Daily);
RecurringJob.AddOrUpdate<FirecrawlBackgroundService>("firecrawl-polling", service => service.ExecuteAsync(), Cron.Minutely);

app.Run();
