using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Roofied.Application.Abstractions;
using Roofied.Application.Admin;
using Roofied.Application.Channels;
using Roofied.Application.Channels.Validation;
using Roofied.Application.Common;
using Roofied.Application.Flags;
using Roofied.Application.Flags.Validation;
using Roofied.Application.Geo;
using Roofied.Application.Lookups;
using Roofied.Application.Moderation;
using Roofied.Application.Reports;
using Roofied.Application.Reports.Validation;
using Roofied.Application.Resources;
using Roofied.Application.Safety;
using Roofied.Infrastructure.Options;
using Roofied.Infrastructure.Persistence;
using Roofied.Infrastructure.Providers;
using Roofied.Infrastructure.Services;

namespace Roofied.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence, options, providers, validators, and application services.
    /// Identity, authentication, and request-scoped <see cref="ICurrentUser"/> are wired up by the web host.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Options (validated on start where required).
        services.AddOptions<SecurityOptions>().Bind(configuration.GetSection(SecurityOptions.SectionName));
        services.AddOptions<CaptchaOptions>().Bind(configuration.GetSection(CaptchaOptions.SectionName));
        services.AddOptions<LocationPrecisionConfig>().Bind(configuration.GetSection(LocationPrecisionConfig.SectionName));
        services.AddOptions<RateLimitOptions>().Bind(configuration.GetSection(RateLimitOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddSingleton<IClock, SystemClock>();

        services.AddDbContext<RoofiedDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(RoofiedDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure();
            });
            // Soft-delete query filters are applied via the metadata API, which can produce a
            // false-positive PendingModelChangesWarning at runtime. Migrations are authoritative
            // (verified by `dotnet ef migrations has-pending-model-changes`), so log instead of throw.
            options.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));
        });

        // Providers
        services.AddSingleton<IHtmlSanitizer, HtmlSanitizerService>();
        services.AddSingleton<IIpHasher, IpHasher>();
        services.AddSingleton<IReferenceCodeGenerator, ReferenceCodeGenerator>();
        services.AddSingleton<ILocationPrecisionService, LocationPrecisionService>();
        services.AddSingleton<IPiiDetectionService, PiiDetectionService>();
        services.AddSingleton<IProfanityFilter>(_ => new ProfanityFilter());
        services.AddHttpClient<ICaptchaVerifier, TurnstileCaptchaVerifier>();

        // Application services
        services.AddScoped<IRateLimitService, RateLimitService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IModerationService, ModerationService>();
        services.AddScoped<IChannelService, ChannelService>();
        services.AddScoped<IContentFlagService, ContentFlagService>();
        services.AddScoped<IResourceService, ResourceService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IUserAdminService, UserAdminService>();

        // Validators
        services.AddScoped<IValidator<ReportSubmissionInput>, ReportSubmissionInputValidator>();
        services.AddScoped<IValidator<ChannelPostInput>, ChannelPostInputValidator>();
        services.AddScoped<IValidator<ContentFlagInput>, ContentFlagInputValidator>();

        return services;
    }
}
