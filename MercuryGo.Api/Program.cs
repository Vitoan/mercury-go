using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Identity;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

// QuestPDF exige declarar la licencia Community
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Default") 
    ?? "Server=localhost;Port=3306;Database=mercurygo_app;User=mercurygo_app;Password=MercuryGO_2026!;";

const string corsPolicy = "MercuryGoCors";
var corsConfigurados = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var origenesProduccion = new List<string>
{
    "http://localhost",
    "https://localhost",
    "http://localhost:8100",
    "http://localhost:5173",
    "http://localhost:3000",
    "capacitor://localhost",
    "ionic://localhost"
};
if (corsConfigurados.Length > 0)
{
    origenesProduccion.AddRange(corsConfigurados);
}

var jwtOpciones = builder.Configuration.GetSection(JwtOpciones.Seccion).Get<JwtOpciones>() ?? new JwtOpciones();
if (string.IsNullOrWhiteSpace(jwtOpciones.Key))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("No se configuró Jwt:Key en producción.");
    jwtOpciones.Key = TokenService.GenerarClaveEfimera();
}
var tokenService = new TokenService(jwtOpciones);
builder.Services.AddSingleton(jwtOpciones);
builder.Services.AddSingleton(tokenService);
builder.Services.AddSingleton<ILoginRateLimiter, LoginRateLimiterService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOpciones.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOpciones.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = tokenService.ClaveFirma,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            // Valida en cada request que el usuario siga activo y conserve el rol del token.
            // Una baja o cambio de rol toma efecto instantáneamente.
            OnTokenValidated = async contexto =>
            {
                var db = contexto.HttpContext.RequestServices.GetRequiredService<MercuryGoDbContext>();
                var usuarioId = contexto.Principal?.UsuarioId() ?? 0;
                if (usuarioId <= 0)
                {
                    contexto.Fail("Token sin usuario válido.");
                    return;
                }

                var usuario = await db.Usuarios.AsNoTracking()
                    .Where(u => u.Id == usuarioId)
                    .Select(u => new { u.Activo, u.RolId })
                    .FirstOrDefaultAsync(contexto.HttpContext.RequestAborted);

                if (usuario is null || !usuario.Activo)
                {
                    contexto.Fail("El usuario fue dado de baja.");
                    return;
                }

                var rolActual = usuario.RolId.HasValue
                    ? await db.Roles.AsNoTracking().Where(r => r.Id == usuario.RolId.Value && r.Activo).Select(r => r.Codigo).FirstOrDefaultAsync(contexto.HttpContext.RequestAborted)
                    : null;

                if (rolActual != contexto.Principal?.RolCodigo())
                {
                    contexto.Fail("El rol del usuario cambió; vuelva a iniciar sesión.");
                }
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<MercuryGo.Api.Services.IPedidoTransicionService, MercuryGo.Api.Services.PedidoTransicionService>();
builder.Services.AddScoped<MercuryGo.Api.Services.IdempotenciaService>();
builder.Services.AddScoped<MercuryGo.Api.Services.NotificacionService>();
builder.Services.AddScoped<MercuryGo.Api.Services.ConflictoOfflineService>();
builder.Services.AddHostedService<MercuryGo.Api.Services.RetencionArchivosService>();

builder.Services.AddControllers().AddJsonOptions(options => ConfigurarJson(options.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(options => ConfigurarJson(options.SerializerOptions));

builder.Services.AddDbContext<MercuryGoDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)))
           .UseSnakeCaseNamingConvention());

builder.Services.AddCors(options => 
{
    options.AddPolicy(corsPolicy, policy => 
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(EsOrigenDesarrolloPermitido)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .SetPreflightMaxAge(TimeSpan.FromHours(1));
        }
        else
        {
            policy.WithOrigins(origenesProduccion.Distinct().ToArray())
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .SetPreflightMaxAge(TimeSpan.FromHours(1));
        }
    });
});

var app = builder.Build();

app.UseCors(corsPolicy);

// Asegurar directorios de uploads de archivos y fotos
var webRootPath = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(webRootPath, "uploads", "productos"));
Directory.CreateDirectory(Path.Combine(webRootPath, "uploads", "comprobantes"));
Directory.CreateDirectory(Path.Combine(webRootPath, "seed", "productos"));

// Las imágenes del catálogo se sirven públicamente; comprobantes bancarios quedan restringidos (P0-3)
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Denegar acceso anónimo directo a comprobantes bancarios y transferencias
        if (ctx.Context.Request.Path.StartsWithSegments("/uploads/comprobantes", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.Context.Response.ContentLength = 0;
            ctx.Context.Response.Body = Stream.Null;
            return;
        }

        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Headers", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS");
    }
});

// Autenticación antes de Autorización
app.UseAuthentication();
app.UseAuthorization();

// Aplicar migraciones de base de datos automáticamente al arrancar
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MercuryGoDbContext>();
    await db.Database.MigrateAsync();

    // Sincronizar o validar contraseña de Admin en producción
    var adminPassword = app.Configuration["Admin:Password"];
    if (!string.IsNullOrWhiteSpace(adminPassword))
    {
        if (!app.Environment.IsDevelopment() && adminPassword.Length < 8)
        {
            throw new InvalidOperationException("La contraseña configurada en Admin:Password debe tener al menos 8 caracteres en entornos productivos.");
        }

        var adminUser = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == "admin@mercurygo.local");
        if (adminUser != null)
        {
            var hasher = new PasswordHasher<Usuario>();
            adminUser.PasswordHash = hasher.HashPassword(adminUser, adminPassword);
            await db.SaveChangesAsync();
        }
    }
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
