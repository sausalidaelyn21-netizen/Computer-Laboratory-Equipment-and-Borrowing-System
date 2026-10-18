using System.Threading.RateLimiting;
using ComputerLaboratoryBorrowingSystem.Data;
using ComputerLaboratoryBorrowingSystem.Infrastructure;
using ComputerLaboratoryBorrowingSystem.Models;
using ComputerLaboratoryBorrowingSystem.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------------ Database (MySQL via Pomelo)
// Settings come from User Secrets (dotnet user-secrets list). No credentials are stored in code.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("YOUR_DB_PASSWORD", StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        "The database connection string is not configured. Set ConnectionStrings:DefaultConnection with " +
        "'dotnet user-secrets set' (see README, section 'Configure the connection string').");
}

// The server version is configured (not auto-detected) so the application can start and show a friendly
// error page even if the database is temporarily down. Example: "8.0.46-mysql".
var serverVersion = ServerVersion.Parse(builder.Configuration["Database:ServerVersion"] ?? "8.0.46-mysql");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

// ------------------------------------------------------------------ Authentication: one Administrator, cookie based
var sessionMinutes = Math.Max(5, builder.Configuration.GetValue("Security:SessionTimeoutMinutes", 30));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "CLEMS.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionMinutes);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = AdminSessionValidator.ValidateAsync
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminClaims.AdministratorPolicy, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(AdminClaims.IsAdministrator, "true"));
});

// Brute-force mitigation in addition to account lockout: at most 10 sign-in posts per minute per IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Too many sign-in attempts from this computer. Please wait one minute and try again.", cancellationToken);
    };
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "CLEMS.Antiforgery";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.HttpOnly = true;
});

// Every controller action requires the Administrator unless it is explicitly marked [AllowAnonymous],
// and every unsafe (POST) request must carry a valid anti-forgery token.
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(AdminClaims.IsAdministrator, "true")
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    // Only explicit [Required] attributes make a field mandatory. Display-only string properties on view models
    // (reference numbers, labels) are not posted back and must not fail validation.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher<Administrator>, PasswordHasher<Administrator>>();
builder.Services.AddScoped<ActivityLogService>();
builder.Services.AddScoped<AdminAccountService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<BorrowingService>();
builder.Services.AddScoped<ReturnService>();
builder.Services.AddScoped<MaintenanceService>();
builder.Services.AddScoped<IncidentService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReportService>();

var app = builder.Build();

// ------------------------------------------------------------------ Database check + single administrator creation
// Creates ONLY the one administrator from InitialAdmin settings, and only if none exists. No sample data.
await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration, app.Logger);

// Offline password recovery:  dotnet run -- reset-admin-password
if (AdminCli.IsResetPasswordCommand(args))
{
    Environment.ExitCode = await AdminCli.ResetPasswordAsync(app.Services);
    return;
}

// ------------------------------------------------------------------ HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}");

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

   app.MapControllerRoute(
       name: "default",
       pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();

// Exposed for integration tests (WebApplicationFactory<Program>).
public partial class Program { }