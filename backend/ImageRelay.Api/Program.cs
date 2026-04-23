using System.Text;
using ImageRelay.Api.Configuration;
using ImageRelay.Api.Data;
using ImageRelay.Api.Features.Auth;
using ImageRelay.Api.Features.ClientKeys;
using ImageRelay.Api.Features.Dashboard;
using ImageRelay.Api.Features.Logs;
using ImageRelay.Api.Features.ModelMappings;
using ImageRelay.Api.Features.Proxy;
using ImageRelay.Api.Features.Settings;
using ImageRelay.Api.Features.UpstreamAccounts;
using ImageRelay.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

ApplyEnvironmentOverrides(builder.Configuration);

// ---------- Logging (Serilog -> Console + Seq) ----------
builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .ReadFrom.Services(services)
       .Enrich.FromLogContext()
       .WriteTo.Console();

    var seqUrl = ctx.Configuration["Seq:Url"];
    if (!string.IsNullOrWhiteSpace(seqUrl))
    {
        cfg.WriteTo.Seq(seqUrl);
    }
});

// ---------- Options ----------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<BootstrapOptions>(builder.Configuration.GetSection(BootstrapOptions.SectionName));
builder.Services.Configure<UpstreamOptions>(builder.Configuration.GetSection(UpstreamOptions.SectionName));
builder.Services.Configure<ProxyOptions>(builder.Configuration.GetSection(ProxyOptions.SectionName));

// ---------- Database ----------
var conn = builder.Configuration.GetConnectionString("Default")
           ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn));

// ---------- Auth (JWT) ----------
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
if (string.IsNullOrWhiteSpace(jwt.Secret) || jwt.Secret.Length < 16)
    throw new InvalidOperationException("Jwt:Secret must be configured with at least 16 chars.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

// ---------- HTTP Client ----------
builder.Services.AddHttpClient("upstream", c =>
{
    c.Timeout = TimeSpan.FromMinutes(10);
});

// ---------- Singletons ----------
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<ApiKeyGenerator>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<AccountConcurrencyRegistry>();
builder.Services.AddSingleton<ClientRateLimiter>();
builder.Services.AddSingleton<TokenRefresher>();
builder.Services.AddSingleton<UpstreamForwarder>();

// ---------- Scoped ----------
builder.Services.AddScoped<AccountSelector>();
builder.Services.AddScoped<DbSeeder>();

// ---------- Hosted ----------
builder.Services.AddHostedService<CooldownRecoveryService>();

// ---------- CORS ----------
var adminOrigins = builder.Configuration.GetSection("Cors:AdminOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o =>
{
    o.AddPolicy("admin", p => p
        .WithOrigins(adminOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
    o.AddPolicy("public", p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// ---------- Swagger (Dev only) ----------
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

// ---------- Kestrel: no response buffering for SSE ----------
builder.WebHost.ConfigureKestrel(o =>
{
    o.AllowSynchronousIO = false;
});

var app = builder.Build();

// ---------- Migrate + seed ----------
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync();
}

// ---------- Pipeline ----------
app.UseSerilogRequestLogging();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Apply admin CORS to /api/** via endpoint metadata
var apiCors = app.MapGroup("").RequireCors("admin");
apiCors.MapAuth();
apiCors.MapUpstreamAccounts();
apiCors.MapClientKeys();
apiCors.MapModelMappings();
apiCors.MapLogs();
apiCors.MapDashboard();
apiCors.MapSettings();

// Proxy route under /v1 — permissive CORS so any client can call.
var publicGroup = app.MapGroup("").RequireCors("public");
publicGroup.MapProxy();

app.MapGet("/healthz", () => Results.Ok(new { ok = true, ts = DateTime.UtcNow }));

app.Run();

static void ApplyEnvironmentOverrides(IConfiguration cfg)
{
    // Copy well-known env vars into configuration keys if present.
    Map("DATABASE_URL", "ConnectionStrings:Default");
    Map("JWT_SECRET", "Jwt:Secret");
    Map("BOOTSTRAP_ADMIN_USERNAME", "Bootstrap:AdminUsername");
    Map("BOOTSTRAP_ADMIN_PASSWORD", "Bootstrap:AdminPassword");
    Map("SEQ_URL", "Seq:Url");
    Map("UPSTREAM_BASE_URL", "Upstream:BaseUrl");
    Map("UPSTREAM_TOKEN_URL", "Upstream:TokenUrl");
    Map("UPSTREAM_TOKEN_CLIENT_ID", "Upstream:TokenClientId");
    Map("PROXY_MAX_RETRIES", "Proxy:MaxRetries");
    Map("PROXY_COOLING_MINUTES", "Proxy:CoolingMinutes");
    Map("PROXY_ACCOUNT_CONCURRENCY", "Proxy:AccountConcurrency");

    void Map(string envKey, string cfgKey)
    {
        var val = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(val)) cfg[cfgKey] = val;
    }
}
