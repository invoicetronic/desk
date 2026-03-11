using System.Globalization;
using Desk;
using Desk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection.KeyManagement", LogLevel.Error);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

builder.Configuration.AddYamlFile("desk.yml", optional: true, reloadOnChange: false);

if (!File.Exists(Path.Combine(builder.Environment.ContentRootPath, "desk.yml")))
    Console.WriteLine("info: desk.yml not found. To customize settings: cp desk.yml.example desk.yml");

var config = new DeskConfig();
builder.Configuration.GetSection("desk").Bind(config);
builder.Services.AddSingleton(config);

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

builder.Services.AddScoped<StripeService>();
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

if (!app.Services.GetRequiredService<DeskConfig>().IsStandalone)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DeskDbContext>();

    // Ensure the parent directory exists for SQLite database files
    if (config.Database.Provider is not "pgsql")
    {
        var dbConnString = db.Database.GetConnectionString();
        if (dbConnString is not null)
        {
            var builder2 = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(dbConnString);
            if (!string.IsNullOrEmpty(builder2.DataSource) && builder2.DataSource != ":memory:")
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(builder2.DataSource));
                if (dir is not null) Directory.CreateDirectory(dir);
            }
        }
    }

    db.Database.EnsureCreated();

    // Add Stripe columns if they don't exist (no EF Core Migrations)
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync();
    using var cmd = conn.CreateCommand();

    string[] extraColumns =
    [
        "StripeCustomerId", "SubscriptionStatus",
        "CompanyName", "TaxId", "Address", "City", "State", "ZipCode",
        "Country", "PecMail", "CodiceDestinatario"
    ];

    if (config.Database.Provider is "pgsql")
    {
        var checks = string.Join("\n", extraColumns.Select(c =>
            $"""
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='AspNetUsers' AND column_name='{c}') THEN
                            ALTER TABLE "AspNetUsers" ADD COLUMN "{c}" TEXT;
                        END IF;
            """));
        cmd.CommandText = $"""
            DO $$
            BEGIN
            {checks}
            END $$;
            """;
    }
    else
    {
        // SQLite: check via PRAGMA table_info
        cmd.CommandText = "PRAGMA table_info(AspNetUsers)";
        var columns = new HashSet<string>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(1));
        }

        var alterStatements = extraColumns
            .Where(c => !columns.Contains(c))
            .Select(c => $"ALTER TABLE AspNetUsers ADD COLUMN {c} TEXT")
            .ToList();

        cmd.CommandText = string.Join("; ", alterStatements);
    }

    if (!string.IsNullOrEmpty(cmd.CommandText))
        await cmd.ExecuteNonQueryAsync();

    // Create DataProtectionKeys table if it doesn't exist
    using var dpCmd = conn.CreateCommand();
    if (config.Database.Provider is "pgsql")
    {
        dpCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS "DataProtectionKeys" (
                "Id" SERIAL PRIMARY KEY,
                "FriendlyName" TEXT,
                "Xml" TEXT
            );
            """;
    }
    else
    {
        dpCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS DataProtectionKeys (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FriendlyName TEXT,
                Xml TEXT
            );
            """;
    }
    await dpCmd.ExecuteNonQueryAsync();

    // Encrypt plaintext API keys
    var protector = scope.ServiceProvider.GetRequiredService<ApiKeyProtector>();
    using var readCmd = conn.CreateCommand();
    readCmd.CommandText = config.Database.Provider is "pgsql"
        ? """SELECT "Id", "ApiKey" FROM "AspNetUsers" WHERE "ApiKey" IS NOT NULL"""
        : "SELECT Id, ApiKey FROM AspNetUsers WHERE ApiKey IS NOT NULL";

    var toEncrypt = new List<(string id, string key)>();
    using (var reader = await readCmd.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            var key = reader.GetString(1);
            if (!protector.IsEncrypted(key))
                toEncrypt.Add((id, key));
        }
    }

    if (toEncrypt.Count > 0)
    {
        using var tx = await conn.BeginTransactionAsync();
        foreach (var (id, key) in toEncrypt)
        {
            using var updateCmd = conn.CreateCommand();
            updateCmd.Transaction = (System.Data.Common.DbTransaction)tx;
            updateCmd.CommandText = config.Database.Provider is "pgsql"
                ? """UPDATE "AspNetUsers" SET "ApiKey" = @key WHERE "Id" = @id"""
                : "UPDATE AspNetUsers SET ApiKey = @key WHERE Id = @id";

            var pKey = updateCmd.CreateParameter();
            pKey.ParameterName = "@key";
            pKey.Value = protector.Protect(key);
            updateCmd.Parameters.Add(pKey);

            var pId = updateCmd.CreateParameter();
            pId.ParameterName = "@id";
            pId.Value = id;
            updateCmd.Parameters.Add(pId);

            await updateCmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
        Console.WriteLine($"info: Encrypted {toEncrypt.Count} plaintext API key(s).");
    }
}

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

if (app.Services.GetRequiredService<DeskConfig>().IsBillingEnabled)
    app.MapStripeWebhook();

app.Run();

public partial class Program;
