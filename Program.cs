using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LaundryHub2._0.Data;
using LaundryHub2._0.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient<LaundryHub2._0.Services.PayMongoService>();
builder.Services.AddScoped<LaundryHub2._0.Services.INotificationSender, LaundryHub2._0.Services.DatabaseNotificationSender>();
builder.Services.AddScoped<LaundryHub2._0.Services.NotificationService>();
builder.Services.AddScoped<LaundryHub2._0.Services.OrderNumberService>();
builder.Services.AddScoped<LaundryHub2._0.Services.LoyaltyService>();

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
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;

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
});

// Cookie settings for authentication & authorization
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
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

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
