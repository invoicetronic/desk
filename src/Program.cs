using System.Globalization;
using Desk;
using Desk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;
using Serilog.Events;

// Bootstrap logger captures errors during host construction.
// The real logger replaces it once DeskConfig is bound.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration.AddYamlFile("desk.yml", optional: true, reloadOnChange: false);

    if (!File.Exists(Path.Combine(builder.Environment.ContentRootPath, "desk.yml")))
        Console.WriteLine("info: desk.yml not found. To customize settings: cp desk.yml.example desk.yml");

    var config = new DeskConfig();
    builder.Configuration.GetSection("desk").Bind(config);
    builder.Services.AddSingleton(config);

    Log.Logger = BuildLogger(builder.Environment, config);
    builder.Services.AddSerilog(Log.Logger, dispose: true);

    builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

    var supportedCultures = new[] { new CultureInfo("it"), new CultureInfo("en") };
    builder.Services.Configure<RequestLocalizationOptions>(options =>
    {
        options.DefaultRequestCulture = new RequestCulture("it");
        options.SupportedCultures = supportedCultures;
        options.SupportedUICultures = supportedCultures;

        if (config.Locale is not null)
        {
            // Locale forced in desk.yml: override browser preference
            var forced = new CultureInfo(config.Locale);
            options.DefaultRequestCulture = new RequestCulture(forced);
            options.RequestCultureProviders.Clear();
        }
    });

    // Always register Identity + DB (in-memory SQLite for standalone).
    // This ensures consistent service registration regardless of mode.
    // Auth behavior is controlled at runtime by DeskAuthHandler.
    var connString = config.IsStandalone
        ? "DataSource=:memory:"
        : config.Database.ConnectionString;

    if (config.Database.Provider is "pgsql" && !config.IsStandalone)
        builder.Services.AddDbContext<DeskDbContext>(o => o.UseNpgsql(connString));
    else
        builder.Services.AddDbContext<DeskDbContext>(o =>
            o.UseSqlite(connString ?? "Data Source=data/desk.db"));

    builder.Services.AddIdentity<DeskUser, IdentityRole>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequireDigit = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
        })
        .AddEntityFrameworkStores<DeskDbContext>()
        .AddDefaultTokenProviders()
        .AddErrorDescriber<LocalizedIdentityErrorDescriber>();

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Identity/Account/Login";
        options.LogoutPath = "/Identity/Account/Logout";
        options.AccessDeniedPath = "/Identity/Account/Login";
    });

    // Runtime authorization: DeskAuthHandler checks DeskConfig.IsStandalone from DI.
    // In standalone → always succeeds. In multi-user → requires authenticated user.
    builder.Services.AddSingleton<IAuthorizationHandler, DeskAuthHandler>();
    builder.Services.AddAuthorization(options =>
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .AddRequirements(new DeskAuthRequirement())
            .Build();
    });

    builder.Services.AddRazorPages()
        .AddViewLocalization()
        .AddDataAnnotationsLocalization(options =>
        {
            options.DataAnnotationLocalizerProvider = (_, factory) =>
                factory.Create(typeof(SharedResource));
        })
        .ConfigureApplicationPartManager(partManager =>
        {
            // Remove Identity.UI compiled Razor pages from the shared framework
            // so our custom pages in Areas/Identity/ take precedence.
            var identityParts = partManager.ApplicationParts
                .Where(p => p.Name.Contains("Identity.UI", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var part in identityParts)
                partManager.ApplicationParts.Remove(part);
        });

    builder.Services.Configure<Microsoft.AspNetCore.Mvc.Razor.RazorViewEngineOptions>(o =>
        o.ViewLocationExpanders.Add(new ThemeViewLocationExpander()));
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });
    builder.Services.AddDistributedMemoryCache();

    builder.Services.AddScoped<SessionManager>();
    builder.Services.AddHttpClient<ApiClient>();
    builder.Services.AddScoped<ApiManager>();

    builder.Services.AddScoped<EmailService>();
    builder.Services.AddSingleton<ApiKeyProtector>();

    builder.Services.AddDataProtection();
    builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
        new ConfigureOptions<KeyManagementOptions>(options =>
        {
            if (!sp.GetRequiredService<DeskConfig>().IsStandalone)
            {
                options.XmlRepository = new EntityFrameworkCoreXmlRepository<DeskDbContext>(
                    sp, sp.GetRequiredService<ILoggerFactory>());
            }
        }));

    var app = builder.Build();

    await DatabaseInitializer.InitializeAsync(app.Services, app.Services.GetRequiredService<DeskConfig>());

    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
            | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
    });

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    // Security headers
    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'";
        await next();
    });

    app.UseStaticFiles();

    var customPath = Path.Combine(app.Environment.ContentRootPath, "custom");
    if (Directory.Exists(customPath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(customPath),
            RequestPath = "/custom"
        });
    }

    app.UseSerilogRequestLogging();

    app.UseRequestLocalization();
    app.UseRouting();
    app.UseSession();

    // Redirect /Identity/* to / in standalone mode (Identity pages not needed)
    app.Use(async (context, next) =>
    {
        var cfg = context.RequestServices.GetRequiredService<DeskConfig>();
        if (cfg.IsStandalone && context.Request.Path.StartsWithSegments("/Identity"))
        {
            context.Response.Redirect("/");
            return;
        }
        await next();
    });

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapRazorPages();
    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Desk terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

static Logger BuildLogger(IHostEnvironment env, DeskConfig config)
{
    const string consoleTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";
    const string fileTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    var minLevel = Enum.TryParse<LogEventLevel>(config.Logging.MinLevel, ignoreCase: true, out var parsed)
        ? parsed
        : LogEventLevel.Information;

    var lc = new LoggerConfiguration()
        .MinimumLevel.Is(minLevel)
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore.DataProtection.KeyManagement", LogEventLevel.Error)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .Enrich.FromLogContext();

    if (env.IsDevelopment())
    {
        // Dev: console only, full verbosity. No file sink (avoids littering test runs).
        lc.WriteTo.Console(outputTemplate: consoleTemplate);
    }
    else
    {
        // Prod: file sink for the bulk; console limited to Warning+ so `docker logs`
        // still surfaces problems without paying the cost of console for every line.
        lc.WriteTo.Console(
            restrictedToMinimumLevel: LogEventLevel.Warning,
            outputTemplate: consoleTemplate);

        if (!string.IsNullOrWhiteSpace(config.Logging.Directory))
        {
            try
            {
                lc.WriteTo.File(
                    path: Path.Combine(config.Logging.Directory, ".log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: config.Logging.RetainedFiles,
                    fileSizeLimitBytes: null,
                    shared: false,
                    outputTemplate: fileTemplate);
            }
            catch (Exception ex)
            {
                // File sink unavailable (perms, missing dir, disk full…).
                // Boot anyway with console-only output so the operator can diagnose.
                Console.Error.WriteLine(
                    $"warning: file logging at '{config.Logging.Directory}' unavailable ({ex.Message}); falling back to console only");
            }
        }
    }

    return lc.CreateLogger();
}

public partial class Program;
