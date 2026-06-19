using DinkToPdf;
using DinkToPdf.Contracts;
using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Models;
using eInvWorld.Models.Settings;
using eInvWorld.Services;
using eInvWorld.Services.Background;
using eInvWorld.Services.Extensions;
using eInvWorld.Services.Logging;
using EINVWORLD.Data;
using EINVWORLD.Helpers;
using EINVWORLD.Models.Settings;
using EINVWORLD.Services;
using EINVWORLD.Services.Background;
using EINVWORLD.Services.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NToastNotify;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// NOTE: WebApplication.CreateBuilder already loads, in this precedence order (later wins):
//   appsettings.json  ->  appsettings.{Environment}.json  ->  user-secrets (Development only)  ->  environment variables.
// We re-assert env vars + user-secrets LAST so that secrets supplied out-of-band always override
// any placeholder left in appsettings.json. Secrets (DB passwords, LHDN client secrets, cert pass,
// SMTP, Turnstile) live in user-secrets (dev) or environment variables (server) — NOT in appsettings.json.
// See SECRETS-SETUP.md.
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly(), optional: true);
}
builder.Configuration.AddEnvironmentVariables();

// 🧩 Load the native wkhtmltox library ONLY when the DinkToPdf engine is selected (the default).
// With the Puppeteer engine the native DLL is not needed and may be absent, so loading it
// unconditionally would crash startup. Done after configuration is available so the engine is known.
var pdfEngine = builder.Configuration["PDFGenerationSettings:Engine"] ?? "DinkToPdf";
if (string.Equals(pdfEngine, "DinkToPdf", StringComparison.OrdinalIgnoreCase))
{
    var loadContext = new CustomAssemblyLoadContext();
    var wkhtmlPath = Path.Combine(Directory.GetCurrentDirectory(), "wkhtmltox", "libwkhtmltox.dll");
    loadContext.LoadUnmanagedLibrary(wkhtmlPath);
}


// Configure Serilog to read from appsettings.json
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

// Use Serilog for logging
builder.Host.UseSerilog();

// Configure Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var dbCommandTimeout = builder.Configuration.GetValue<int>("DatabaseSettings:CommandTimeoutSeconds", 180);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        sqlOptions.CommandTimeout(dbCommandTimeout);
    }));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDbContext<WebsiteDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("WebsiteDb")));


// Identity services
var lockoutSettings = builder.Configuration
    .GetSection("IdentityLockout")
    .Get<IdentityLockoutSettings>()
    ?? throw new InvalidOperationException("Missing configuration: IdentityLockout");

builder.Services.Configure<IdentityOptions>(options =>
{
    options.Lockout.AllowedForNewUsers = lockoutSettings.AllowedForNewUsers;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(lockoutSettings.DefaultLockoutMinutes);
    options.Lockout.MaxFailedAccessAttempts = lockoutSettings.MaxFailedAccessAttempts;
});


builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
       .AddRoles<IdentityRole>()  // Adding roles
       .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddSingleton<EINVWORLD.Services.Background.IBackgroundTaskQueue, EINVWORLD.Services.Background.BackgroundTaskQueue>();
builder.Services.AddHostedService<EINVWORLD.Services.Background.QueuedHostedService>();
builder.Services.AddScoped<EINVWORLD.Services.Background.ISyncJobTracker, EINVWORLD.Services.Background.SyncJobTracker>();


// 🔐 Data Protection (Persist Keys)
// IMPORTANT: keep the key ring OUTSIDE the deployable App folder, otherwise a redeploy that clears
// App/ wipes the keys → every existing session cookie and antiforgery token becomes undecryptable
// ("The key {...} was not found in the key ring"), logging all users out and breaking session-based
// flows (e.g. the submission TIN stored in session). Configure DataProtection:KeyRingPath to a stable
// location such as D:\EINVWORLD\Keys; falls back to an in-app folder only if not configured.
var dataProtectionDir = builder.Configuration["DataProtection:KeyRingPath"];
if (string.IsNullOrWhiteSpace(dataProtectionDir))
    dataProtectionDir = Path.Combine(Directory.GetCurrentDirectory(), "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionDir); // Ensure it exists
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionDir))
    .SetApplicationName("eInvWorld");


// Core services
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
// Razor Pages
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToPage("/");
    options.Conventions.AllowAnonymousToPage("/about");
    options.Conventions.AllowAnonymousToPage("/ourservices");
    options.Conventions.AllowAnonymousToPage("/contact");
    options.Conventions.AllowAnonymousToPage("/term-and-condition");
    options.Conventions.AllowAnonymousToPage("/privacy");
    options.Conventions.AllowAnonymousToPage("/customer-info/submit");
    options.Conventions.AllowAnonymousToPage("/Resources/Index");
    options.Conventions.AllowAnonymousToPage("/Resources/Article");


    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Login");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Register");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ForgotPassword");

});

builder.Services.Configure<GoogleAnalyticsSettings>(
    builder.Configuration.GetSection("GoogleAnalytics"));



builder.Services.AddRazorPages();
builder.Services.AddControllers(); // Add API Controllers support
builder.Services.AddMemoryCache(); // Backs the LHDN token cache (TokenService)

// Health checks (DB connectivity) — exposed at /health for uptime monitoring on the in-house server.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>(name: "database");
builder.Services.AddScoped<IStatusMappingService, StatusMappingService>();
builder.Services.AddScoped<GlobalThemeService>();
builder.Services.AddHostedService<InvoiceStatusUpdater>();
//builder.Services.AddSingleton<InvoiceStatusUpdater>();
builder.Services.AddHostedService<eInvWorld.Services.Background.LogCleanupService>();
builder.Services.AddSingleton<QRCodeGeneratorService>();
builder.Services.AddScoped<IRazorViewToStringRenderer, RazorViewToStringRenderer>();

// PDF rendering: DinkToPdf (default) or Puppeteer, selected by PDFGenerationSettings:Engine in appsettings.
builder.Services.AddSingleton<IConverter>(new SynchronizedConverter(new PdfTools()));
builder.Services.AddScoped<eInvWorld.Services.PdfRendering.DinkToPdfRenderer>();
builder.Services.AddScoped<eInvWorld.Services.PdfRendering.PuppeteerPdfRenderer>();
var pdfEngine = builder.Configuration["PDFGenerationSettings:Engine"] ?? "DinkToPdf";
builder.Services.AddScoped<eInvWorld.Services.PdfRendering.IPdfRenderer>(sp =>
    pdfEngine.Equals("Puppeteer", StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<eInvWorld.Services.PdfRendering.PuppeteerPdfRenderer>()
        : sp.GetRequiredService<eInvWorld.Services.PdfRendering.DinkToPdfRenderer>());
builder.Services.AddScoped<PDFGeneratorService>();
builder.Services.AddScoped<IPdfGeneratorService>(sp => sp.GetRequiredService<PDFGeneratorService>());
builder.Services.AddScoped<InvoicePdfMapper>();

builder.Services.AddScoped<DropdownHelper>();
builder.Services.Configure<PDFGenerationSettings>(
    builder.Configuration.GetSection("PDFGenerationSettings"));
builder.Services.Configure<InvoiceStatusUpdaterSettings>(
    builder.Configuration.GetSection("InvoiceStatusUpdaterSettings"));
builder.Services.AddScoped<JsonFileService, JsonFileService>();
builder.Services.AddScoped<IJsonFileService>(sp => sp.GetRequiredService<JsonFileService>());
builder.Services.AddScoped<InvoiceHistoryService>();
builder.Services.AddHttpContextAccessor(); // Needed for IHttpContextAccessor
builder.Services.AddScoped<InvoiceDraftService>();
builder.Services.AddScoped<InvoiceTemplateService>();
builder.Services.AddScoped<DashboardDataService>();
//builder.Services.AddSingleton<InvoiceFinalizerService>();
builder.Services.AddHostedService<TokenRenewalService>();
builder.Services.AddScoped<InvoiceStatusSyncHelper>();
builder.Services.AddScoped<InvoiceSubmissionHelper>();
builder.Services.AddScoped<InvoiceSyncHelper>();
builder.Services.AddScoped<InvoiceFullSyncHelper>();



// ToastNotify
builder.Services.AddRazorPages()
    .AddNToastNotifyToastr(new ToastrOptions()
    {
        ProgressBar = true,
        PositionClass = ToastPositions.TopCenter,
        PreventDuplicates = true,
        CloseButton = true
    });

// Configuration bindings
builder.Services.Configure<FilePathConfig>(builder.Configuration.GetSection("FilePathConfig"));
builder.Services.PostConfigure<FilePathConfig>(config =>
{
    // Resolve relative paths against the content root so the app works on any machine
    // without requiring absolute paths in config. Absolute paths (existing deployments) pass through unchanged.
    var contentRoot = builder.Environment.ContentRootPath;
    static string Resolve(string path, string root) =>
        string.IsNullOrWhiteSpace(path) || System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(root, path);

    config.BasePath = Resolve(config.BasePath, contentRoot);
    config.DraftFolder = Resolve(config.DraftFolder, contentRoot);
    config.SubmittedFolder = Resolve(config.SubmittedFolder, contentRoot);
    config.ValidFolder = Resolve(config.ValidFolder, contentRoot);
    config.InvalidFolder = Resolve(config.InvalidFolder, contentRoot);
    config.CancelledFolder = Resolve(config.CancelledFolder, contentRoot);
    config.InvoiceCounterFilePath = Resolve(config.InvoiceCounterFilePath, contentRoot);
    config.GeneratedPdfFolder = Resolve(config.GeneratedPdfFolder, contentRoot);
    config.ResourceImagesFolder = Resolve(config.ResourceImagesFolder, contentRoot);
    config.EditorUploadsFolder = Resolve(config.EditorUploadsFolder, contentRoot);
    config.CompanyLogosFolder = Resolve(config.CompanyLogosFolder, contentRoot);
});
builder.Services.Configure<EmailConfiguration>(builder.Configuration.GetSection("EmailConfiguration"));
builder.Services.Configure<EmailBaseUrls>(builder.Configuration.GetSection("EmailConfiguration:EmailBaseUrls"));

builder.Services.AddSingleton<EmailConfiguration>(sp =>
    builder.Configuration.GetSection("EmailConfiguration").Get<EmailConfiguration>()
        ?? throw new InvalidOperationException("Missing configuration: EmailConfiguration"));

// Email & Background Services
builder.Services.AddTransient<EmailService>();
builder.Services.AddScoped<DataSeeder>();
builder.Services.AddScoped<RoleSeeder>();
builder.Services.AddScoped<IBuyerService, BuyerService>();
builder.Services.AddScoped<InvoiceService>(); // Scoped: it now uses the (scoped) ApplicationDbContext
builder.Services.AddScoped<EInvoiceNotificationService>();
builder.Services.AddScoped<IEInvoiceNotificationService>(sp => sp.GetRequiredService<EInvoiceNotificationService>());
builder.Services.AddHostedService<InvoiceFinalizerService>();
builder.Services.AddScoped<DashboardDataService>();
builder.Services.AddHostedService<eInvWorld.Services.Background.RecurringInvoiceWorker>();

// ── LHDN/MyInvois HTTP clients + client-side rate limiting ──────────────────────────
// One unified rate-limit handler (LhdnRateLimitHandler) throttles EVERY LHDN endpoint
// (token, validate, submit, poll, search, get-document, cancel/reject) below the official
// per-API limits. It is attached to BOTH the LHDNApiService client and the TokenService
// client so token acquisition is rate-limited too.
var baseUrl = builder.Configuration["LHDNApiConfig:BaseUrl"];
if (string.IsNullOrWhiteSpace(baseUrl))
{
    Log.Warning("LHDN API BaseUrl configuration is missing.");
}

builder.Services.AddTransient<EINVWORLD.Services.LhdnRateLimitHandler>();

// Single consolidated registration for the LHDNApiService typed client (was previously
// registered twice with conflicting config).
builder.Services.AddHttpClient<LHDNApiService>(client =>
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
            client.BaseAddress = new Uri(baseUrl);
    })
    .AddHttpMessageHandler<EINVWORLD.Services.LhdnRateLimitHandler>()
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        // Avoids stale-DNS issues in long-running apps
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    });

// Expose the typed client behind its interface (DIP) so consumers can depend on ILHDNApiService and
// it can be mocked in tests. Forwards to the concrete typed-client registration above, so existing
// GetRequiredService<LHDNApiService>() resolutions keep working too.
builder.Services.AddScoped<ILHDNApiService>(sp => sp.GetRequiredService<LHDNApiService>());

// TokenService now uses a typed HttpClient so /connect/token calls go through the limiter too.
builder.Services.AddHttpClient<ITokenService, TokenService>()
    .AddHttpMessageHandler<EINVWORLD.Services.LhdnRateLimitHandler>()
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    });

builder.Services.Configure<TaxpayerValidationSettings>(builder.Configuration.GetSection("TaxpayerValidationSettings"));

// v1.1 document digital signing — bound from LHDNApiConfig (SigningEnabled/DocVersion/CertPath/CertPass).
// Disabled by default; flip LHDNApiConfig:SigningEnabled to true in appsettings to activate (see SECRETS-SETUP.md / §14).
builder.Services.Configure<DigitalSignatureSettings>(builder.Configuration.GetSection("LHDNApiConfig"));
builder.Services.AddScoped<eInvWorld.Services.IDocumentSigningService, eInvWorld.Services.DocumentSigningService>();

// AI E-invoice Assistant (local Ollama LLM — FOSS, on-prem; OFF by default, see appsettings "AIAssistant").
var aiOptions = builder.Configuration.GetSection(EINVWORLD.Services.Assistant.AIAssistantOptions.SectionName)
    .Get<EINVWORLD.Services.Assistant.AIAssistantOptions>() ?? new EINVWORLD.Services.Assistant.AIAssistantOptions();
builder.Services.AddSingleton(aiOptions);
builder.Services.AddHttpClient<EINVWORLD.Services.Assistant.IEInvoiceAssistantService, EINVWORLD.Services.Assistant.EInvoiceAssistantService>(client =>
{
    if (Uri.TryCreate(aiOptions.BaseUrl, UriKind.Absolute, out var baseUri))
        client.BaseAddress = baseUri;
    client.Timeout = TimeSpan.FromSeconds(aiOptions.TimeoutSeconds <= 0 ? 120 : aiOptions.TimeoutSeconds);
});

// Add HttpClient services to the DI container
builder.Services.AddHttpClient();

var sessionSettings = builder.Configuration.GetSection("SessionSettings").Get<SessionSettings>()
    ?? throw new InvalidOperationException("Missing configuration: SessionSettings");
builder.Services.Configure<SessionSettings>(builder.Configuration.GetSection("SessionSettings"));

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(sessionSettings.IdleTimeoutMinutes);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = sessionSettings.CookieName;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Add authentication cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login"; // Specify the login page
    options.LogoutPath = "/Identity/Account/Logout"; // Specify the logout page
    options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionSettings.IdleTimeoutMinutes);
    options.SlidingExpiration = true; // Resets expiration on activity
});

//builder.Services.Configure<RouteOptions>(options =>
//{
//    options.LowercaseUrls = true;
//    options.LowercaseQueryStrings = false; // optional: leave query strings as-is
//});


var app = builder.Build();

// Apply migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    // Apply migrations — gated by config so a production deploy can run migrations as a separate,
    // controlled step (set DatabaseSettings:AutoMigrateOnStartup=false and run `dotnet ef database update`).
    // Defaults to true to preserve existing behaviour.
    var context = services.GetRequiredService<ApplicationDbContext>();
    if (app.Configuration.GetValue<bool>("DatabaseSettings:AutoMigrateOnStartup", true))
    {
        context.Database.Migrate();
        Log.Information("✅ Database migrations applied on startup.");
    }
    else
    {
        Log.Information("⏭️ Skipping startup migrations (DatabaseSettings:AutoMigrateOnStartup=false). Run migrations as a deploy step.");
    }

    // Seed data codes
    var seeder = services.GetRequiredService<DataSeeder>(); // Resolve DataSeeder from the service provider
    await seeder.SeedDataAsync(); // Seed the data codes

    var roleSeeder = services.GetRequiredService<RoleSeeder>();
    await roleSeeder.SeedRolesAndAdminAsync(); // Seed roles

    // ✅ Auto-create folders from FilePathConfig
    var filePathConfig = builder.Configuration.GetSection("FilePathConfig").Get<FilePathConfig>()
        ?? throw new InvalidOperationException("Missing configuration: FilePathConfig");
    var foldersToEnsure = new[]
    {
        filePathConfig.BasePath,
        filePathConfig.DraftFolder,
        filePathConfig.SubmittedFolder,
        filePathConfig.ValidFolder,
        filePathConfig.InvalidFolder,
        filePathConfig.CancelledFolder,
        filePathConfig.InvoiceCounterFilePath,
        filePathConfig.GeneratedPdfFolder
    };
    foreach (var folder in foldersToEnsure)
    {
        if (!string.IsNullOrWhiteSpace(folder))
        {
            try
            {
                Directory.CreateDirectory(folder);
                Log.Information("📁 Ensured folder exists: {Folder}", folder);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to create folder: {Folder}", folder);
            }
        }
    }
}



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Security response headers — applied to ALL responses (placed before static files).
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["X-Permitted-Cross-Domain-Policies"] = "none";

    // Content-Security-Policy is shipped in REPORT-ONLY mode first: it never blocks, only reports
    // violations, so it cannot break the (CDN-heavy, inline-script) UI. It documents the current
    // allowed sources and gives a baseline to tighten — once the CDN assets are localized and inline
    // scripts removed, drop the CDN hosts / 'unsafe-*' tokens and promote this to the enforcing
    // "Content-Security-Policy" header.
    headers["Content-Security-Policy-Report-Only"] = string.Join("; ", new[]
    {
        "default-src 'self'",
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://code.jquery.com https://cdn.datatables.net https://cdn.tiny.cloud https://www.googletagmanager.com https://www.google-analytics.com https://challenges.cloudflare.com",
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://cdn.datatables.net https://fonts.googleapis.com",
        "font-src 'self' data: https://cdnjs.cloudflare.com https://fonts.gstatic.com",
        "img-src 'self' data: https:",
        "connect-src 'self' https://www.google-analytics.com https://cdn.tiny.cloud",
        "frame-src https://challenges.cloudflare.com",
        "object-src 'none'",
        "base-uri 'self'",
        "frame-ancestors 'self'"
    });

    await next();
});

// NOTE: the previous "/documents" auth middleware and the static-file "Documents" substring 403
// check were removed — the Documents/ folder is outside wwwroot and is NOT web-served. Protected
// documents (PDFs, history) are delivered by authenticated, IDOR-guarded page actions
// (OnGetDownloadPdfAsync / OnGetExportHistoryAsync), so the static-file guard protected nothing.

app.UseHttpsRedirection();
app.UseStaticFiles(); // Serves static files from wwwroot
app.UseRouting();
app.Use(async (context, next) =>
{
    // no-store on dynamic pages (sensitive financial data). Skip the health endpoint so monitors
    // get a clean response. Static files are served earlier and never reach this middleware.
    if (!context.Request.Path.StartsWithSegments("/health"))
    {
        context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Expires"] = "0";
    }
    await next();
});

app.UseSession(); // Add this line to enable session management
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<eInvWorld.Services.Middleware.UserContextMiddleware>();

// Map API Controllers (for ThemeController and other API endpoints)
app.MapControllers();

// Liveness/DB health endpoint for uptime monitoring (anonymous).
app.MapHealthChecks("/health").AllowAnonymous();

app.MapRazorPages();
app.Run();
