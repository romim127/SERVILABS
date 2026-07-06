using System.Text;
using System.Text.Json.Serialization;
using AppServicios.Api.Data;
using AppServicios.Api.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
var defaultConnection = ResolveConnectionString(builder.Configuration, builder.Environment);
builder.Services.AddDbContext<AppServiciosDbContext>(options =>
    options.UseNpgsql(defaultConnection));
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

// Registrar PushNotificationService
builder.Services.AddSingleton<AppServicios.Api.Services.PushNotificationService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
            return;
        }

        if (allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException("Configura Cors:AllowedOrigins con el dominio productivo antes de iniciar en producción.");
        }

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();

var jwtKey = builder.Configuration["Jwt:Key"] ?? "AppServicios-Dev-Key-2026-Segura-Preview-32CharsMin";
if (!builder.Environment.IsDevelopment() && jwtKey == "AppServicios-Dev-Key-2026-Segura-Preview-32CharsMin")
{
    throw new InvalidOperationException("Configura Jwt:Key con un secreto productivo antes de iniciar en producción.");
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "AppServicios.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "AppServicios.Client";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppServiciosDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    try
    {
        await db.Database.MigrateAsync();
        if (app.Environment.IsDevelopment())
        {
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
            await EnsureDemoAdminAsync(db, passwordHasher);
        }

        var superAdminPasswordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
        await EnsureSuperAdminAsync(db, builder.Configuration, superAdminPasswordHasher, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "No se pudo inicializar la base de datos al arrancar la app.");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

// Health check
app.MapGet("/health", () => new { status = "healthy" });

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

static string ResolveConnectionString(IConfiguration configuration, IHostEnvironment environment)
{
    var configured = configuration.GetConnectionString("DefaultConnection");
    var databaseUrl = configuration["DATABASE_URL"];

    if (!string.IsNullOrWhiteSpace(databaseUrl)
        && (string.IsNullOrWhiteSpace(configured) || (!environment.IsDevelopment() && IsLocalPostgresConnection(configured))))
    {
        return ConvertDatabaseUrlToNpgsql(databaseUrl);
    }

    return configured ?? "Host=localhost;Port=5432;Database=appservicios;Username=postgres;Password=postgres";
}

static bool IsLocalPostgresConnection(string connectionString)
    => connectionString.Contains("Host=localhost", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase);

static string ConvertDatabaseUrlToNpgsql(string databaseUrl)
{
    if (databaseUrl.Contains("Host=", StringComparison.OrdinalIgnoreCase))
    {
        return databaseUrl;
    }

    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.TrimEntries);
    var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Username = username,
        Password = password,
        Database = uri.AbsolutePath.Trim('/'),
        SslMode = SslMode.Require,
        // TrustServerCertificate eliminado: obsoleto en Npgsql >=7
    };

    return builder.ConnectionString;
}

static async Task EnsureDemoAdminAsync(AppServiciosDbContext db, IPasswordHasher<Usuario> passwordHasher)
{
    const string adminEmail = "admin@appservicios.com";
    const string adminPassword = "Admin123!";

    var admin = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == adminEmail);
    if (admin is null)
    {
        db.Usuarios.Add(new Usuario
        {
            Nombre = "Admin AppServicios",
            Email = adminEmail,
            Telefono = "1100000000",
            DNI = "99999999",
            FechaNacimiento = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Rol = "Administrador",
            Activo = true,
            FechaRegistro = DateTime.UtcNow,
            PasswordHash = passwordHasher.HashPassword(null!, adminPassword),
            VerificadoRenaper = true,
            FechaVerificacion = DateTime.UtcNow,
            RecibeNotificaciones = true
        });
    }
    else
    {
        admin.Rol = "Administrador";
        admin.Activo = true;
        admin.PasswordHash = passwordHasher.HashPassword(admin, adminPassword);
        admin.RecibeNotificaciones = true;
    }

    await db.SaveChangesAsync();

    // --- Usuario de prueba: Cliente ---
    const string testClientEmail = "cliente@servilab.com";
    const string testClientPassword = "Cliente123!";
    var testClient = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == testClientEmail);
    if (testClient is null)
    {
        var clienteUsuario = new Usuario
        {
            Nombre = "Carlos Prueba",
            Email = testClientEmail,
            Telefono = "1155667788",
            DNI = "33444555",
            FechaNacimiento = new DateTime(1992, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            Rol = "Cliente",
            Activo = true,
            FechaRegistro = DateTime.UtcNow,
            PasswordHash = passwordHasher.HashPassword(null!, testClientPassword),
            VerificadoRenaper = false,
            RecibeNotificaciones = true
        };
        db.Usuarios.Add(clienteUsuario);
        await db.SaveChangesAsync();
        db.Clientes.Add(new Cliente
        {
            UsuarioId = clienteUsuario.Id,
            Ubicacion = "Buenos Aires, Argentina"
        });
    }

    // --- Usuario de prueba: Profesional ---
    const string testProEmail = "profesional@servilab.com";
    const string testProPassword = "Profesional123!";
    var testPro = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == testProEmail);
    if (testPro is null)
    {
        var proUsuario = new Usuario
        {
            Nombre = "Laura Servicios",
            Email = testProEmail,
            Telefono = "1199887766",
            DNI = "28999111",
            FechaNacimiento = new DateTime(1988, 9, 20, 0, 0, 0, DateTimeKind.Utc),
            Rol = "Profesional",
            Activo = true,
            FechaRegistro = DateTime.UtcNow,
            PasswordHash = passwordHasher.HashPassword(null!, testProPassword),
            VerificadoRenaper = true,
            FechaVerificacion = DateTime.UtcNow,
            RecibeNotificaciones = true
        };
        db.Usuarios.Add(proUsuario);
        await db.SaveChangesAsync();
        db.Profesionales.Add(new Profesional
        {
            UsuarioId = proUsuario.Id,
            AñosExperiencia = 8,
            CalificacionPromedio = 4.7m,
            TarifaBase = 30000,
            RadioAlcanceKm = 20,
            Ubicacion = "Buenos Aires, Argentina"
        });
    }

    await db.SaveChangesAsync();
}

static async Task EnsureSuperAdminAsync(
    AppServiciosDbContext db,
    IConfiguration configuration,
    IPasswordHasher<Usuario> passwordHasher,
    ILogger logger)
{
    var email = configuration["SuperAdmin:Email"]?.Trim();
    var password = configuration["SuperAdmin:Password"];

    if (string.IsNullOrWhiteSpace(email))
    {
        logger.LogWarning("SuperAdmin:Email no está configurado. No se creó Super Admin.");
        return;
    }

    if (string.IsNullOrWhiteSpace(password) || password.Length < 16)
    {
        logger.LogWarning("SuperAdmin:Password no está configurado o tiene menos de 16 caracteres. No se creó Super Admin.");
        return;
    }

    var normalizedEmail = email.ToLowerInvariant();
    var superAdmin = await db.Usuarios.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
    if (superAdmin is null)
    {
        superAdmin = new Usuario
        {
            Nombre = configuration["SuperAdmin:Name"]?.Trim() ?? "Super Admin AppServicios",
            Email = normalizedEmail,
            Telefono = configuration["SuperAdmin:Phone"]?.Trim() ?? "0000000000",
            DNI = configuration["SuperAdmin:Dni"]?.Trim() ?? "00000000",
            FechaNacimiento = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Rol = "Administrador",
            Activo = true,
            FechaRegistro = DateTime.UtcNow,
            VerificadoRenaper = true,
            FechaVerificacion = DateTime.UtcNow,
            RecibeNotificaciones = true
        };
        superAdmin.PasswordHash = passwordHasher.HashPassword(superAdmin, password);
        db.Usuarios.Add(superAdmin);
        await db.SaveChangesAsync();
        logger.LogInformation("Super Admin creado desde configuración.");
        return;
    }

    superAdmin.Nombre = string.IsNullOrWhiteSpace(configuration["SuperAdmin:Name"])
        ? superAdmin.Nombre
        : configuration["SuperAdmin:Name"]!.Trim();
    superAdmin.Rol = "Administrador";
    superAdmin.Activo = true;
    superAdmin.VerificadoRenaper = true;
    superAdmin.FechaVerificacion ??= DateTime.UtcNow;
    superAdmin.PasswordHash = passwordHasher.HashPassword(superAdmin, password);
    await db.SaveChangesAsync();
    logger.LogInformation("Super Admin actualizado desde configuración.");
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
