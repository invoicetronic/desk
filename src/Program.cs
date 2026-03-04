using System.Globalization;
using Desk;
using Desk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddYamlFile("desk.yml", optional: true, reloadOnChange: false);

var config = new DeskConfig();
builder.Configuration.GetSection("app").Bind(config);
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
        o.UseSqlite(connString ?? "Data Source=desk.db"));

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

var app = builder.Build();

if (!app.Services.GetRequiredService<DeskConfig>().IsStandalone)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DeskDbContext>();
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
}

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
