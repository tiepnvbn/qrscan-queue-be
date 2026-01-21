using Microsoft.EntityFrameworkCore;
using QueueQr.Api.Data;
using QueueQr.Api.Hubs;
using QueueQr.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Render sets PORT; ensure Kestrel listens on 0.0.0.0.
var port = Environment.GetEnvironmentVariable("PORT");
var aspnetcoreUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (!string.IsNullOrWhiteSpace(port) && string.IsNullOrWhiteSpace(aspnetcoreUrls))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod();

        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        var envAllowedOrigins = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
        if ((allowedOrigins is null || allowedOrigins.Length == 0) && !string.IsNullOrWhiteSpace(envAllowedOrigins))
        {
            allowedOrigins = envAllowedOrigins
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // If no origins are configured, allow any origin (no credentials) for simple demo usage.
        // If origins are configured (recommended for production), allow credentials.
        if (allowedOrigins is { Length: > 0 })
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowCredentials();
        }
        else
        {
            policy.SetIsOriginAllowed(_ => true);
        }
    });
});

builder.Services.AddSignalR();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var dbProvider = builder.Configuration["DbProvider"]
        ?? Environment.GetEnvironmentVariable("QUEUEQR_DB_PROVIDER")
        ?? "Postgres";

    if (string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(dbProvider, "SQLite", StringComparison.OrdinalIgnoreCase))
    {
        var sqliteConnectionString = builder.Configuration.GetConnectionString("Sqlite")
            ?? $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "queueqr.db")}";
        options.UseSqlite(sqliteConnectionString);
    }
    else
    {
        var connectionString = builder.Configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var databaseUrl =
                Environment.GetEnvironmentVariable("DATABASE_URL")
                ?? Environment.GetEnvironmentVariable("POSTGRES_URL")
                ?? Environment.GetEnvironmentVariable("POSTGRESQL_URL");

            if (!string.IsNullOrWhiteSpace(databaseUrl))
            {
                connectionString = BuildNpgsqlConnectionStringFromDatabaseUrl(databaseUrl);
            }
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "PostgreSQL is selected but no connection string is configured. " +
                "Set ConnectionStrings:Default (or env var ConnectionStrings__Default) or DATABASE_URL.");
        }

        options.UseNpgsql(connectionString);
    }
});

builder.Services.AddSingleton<IClock, AppClock>();
builder.Services.AddScoped<QueueService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.MapHub<QueueHub>("/hubs/queue");

// Seed minimal site/room data for dev.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.EnsureSeededAsync(db);
}

app.Run();

static string BuildNpgsqlConnectionStringFromDatabaseUrl(string databaseUrl)
{
    // Supports URLs like: postgres://user:pass@host:5432/dbname
    // Render typically provides DATABASE_URL in this format.
    if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri))
    {
        throw new InvalidOperationException("DATABASE_URL is not a valid absolute URI.");
    }

    var scheme = uri.Scheme.ToLowerInvariant();
    if (scheme is not ("postgres" or "postgresql"))
    {
        throw new InvalidOperationException("DATABASE_URL must start with postgres:// or postgresql://.");
    }

    var userInfoParts = uri.UserInfo.Split(':', 2);
    var username = Uri.UnescapeDataString(userInfoParts[0]);
    var password = userInfoParts.Length > 1 ? Uri.UnescapeDataString(userInfoParts[1]) : string.Empty;

    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : 5432;
    var database = uri.AbsolutePath.Trim('/');

    // Render-managed Postgres requires SSL.
    // TrustServerCertificate avoids cert chain issues in simple deployments.
    return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
}
