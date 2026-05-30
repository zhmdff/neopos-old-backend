using DAL.Server;
using BusinessLayer;
using Microsoft.OpenApi.Models;
using NeoPos.WebAPI.Helpers;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using DAL.Server.Context;
using DAL.Server.SchemaPatches;
using DAL.Server.Service;
using Microsoft.EntityFrameworkCore;

try
{
    Console.WriteLine(">>> NeoPos Backend Starting...");

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        WebRootPath = "wwwroot"
    });

    Console.WriteLine(">>> Configuring Services...");
    // Culture ayarları
    var cultureInfo = new CultureInfo("en-US");
    cultureInfo.NumberFormat.NumberDecimalSeparator = ".";
    CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
    CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

    // 1. Servislerin Qeydiyyatı
    builder.Services.AddHttpClient();
    builder.Services.AddHttpClient("NeoPosMediaSync", client =>
    {
        client.Timeout = TimeSpan.FromMinutes(10);
    });
    builder.Services.RegisterDAL(builder.Configuration);
    builder.Services.AddBlServices(builder.Configuration);
    builder.Services.AddScoped<NeoPos.WebAPI.Services.TenantBootstrapService>();
    builder.Services.AddScoped<NeoPos.WebAPI.Services.IMediaSyncService, NeoPos.WebAPI.Services.MediaSyncService>();
    builder.Services.AddHostedService<NeoPos.WebAPI.Services.AutoCashShiftHostedService>();
    builder.Services.AddHostedService<NeoPos.WebAPI.Services.BossTelegramLineDeleteCallbackHostedService>();

    // Register DatabaseSyncService as a singleton and a hosted service (only for tenants/SQLite)
    string mode = Environment.GetEnvironmentVariable("NEOPOS_MODE") 
                  ?? builder.Configuration["NeoPos:Mode"] 
                  ?? "tenant"; // Default is tenant
    bool usePostgresAsPrimary = mode.Equals("master", StringComparison.OrdinalIgnoreCase);
    builder.Services.AddScoped<NeoPos.WebAPI.Services.TenantMasterLoginBootstrapService>();

    if (!usePostgresAsPrimary)
    {
        builder.Services.AddSingleton<NeoPos.WebAPI.Services.DatabaseSyncService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<NeoPos.WebAPI.Services.DatabaseSyncService>());
    }

    // --- KRİTİK ƏLAVƏLƏR ---
    builder.Services.AddHttpContextAccessor(); // Tokeni oxumaq üçün vacibdir
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>(); // ID-ni filtrlərə ötürmək üçün
    Console.WriteLine(">>> Services Configured.");

    // 2. JWT Authentication Qeydiyyatı (Tokeni tanımaq üçün mütləqdir)
    Console.WriteLine(">>> Configuring Authentication...");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]))
            };
            // SignalR WebSocket: brauzer bəzən Authorization ötürmür — ?access_token= ilə uyğunlaşdırırıq.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/notificationHub"))
                        context.Token = accessToken;
                    return Task.CompletedTask;
                }
            };
        });
    // -----------------------

    builder.Services.AddControllers().AddJsonOptions(o =>
    {
        // Telefon / ofisiant JSON-u camelCase göndərir; köhnə konfiqlərlə uyğunluq.
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
    builder.Services.AddSignalR();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "NeoPos API", Version = "v1" });
        c.OperationFilter<SwaggerFileOperationFilter>();

        // Swagger-də "Authorize" düyməsinin çıxması üçün (Test üçün rahatdır)
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "Zəhmət olmasa tokeni daxil edin",
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            BearerFormat = "JWT",
            Scheme = "Bearer"
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement {
            {
                new OpenApiSecurityScheme {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                new string[] { }
            }
        });
    });

    // SignalR + JWT (?access_token=) negotiate sorğusu credentials ilə gəlir; * origin ilə uyğun gəlmir.
    var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
        ?? Array.Empty<string>();
    // "*" + AllowCredentials qadağandır; env/appsettings səhvində startup çökməsin deyə * süzülür.
    var corsOriginsTrimmed = corsOrigins
        .Select(o => o?.Trim())
        .Where(o => !string.IsNullOrEmpty(o))
        .Where(o => !string.Equals(o, "*", StringComparison.Ordinal))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var allowPrivateHttpLanOrigins = builder.Configuration.GetValue("Cors:AllowPrivateHttpLanOrigins", true);
    /** true: istənilən origin (LAN, 192.168.x.x, terminal və s.) — POS üçün praktik default. */
    var allowAllOrigins = builder.Configuration.GetValue("Cors:AllowAllOrigins", true);

    static bool IsPrivateHttpLanOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)) return false;
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
        if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase)) return false;
        if (!IPAddress.TryParse(uri.Host, out var ip)) return false;
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        if (b[0] == 10) return true;
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
        if (b[0] == 192 && b[1] == 168) return true;
        return false;
    }

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("NeoPosCors", policy =>
        {
            if (allowAllOrigins || corsOriginsTrimmed.Length > 0 || allowPrivateHttpLanOrigins)
            {
                policy.SetIsOriginAllowed(origin =>
                {
                    if (allowAllOrigins) return !string.IsNullOrWhiteSpace(origin);
                    if (string.IsNullOrWhiteSpace(origin)) return false;
                    if (corsOriginsTrimmed.Contains(origin, StringComparer.OrdinalIgnoreCase)) return true;
                    return allowPrivateHttpLanOrigins && IsPrivateHttpLanOrigin(origin);
                })
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            }
            else
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            }
        });
    });

    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    Console.WriteLine(">>> Building Application...");
    var app = builder.Build();

    Console.WriteLine(">>> Initializing Database...");
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        if (usePostgresAsPrimary)
        {
            Console.WriteLine(">>> Running Primary Migrations (PostgreSQL using RemoteDb migrations)...");
            var remoteDb = scope.ServiceProvider.GetRequiredService<RemoteDbContext>();
            await remoteDb.Database.MigrateAsync();
        }
        else
        {
            Console.WriteLine(">>> Running Primary Migrations (SQLite)...");
            await db.Database.MigrateAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;");
            // Tenants use SQLite only — no PostgreSQL access at all.
        }

        Console.WriteLine(">>> Starting Tenant Bootstrap...");
        // Initial Bootstrap
        var bootstrapService = scope.ServiceProvider.GetRequiredService<NeoPos.WebAPI.Services.TenantBootstrapService>();
        await bootstrapService.BootstrapAsync();
        
        Console.WriteLine(">>> Applying Schema Patches...");
        await HallTimeDiscountRulesSchemaPatch.ApplyAsync(db);
    }

    Console.WriteLine(">>> Database Ready. Configuring Pipeline...");
    // CORS əvvəl — Swagger, static, API hamısına başlıq getsin (əks halda swagger.json bloklanır).
    app.UseCors("NeoPosCors");

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseStaticFiles();

    // --- SIRALAMA ÇOX VACİBDİR (Dəyişmə!) ---
    app.UseAuthentication(); // 1. Tokeni oxu və useri tanı
    app.UseAuthorization();  // 2. İcazələri yoxla

    app.MapHub<BusinessLayer.Hubs.NotificationHub>("/notificationHub");

    app.MapControllers();
    
    Console.WriteLine(">>> Backend Running on http://localhost:5050");
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine("\n!!! CRITICAL STARTUP ERROR !!!");
    Console.WriteLine(ex.ToString());
    Console.WriteLine("\nApplication will now exit.");
}
