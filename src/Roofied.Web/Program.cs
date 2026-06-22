using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using MudBlazor.Services;
using Roofied.Domain.Identity;
using Roofied.Infrastructure;
using Roofied.Infrastructure.Persistence;
using Roofied.Infrastructure.Persistence.Seed;
using Roofied.Web.Components;
using Roofied.Web.Components.Account;
using Roofied.Web.Configuration;
using Roofied.Web.Security;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging. Avoid logging report narratives or precise locations anywhere in the app.
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/roofied-.log", rollingInterval: Serilog.RollingInterval.Day, retainedFileCountLimit: 14));

// Fail fast (and safely) if required configuration is missing.
StartupValidation.Validate(builder.Configuration, builder.Environment);

// Razor components (Interactive Server) + MudBlazor.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// Persistence, providers, validators, and application services.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        // Sign-in abuse protection.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<RoofiedDbContext>()
    .AddSignInManager()
    .AddClaimsPrincipalFactory<AdditionalUserClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Data Protection keys protect auth cookies and antiforgery tokens. By default they live in a
// per-container directory and are lost when the container is recreated (every deploy), logging
// everyone out. When a persistent path is configured (DataProtection:KeysPath — set to a mounted
// volume in production), keys survive restarts/deploys. In development the path is unset, so the
// default ephemeral location is used, which is fine.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Roofied");
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
{
    Directory.CreateDirectory(keysPath);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

// Request-scoped current-user accessor used by the application services.
builder.Services.AddScoped<ClientSession>();
builder.Services.AddScoped<Roofied.Application.Abstractions.ICurrentUser, CurrentUser>();

// Map provider abstraction (Leaflet/OSM).
builder.Services.AddScoped<Roofied.Web.Maps.IMapInterop, Roofied.Web.Maps.LeafletMapInterop>();

// Explicit, centralized authorization policies.
builder.Services.AddAuthorizationBuilder().AddRoofiedPolicies();

// Health checks for IIS / load balancer monitoring.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RoofiedDbContext>("database");

var app = builder.Build();

// Apply migrations and seed baseline data.
using (var scope = app.Services.CreateScope())
{
    try
    {
        await DbSeeder.MigrateAndSeedAsync(app.Services);
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Database migration/seed failed on startup.");
        throw;
    }
}

// HTTP pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Identity /Account Razor component endpoints.
app.MapAdditionalIdentityEndpoints();

app.MapHealthChecks("/health");

app.Run();
