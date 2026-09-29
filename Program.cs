using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LaundryHub2._0.Data;
using LaundryHub2._0.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient<LaundryHub2._0.Services.PayMongoService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<LaundryHub2._0.Services.SemaphoreSmsSender>();
builder.Services.AddScoped<LaundryHub2._0.Services.SesEmailSender>();
builder.Services.AddScoped<LaundryHub2._0.Services.INotificationSender, LaundryHub2._0.Services.DatabaseNotificationSender>();
builder.Services.AddScoped<LaundryHub2._0.Services.NotificationService>();
builder.Services.AddScoped<LaundryHub2._0.Services.OrderNumberService>();
builder.Services.AddScoped<LaundryHub2._0.Services.LoyaltyService>();
builder.Services.AddScoped<LaundryHub2._0.Services.PaymentDeadlineBackfillService>();
builder.Services.AddHostedService<LaundryHub2._0.Services.OrderAbandonmentBackgroundService>();
builder.Services.Configure<LaundryHub2._0.Models.MapSettings>(builder.Configuration.GetSection("MapSettings"));
builder.Services.AddSignalR();

// Database Configuration - MySQL via MySql.EntityFrameworkCore (.NET 10 compatible)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Port=3306;Database=laundryhub_db;User=root;Password=;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseMySQL(connectionString);
});

// ASP.NET Core Identity Configuration
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Tiered action gating — named policies shared by controllers and views.
// Staff is excluded from all seven; Managers equal Admins except Refunds,
// LoyaltyAdjust, Admin-role assignment, and suspend/archive/demote of Admins
// (the last two are enforced inline in AdminController, not by role alone).
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("UserManagement", policy => policy.RequireRole("Admin", "Manager"));
    options.AddPolicy("Refunds", policy => policy.RequireRole("Admin"));
    options.AddPolicy("LoyaltyAdjust", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RiderReassign", policy => policy.RequireRole("Admin", "Manager"));
    options.AddPolicy("ClaimsResolve", policy => policy.RequireRole("Admin", "Manager"));
    options.AddPolicy("RevenueView", policy => policy.RequireRole("Admin", "Manager"));
    options.AddPolicy("AuditView", policy => policy.RequireRole("Admin", "Manager"));
    // Inventory catalog management (add/edit/archive/delete items).
    // Stock adjustments (AdjustInventoryStock) remain accessible to Staff for day-to-day operations.
    options.AddPolicy("InventoryManagement", policy => policy.RequireRole("Admin", "Manager"));
});

// Cookie settings for authentication & authorization
// SecurePolicy is environment-aware:
//   Development  → SameAsRequest  (HTTP localhost works without SSL)
//   Production   → Always         (HTTPS enforced; cookies never sent over plain HTTP)
var cookieSecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    // Policy-denied Admin mutations are POSTs made via fetch: answer with a
    // plain 403 (matching the previous explicit 403s) instead of redirecting
    // to the access-denied page, so callers can tell denied from allowed.
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (HttpMethods.IsPost(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.HttpOnly = true;
});

// Built-in Rate Limiting configuration
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";

        var isJson = context.HttpContext.Request.Headers.Accept.ToString().Contains("application/json") ||
                     context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        if (isJson)
        {
            context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
            await context.HttpContext.Response.WriteAsync("{\"success\":false,\"message\":\"Too many requests. Please try again in 1 minute.\"}", token);
        }
        else
        {
            context.HttpContext.Response.ContentType = "text/html; charset=utf-8";
            await context.HttpContext.Response.WriteAsync("<!DOCTYPE html><html><head><title>Too Many Requests</title><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><style>body{font-family:sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0;background:#f8fafc;color:#1e293b;}div{text-align:center;max-width:480px;padding:2rem;background:#fff;border-radius:12px;box-shadow:0 4px 6px -1px rgba(0,0,0,0.1);}</style></head><body><div><h2>Too Many Requests</h2><p>You have made too many requests in a short period. Please wait a minute before trying again.</p><p><a href=\"javascript:location.reload()\">Try Again</a></p></div></body></html>", token);
        }
    };

    // Stricter limiter for authentication endpoints: 10 requests per minute per IP
    options.AddPolicy("auth-policy", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            $"auth_{ip}",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Moderate limiter for sensitive transactional endpoints (booking, payment initiation): 30 requests per minute per IP
    options.AddPolicy("action-policy", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            $"action_{ip}",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Dedicated rate limiter for periodic Rider GPS location updates:
    // Partitioned by authenticated rider identity (or IP fallback).
    // Allows 30 updates per minute per rider (1 update every 2 seconds headroom),
    // cleanly accommodating standard 5-10 second GPS client intervals.
    options.AddPolicy("rider-gps-policy", httpContext =>
    {
        var riderId = httpContext.User?.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? httpContext.User.Identity?.Name ?? "auth_rider"
            : (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            $"rider_gps_{riderId}",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

var app = builder.Build();

// Automatically ensure database and tables exist and seed roles/admin
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        // Use EnsureCreated to create DB/tables from model — MigrateAsync is blocked:
        // MySql.EntityFrameworkCore 10.0.9 throws InvalidCastException reading a missing
        // __EFMigrationsHistory table, so fresh-database boot cannot migrate yet.
        // Migration 20260928051254_AddInventoryAndPaymentTracking plus the current model
        // snapshot are kept dormant for the provider fix; no MigrateAsync runs anywhere.
        await context.Database.EnsureCreatedAsync();
        await DbInitializer.InitializeAsync(services);
        logger.LogInformation("Database and roles successfully verified/seeded.");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not initialize MySQL database automatically. Ensure MySQL is running on XAMPP (port 3306).");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Only redirect to HTTPS in non-development environments.
// In Development, the app runs on plain HTTP (localhost:5242) and HTTPS redirection
// would break the local dev server without a certificate.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Security Headers Middleware
app.Use(async (context, next) =>
{
    // Prevent MIME-type sniffing
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

    // Clickjacking protection: allow framing by same origin if required by modern browser flows
    context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");

    // Referrer policy
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

    // Permissions policy: allow camera and geolocation for same origin, disable unused device capabilities
    context.Response.Headers.Append("Permissions-Policy", "camera=(self), microphone=(), geolocation=(self), payment=(self)");

    // Content Security Policy:
    // Allows local scripts and styles, inline script/style needed for existing Razor components/charts/table-manager,
    // Google Fonts (fonts.googleapis.com, fonts.gstatic.com), data: and blob: URIs for SVG/images and MapLibre workers,
    // external tile servers (https:), and external redirect/connect targets for PayMongo (api.paymongo.com).
    // form-action explicitly allows PayMongo's hosted checkout hosts: without
    // them the browser blocks the POST-redirect navigation from PayOrder to the
    // checkout_url (e.g. https://checkout.paymongo.com/...) and the customer
    // never sees the payment form. Server-to-PayMongo API calls are unaffected
    // by CSP; this only governs the customer's browser navigation.
    var csp = "default-src 'self'; " +
              "script-src 'self' 'unsafe-inline'; " +
              "worker-src 'self' blob:; " +
              "child-src 'self' blob:; " +
              "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
              "font-src 'self' https://fonts.gstatic.com; " +
              "img-src 'self' data: blob: https:; " +
              "connect-src 'self' https: https://api.paymongo.com; " +
              "frame-ancestors 'self'; " +
              "form-action 'self' https://pm.link https://paymongo.com https://*.paymongo.com; " +
              "base-uri 'self';";

    context.Response.Headers.Append("Content-Security-Policy", csp);

    await next();
});

app.UseRouting();

// Rate limiter placed after routing and before authentication
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();

app.MapHub<LaundryHub2._0.Hubs.OrderHub>("/orderHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
