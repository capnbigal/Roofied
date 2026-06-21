using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Roofied.Domain.Channels;
using Roofied.Domain.Enums;
using Roofied.Domain.Identity;
using Roofied.Domain.Profiles;
using Roofied.Domain.Reports;
using Roofied.Domain.Resources;
using Roofied.Infrastructure.Common;

namespace Roofied.Infrastructure.Persistence.Seed;

/// <summary>
/// Applies pending migrations and seeds baseline data: roles, an initial administrator,
/// report/venue categories, the initial channels, and starter resource entries.
/// Idempotent — safe to run on every startup.
/// </summary>
public static class DbSeeder
{
    public static async Task MigrateAndSeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<RoofiedDbContext>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        await db.Database.MigrateAsync(ct);

        await SeedRolesAsync(sp);
        await SeedAdminAsync(sp, logger);
        await SeedReportCategoriesAsync(db, ct);
        await SeedVenueCategoriesAsync(db, ct);
        await SeedChannelsAsync(db, ct);
        await SeedResourcesAsync(db, ct);

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedRolesAsync(IServiceProvider sp)
    {
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private static async Task SeedAdminAsync(IServiceProvider sp, ILogger logger)
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("SeedAdmin:Email/Password not configured; skipping admin seeding.");
            return;
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
            return;

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedUtc = DateTime.UtcNow,
            // UserId is fixed up to the new user's id by EF via the 1:1 relationship on save.
            Profile = new UserProfile { UserId = "pending", DisplayName = "Administrator" },
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogError("Failed to seed admin user: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRolesAsync(admin, new[] { RoleNames.Administrator, RoleNames.Moderator, RoleNames.RegisteredUser });
        logger.LogInformation("Seeded administrator account {Email}.", email);
    }

    private static async Task SeedReportCategoriesAsync(RoofiedDbContext db, CancellationToken ct)
    {
        if (await db.ReportCategories.AnyAsync(ct))
            return;
        var names = new (string Name, string? Desc)[]
        {
            ("Suspected drink tampering", "Believed a drink was spiked or tampered with."),
            ("Needle / injection spiking", "Suspected spiking via injection."),
            ("Food tampering", "Suspected tampering with food."),
            ("Unsure / other", "Unsure of the method or another situation."),
        };
        var order = 0;
        foreach (var (name, desc) in names)
            db.ReportCategories.Add(new ReportCategory { Name = name, Slug = Slug.From(name), Description = desc, SortOrder = order++ });
    }

    private static async Task SeedVenueCategoriesAsync(RoofiedDbContext db, CancellationToken ct)
    {
        if (await db.VenueCategories.AnyAsync(ct))
            return;
        var names = new[] { "Bar", "Restaurant", "Party", "Concert / event", "Home", "Rideshare", "Other" };
        var order = 0;
        foreach (var name in names)
            db.VenueCategories.Add(new VenueCategory { Name = name, Slug = Slug.From(name), SortOrder = order++ });
    }

    private static async Task SeedChannelsAsync(RoofiedDbContext db, CancellationToken ct)
    {
        if (await db.Channels.AnyAsync(ct))
            return;
        var channels = new (string Name, string Desc, string Guidelines)[]
        {
            ("Support and Resources", "A supportive space for sharing resources and encouragement.",
                "Be kind and respectful. Do not name or accuse specific people, venues, or businesses. Avoid sharing personal information. This is not a substitute for emergency help."),
            ("Safety Tips", "Practical safety information and prevention tips.",
                "Share general safety tips. No accusations against identifiable people or places. No personal information."),
            ("General Discussion", "General community conversation.",
                "Keep it respectful and on-topic. No accusations, no personal information, no doxxing."),
            ("Local Awareness", "General, non-identifying local safety awareness.",
                "Discuss general awareness only. Do NOT name specific venues, people, or businesses. Use general areas only."),
            ("Site Updates", "Announcements and updates from the team.",
                "Official updates. Posting may be limited to staff."),
        };
        var order = 0;
        foreach (var (name, desc, guidelines) in channels)
        {
            db.Channels.Add(new Channel
            {
                Name = name,
                Slug = Slug.From(name),
                Description = desc,
                Guidelines = guidelines,
                SortOrder = order++,
                AllowAnonymousPosts = name != "Site Updates",
                CommentsEnabled = false,
            });
        }
    }

    private static async Task SeedResourcesAsync(RoofiedDbContext db, CancellationToken ct)
    {
        if (await db.Resources.AnyAsync(ct))
            return;
        db.Resources.AddRange(
            new Resource
            {
                Title = "In an emergency, contact your local emergency number",
                Category = ResourceCategory.Emergency,
                Description = "If you or someone else is in immediate danger or needs urgent medical help, contact your local emergency services right away (for example, 911 in the US). This app is not a substitute for emergency services.",
                IsEmergency = true,
                SortOrder = 0,
            },
            new Resource
            {
                Title = "Seek medical care",
                Category = ResourceCategory.Medical,
                Description = "If you think you may have been spiked, seek medical care as soon as you can. Some substances leave the body quickly, so prompt testing can matter. Tell medical staff your concerns.",
                SortOrder = 1,
            },
            new Resource
            {
                Title = "Preserving evidence (general guidance)",
                Category = ResourceCategory.EvidencePreservation,
                Description = "If it is safe to do so, you may wish to preserve anything that could be relevant and seek timely medical testing. Guidance and options vary by region; consider speaking with medical professionals or local support services. This is general information, not legal advice, and does not promise any particular outcome.",
                SortOrder = 2,
            },
            new Resource
            {
                Title = "Reach out to someone you trust",
                Category = ResourceCategory.Support,
                Description = "Consider contacting a trusted friend, family member, or a local victim-support service. You do not have to manage this alone.",
                SortOrder = 3,
            });
    }
}
