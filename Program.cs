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
using Microsoft.AspNetCore.HttpOverrides;
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
var pdfEngineForNativeLoad = builder.Configuration["PDFGenerationSettings:Engine"] ?? "DinkToPdf";
if (string.Equals(pdfEngineForNativeLoad, "DinkToPdf", StringComparison.OrdinalIgnoreCase))
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

builder.Services.AddScoped<EINVWORLD.Services.Background.ISyncJobTracker, EINVWORLD.Services.Background.SyncJobTracker>();

// Durable background-job processing: handlers (scoped, resolved per job) + the polling worker.
builder.Services.AddScoped<EINVWORLD.Services.Background.ISyncJobHandler, EINVWORLD.Services.Background.StatusSyncJobHandler>();
builder.Services.AddScoped<EINVWORLD.Services.Background.ISyncJobHandler, EINVWORLD.Services.Background.FullImportJobHandler>();
builder.Services.AddScoped<EINVWORLD.Services.Background.ISyncJobHandler, EINVWORLD.Services.Background.SupplierRefreshJobHandler>();
builder.Services.AddScoped<EINVWORLD.Services.Background.ISyncJobHandler, EINVWORLD.Services.Background.SubmitDocumentJobHandler>(); // background retry of a failed interactive submission
builder.Services.AddHostedService<EINVWORLD.Services.Background.DurableSyncJobWorker>();

// Tamper-evident, hash-chained audit trail.
builder.Services.AddScoped<EINVWORLD.Services.Audit.IAuditService, EINVWORLD.Services.Audit.AuditService>();


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

// Reverse-proxy / tunnel support (e.g. Cloudflare Tunnel). When TLS is terminated upstream and the app
// is reached over plain HTTP on a local port, the app must honour the forwarded headers the proxy sends:
//   • X-Forwarded-Proto — so the app knows the original request was HTTPS (correct Secure cookies, HSTS)
//     instead of treating it as http.
//   • X-Forwarded-For   — so the app sees the REAL client IP instead of 127.0.0.1, which the per-IP rate
//     limiter and the audit/log IP enrichment depend on.
// Only headers from a trusted proxy (KnownProxies/KnownNetworks) are honoured, so this is safe to leave
// on. Defaults: enabled, trusting loopback (cloudflared runs on the same host). Add more proxies or tune
// the hop limit via the ForwardedHeaders config section. Disable with ForwardedHeaders:Enabled=false.
var forwardedHeadersEnabled = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled", true);

// HTTPS redirect port. For a DIRECT IIS HTTPS binding the default is 443 (behind IIS the port can't be
// auto-discovered, so it's set explicitly to avoid the "Failed to determine the https port" warning).
// BUT when ForwardedHeaders is enabled — i.e. we've declared "I'm behind a TLS-terminating proxy /
// Cloudflare Tunnel that forwards plain HTTP" — the redirect DEFAULTS OFF, because an in-app HTTP->HTTPS
// redirect would loop (http->https->http) when the edge already terminates TLS. Let the edge enforce HTTPS
// (e.g. Cloudflare "Always Use HTTPS"). An explicit Security:HttpsRedirectPort always wins: set a port to
// force the redirect on, or 0 to force it off.
var configuredHttpsPort = builder.Configuration.GetValue<int?>("Security:HttpsRedirectPort");
var httpsRedirectPort = configuredHttpsPort ?? (forwardedHeadersEnabled ? 0 : 443);
if (httpsRedirectPort > 0)
{
    builder.Services.AddHttpsRedirection(options => options.HttpsPort = httpsRedirectPort);
}

if (forwardedHeadersEnabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;

        // Trust only the immediate proxy. cloudflared connects over loopback by default.
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();
        options.KnownProxies.Add(System.Net.IPAddress.Loopback);     // 127.0.0.1
        options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback); // ::1

        // Extra proxy IPs (e.g. if cloudflared runs on a different host/container).
        foreach (var ip in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
        {
            if (System.Net.IPAddress.TryParse(ip, out var parsed))
                options.KnownProxies.Add(parsed);
        }

        // How many forwarded hops to walk back. Default 1 = the single cloudflared hop.
        options.ForwardLimit = builder.Configuration.GetValue<int?>("ForwardedHeaders:ForwardLimit") ?? 1;
    });
}

// Bound request/upload sizes. Largest legitimate upload is ~10 MB (bulk import / AI document capture);
// a 32 MB ceiling leaves headroom while rejecting oversized bodies early (memory-exhaustion DoS defence).
const long MaxRequestBytes = 32L * 1024 * 1024;
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = MaxRequestBytes;
});
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(o =>
{
    o.Limits.MaxRequestBodySize = MaxRequestBytes;
});
builder.Services.Configure<Microsoft.AspNetCore.Builder.IISServerOptions>(o =>
{
    o.MaxRequestBodySize = MaxRequestBytes; // IIS in-process hosting
});

// Health checks — split into liveness (process alive) and readiness (can do real work).
// "ready"-tagged checks gate /health/ready; /health/live has none (just confirms the process responds).
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>(name: "database", tags: new[] { "ready" })
    .AddCheck<EINVWORLD.Helpers.HealthChecks.WritableFoldersHealthCheck>("writable-folders", tags: new[] { "ready" });
builder.Services.AddScoped<IStatusMappingService, StatusMappingService>();
builder.Services.AddScoped<GlobalThemeService>();
builder.Services.AddHostedService<InvoiceStatusUpdater>();
//builder.Services.AddSingleton<InvoiceStatusUpdater>();
builder.Services.AddHostedService<eInvWorld.Services.Background.LogCleanupService>();
builder.Services.AddHostedService<EINVWORLD.Services.Background.SyncFailureAlertService>(); // emails admin on failed-job backlog (off by default)
builder.Services.AddHostedService<EINVWORLD.Services.Background.CertExpiryAlertService>(); // emails admin as the LHDN signing cert nears expiry (off by default)
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
    })
    // Resilience is applied ONLY to token acquisition (an idempotent OAuth client-credentials call),
    // so transient LHDN/network blips don't fail a whole sync cycle. It is deliberately NOT applied to
    // the document-submission client: a retry after a timed-out POST could create a duplicate document.
    // (Low on-prem traffic keeps the circuit breaker's min-throughput threshold from tripping.)
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60); // must be >= 2x AttemptTimeout
    });

builder.Services.Configure<TaxpayerValidationSettings>(builder.Configuration.GetSection("TaxpayerValidationSettings"));

// v1.1 document digital signing — bound from LHDNApiConfig (SigningEnabled/DocVersion/CertPath/CertPass).
// Disabled by default; flip LHDNApiConfig:SigningEnabled to true in appsettings to activate (see SECRETS-SETUP.md / §14).
builder.Services.Configure<DigitalSignatureSettings>(builder.Configuration.GetSection("LHDNApiConfig"));
// Signing-key custody seam: the signing service resolves its certificate from an ICertificateProvider
// selected by LHDNApiConfig:SigningKeyProvider ("File" default — loads the .p12 from CertPath). A future
// vault/HSM provider (e.g. Azure Key Vault) is an extra registration here + that config value, with no
// change to the signing service. Singleton: the certificate caches process-wide (rotation = swap file +
// iisreset, per the cert-rotation runbook).
builder.Services.AddSingleton<eInvWorld.Services.Signing.ICertificateProvider, eInvWorld.Services.Signing.FileCertificateProvider>();
builder.Services.AddScoped<eInvWorld.Services.IDocumentSigningService, eInvWorld.Services.DocumentSigningService>();

// Provider-agnostic AI (FOSS, on-prem; OFF by default). Business logic depends only on IAiService,
// never on a concrete backend, so OpenAI/Azure/Claude/Gemini can be added as extra IAiProvider
// registrations without touching callers. Configuration lives entirely in the "AI" section.
var aiSettings = builder.Configuration.GetSection(EINVWORLD.Services.AI.AiSettings.SectionName)
    .Get<EINVWORLD.Services.AI.AiSettings>() ?? new EINVWORLD.Services.AI.AiSettings();
builder.Services.AddSingleton(aiSettings);

// Provider transport (typed HttpClient via the factory — no socket exhaustion). Registered as IAiProvider
// so AiService can resolve every provider and select the configured one by name.
builder.Services.AddHttpClient<EINVWORLD.Services.AI.IAiProvider, EINVWORLD.Services.AI.Providers.OllamaAiProvider>(client =>
{
    if (Uri.TryCreate(aiSettings.BaseUrl, UriKind.Absolute, out var baseUri))
        client.BaseAddress = baseUri;
    client.Timeout = TimeSpan.FromSeconds(aiSettings.TimeoutSeconds <= 0 ? 120 : aiSettings.TimeoutSeconds);
});
builder.Services.AddScoped<EINVWORLD.Services.AI.IAiService, EINVWORLD.Services.AI.AiService>();

// E-invoicing domain assistant — owns LHDN prompts/grounding/validation, delegates model calls to IAiService.
builder.Services.AddScoped<EINVWORLD.Services.Assistant.IEInvoiceAssistantService, EINVWORLD.Services.Assistant.EInvoiceAssistantService>();

// AI Document Capture (upload PDF → extract text → reuse the assistant to suggest a reviewed invoice).
// OFF by default; also requires the AI assistant to be enabled.
var docCaptureOptions = builder.Configuration.GetSection(EINVWORLD.Services.DocumentCapture.DocumentCaptureOptions.SectionName)
    .Get<EINVWORLD.Services.DocumentCapture.DocumentCaptureOptions>() ?? new EINVWORLD.Services.DocumentCapture.DocumentCaptureOptions();
builder.Services.AddSingleton(docCaptureOptions);
builder.Services.AddScoped<EINVWORLD.Services.DocumentCapture.IDocumentTextExtractor, EINVWORLD.Services.DocumentCapture.PdfDocumentTextExtractor>();
// OCR fallback for scanned PDFs (Tesseract + PDFium). Inert unless DocumentCapture:OcrEnabled + tessdata.
builder.Services.AddScoped<EINVWORLD.Services.DocumentCapture.IDocumentOcrService, EINVWORLD.Services.DocumentCapture.TesseractDocumentOcrService>();

// Bulk invoice import (validate-only): parse a CSV/XLSX and validate rows against the LHDN reference codes.
builder.Services.AddScoped<EINVWORLD.Services.Import.IBulkInvoiceImportService, EINVWORLD.Services.Import.BulkInvoiceImportService>();

// Watched-folder importer: validates CSV/XLSX dropped into an Inbox and sorts them. OFF by default.
var watchedFolderOptions = builder.Configuration.GetSection(EINVWORLD.Services.Import.WatchedFolderOptions.SectionName)
    .Get<EINVWORLD.Services.Import.WatchedFolderOptions>() ?? new EINVWORLD.Services.Import.WatchedFolderOptions();
builder.Services.AddSingleton(watchedFolderOptions);
builder.Services.AddHostedService<EINVWORLD.Services.Import.WatchedFolderImportWorker>();

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
    // Lax (not Strict): blocks cross-site cookie sends (CSRF defence-in-depth) while still allowing
    // top-level navigations back into the app (e.g. links from email). Strict would break those.
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Add authentication cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login"; // Specify the login page
    options.LogoutPath = "/Identity/Account/Logout"; // Specify the logout page
    options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionSettings.IdleTimeoutMinutes);
    options.SlidingExpiration = true; // Resets expiration on activity
    // CSRF defence-in-depth for the auth cookie. Lax allows top-level navigations back into the app.
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

//builder.Services.Configure<RouteOptions>(options =>
//{
//    options.LowercaseUrls = true;
//    options.LowercaseQueryStrings = false; // optional: leave query strings as-is
//});


// Inbound rate limiting — a per-IP backstop against runaway/abusive traffic (the limit is generous so
// normal multi-user office NAT traffic is never throttled). Health probes are exempt. Tune or disable
// via the "RateLimiting" config section. (Login brute force is already capped by Identity lockout.)
var rateLimitEnabled = builder.Configuration.GetValue("RateLimiting:Enabled", true);
var permitsPerMinute = builder.Configuration.GetValue("RateLimiting:PermitsPerMinute", 1200);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        if (!rateLimitEnabled || httpContext.Request.Path.StartsWithSegments("/health"))
            return System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("exempt");

        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(ip,
            _ => new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute <= 0 ? 1200 : permitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0
            });
    });

    // Stricter per-user policy for admin sync triggers (each enqueues background work). Prevents one admin
    // from flooding the durable job queue. Partitioned by user name so it's independent of the global per-IP
    // limiter. Configurable via RateLimiting:AdminSyncPerMinute (default 10).
    var adminSyncPerMinute = builder.Configuration.GetValue("RateLimiting:AdminSyncPerMinute", 10);
    options.AddPolicy("admin-sync", httpContext =>
    {
        var user = httpContext.User?.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(user,
            _ => new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
            {
                PermitLimit = adminSyncPerMinute <= 0 ? 10 : adminSyncPerMinute,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0
            });
    });
});

var app = builder.Build();

// Fail fast on broken/missing critical config (blank connection string, missing DataProtection key
// ring, signing enabled without a cert, localhost URLs in Production, etc.) so a misconfigured
// deploy stops here with ONE clear message instead of failing vaguely at runtime.
EINVWORLD.Helpers.ProductionConfigValidator.Validate(app.Configuration, app.Environment.IsProduction());

// One-line startup summary so an operator can confirm from the logs exactly what this instance loaded
// (no secrets — just feature/mode flags).
Log.Information(
    "EINVWORLD {Version} starting — Environment={Environment}, PDFEngine={PdfEngine}, AI={AiEnabled}, DocumentCapture={CaptureEnabled}, OCR={OcrEnabled}, AutoMigrate={AutoMigrate}",
    app.Configuration["AppInfo:Version"] ?? "?",
    app.Environment.EnvironmentName,
    app.Configuration["PDFGenerationSettings:Engine"] ?? "DinkToPdf",
    app.Configuration.GetValue("AI:Enabled", false),
    app.Configuration.GetValue("DocumentCapture:Enabled", false),
    app.Configuration.GetValue("DocumentCapture:OcrEnabled", false),
    app.Configuration.GetValue("DatabaseSettings:AutoMigrateOnStartup", false));

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

// MUST run first: rewrite the request scheme (X-Forwarded-Proto) and client IP (X-Forwarded-For) from the
// reverse proxy / Cloudflare Tunnel BEFORE any other middleware reads them — HTTPS redirect, Secure
// cookies, HSTS, the rate limiter and audit/log IP enrichment all depend on seeing the original values.
if (forwardedHeadersEnabled)
{
    app.UseForwardedHeaders();
}

// Assign a correlation id to every request (early, so all of its logs + audit entries share it).
app.UseMiddleware<eInvWorld.Services.Middleware.CorrelationIdMiddleware>();

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
        "frame-ancestors 'self'",
        // Browsers POST violation reports here so the policy can be tightened from real data before
        // it is promoted from Report-Only to enforcing. See CspReportController.
        "report-uri /csp-report"
    });

    await next();
});

// NOTE: the previous "/documents" auth middleware and the static-file "Documents" substring 403
// check were removed — the Documents/ folder is outside wwwroot and is NOT web-served. Protected
// documents (PDFs, history) are delivered by authenticated, IDOR-guarded page actions
// (OnGetDownloadPdfAsync / OnGetExportHistoryAsync), so the static-file guard protected nothing.

// Only redirect to HTTPS when a port is configured/derived (see the HttpsRedirectPort logic above).
// Behind a TLS-terminating proxy/tunnel this is skipped so the request pipeline never tries to redirect.
if (httpsRedirectPort > 0)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles(); // Serves static files from wwwroot
// Structured per-request log (method, path, status, elapsed ms) — one tidy line per request instead of
// the framework's noisy multi-line default. Placed after static files so asset hits don't flood the log.
app.UseSerilogRequestLogging();
app.UseRouting();
app.UseRateLimiter();
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

// Enforce 2FA for the Admin role (block-until-enrolled). Placed after auth so User/roles are populated.
app.UseMiddleware<eInvWorld.Services.Middleware.AdminMfaEnforcementMiddleware>();

// Map API Controllers (for ThemeController and other API endpoints)
app.MapControllers();

// Health endpoints for uptime monitoring (anonymous).
//   /health/live  — process is up (no dependency checks); use for IIS App Initialization / liveness probes.
//   /health/ready — DB reachable + required folders writable; use to gate "is it safe to send traffic".
//   /health       — kept for backward compatibility (runs all checks).
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // no checks — liveness only
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();

app.MapRazorPages();
app.Run();
