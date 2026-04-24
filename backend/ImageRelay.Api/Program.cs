using ImageRelay.Api.Features.Auth;
using ImageRelay.Api.Features.ClientKeys;
using ImageRelay.Api.Features.Dashboard;
using ImageRelay.Api.Features.Logs;
using ImageRelay.Api.Features.ModelMappings;
using ImageRelay.Api.Features.Proxy;
using ImageRelay.Api.Features.Settings;
using ImageRelay.Api.Features.UpstreamAccounts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
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
builder.Services.AddSingleton<AccountConnectivityStatusUpdater>();
builder.Services.AddSingleton<AccountConnectivityTester>();

// ---------- Scoped ----------
builder.Services.AddScoped<AccountSelector>();
builder.Services.AddScoped<DbSeeder>();

// ---------- Hosted ----------
builder.Services.AddHostedService<CooldownRecoveryService>();

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
var spaRoot = ResolveSpaRoot(app.Environment);
var spaAssetsAvailable = Directory.Exists(spaRoot);
var spaIndexPath = Path.Combine(spaRoot, "index.html");
var spaFiles = spaAssetsAvailable ? new PhysicalFileProvider(spaRoot) : null;

// ---------- Migrate + seed ----------
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync();
}

// ---------- Pipeline ----------
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (spaFiles is not null)
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = spaFiles
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = spaFiles
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAuth();
app.MapUpstreamAccounts();
app.MapClientKeys();
app.MapModelMappings();
app.MapLogs();
app.MapDashboard();
app.MapSettings();
app.MapProxy();

app.MapGet("/healthz", () => Results.Ok(new { ok = true, ts = DateTime.UtcNow }));
app.MapFallback(async context =>
{
    if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    if (IsReservedPath(context.Request.Path) || !File.Exists(spaIndexPath))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(spaIndexPath);
});

app.Run();

static string ResolveSpaRoot(IWebHostEnvironment env)
{
    var repoDist = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "frontend", "dist"));
    if (Directory.Exists(repoDist))
    {
        return repoDist;
    }

    return env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
}

static bool IsReservedPath(PathString path)
{
    return path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/v1", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);
}

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
