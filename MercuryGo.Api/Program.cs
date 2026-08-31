using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Data;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Default") 
    ?? "Server=localhost;Port=3306;Database=mercurygo_app;User=mercurygo_app;Password=MercuryGO_2026!;";

const string corsPolicy = "IonicDevelopment";
var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

if (builder.Environment.IsDevelopment() && corsAllowedOrigins.Length == 0)
{
    corsAllowedOrigins = [
        "http://localhost:8100",
        "http://127.0.0.1:8100",
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "capacitor://localhost",
        "ionic://localhost"
    ];
}

builder.Services.AddControllers().AddJsonOptions(options => ConfigurarJson(options.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(options => ConfigurarJson(options.SerializerOptions));

builder.Services.AddDbContext<MercuryGoDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)))
           .UseSnakeCaseNamingConvention());

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options => 
        options.AddPolicy(corsPolicy, policy => 
            policy.SetIsOriginAllowed(EsOrigenDesarrolloPermitido)
                  .AllowAnyHeader()
                  .AllowAnyMethod()));
}
else if (corsAllowedOrigins.Length > 0)
{
    builder.Services.AddCors(options => 
        options.AddPolicy(corsPolicy, policy => 
            policy.WithOrigins(corsAllowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()));
}

var app = builder.Build();

if (app.Environment.IsDevelopment() || corsAllowedOrigins.Length > 0)
{
    app.UseCors(corsPolicy);
}

// Las imagenes del catálogo se sirven desde wwwroot/seed/
app.UseStaticFiles();

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<MercuryGoDbContext>();
    await db.Database.MigrateAsync();
}

app.MapControllers();

// Endpoint de prueba de vida /health
app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }));

app.Run();

static void ConfigurarJson(JsonSerializerOptions options)
{
    options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
}

static bool EsOrigenDesarrolloPermitido(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
    if (uri.Scheme is "capacitor" or "ionic" && uri.Host == "localhost") return true;
    if (uri.Scheme is not ("http" or "https")) return false;
    if (uri.Host is "localhost" or "127.0.0.1") return true;
    return IPAddress.TryParse(uri.Host, out var ip) && EsIpPrivada(ip);
}

static bool EsIpPrivada(IPAddress ip)
{
    if (IPAddress.IsLoopback(ip)) return true;
    var bytes = ip.GetAddressBytes();
    return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && (
        bytes[0] == 10 ||
        bytes[0] == 192 && bytes[1] == 168 ||
        bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31
    );
}
