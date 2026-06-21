using System.Linq.Expressions;
using System.Reflection;
using Roofied.Application.Reports;
using Roofied.Application.Reports.Dtos;
using Roofied.Domain.Reports;

namespace Roofied.Tests;

/// <summary>
/// Structural guarantees that the public report surface cannot carry restricted data.
/// These tests fail the build if anyone adds a leaky field or projection reference.
/// </summary>
public class PublicProjectionSafetyTests
{
    private static readonly string[] ForbiddenPropertyFragments =
    {
        "narrative", "symptom", "privatecontact", "contactvalue", "contactmethod",
        "exactlat", "exactlon", "exactaddress", "iphash", "createdby", "ipaddress",
        "moderatornote", "internalnote", "rawnarrative", "restricted",
    };

    public static IEnumerable<object[]> PublicDtoTypes() => new[]
    {
        new object[] { typeof(PublicMapPointDto) },
        new object[] { typeof(PublicReportListItemDto) },
        new object[] { typeof(PublicReportDetailDto) },
    };

    [Theory]
    [MemberData(nameof(PublicDtoTypes))]
    public void Public_dtos_expose_no_restricted_property_names(Type dtoType)
    {
        foreach (var prop in dtoType.GetProperties())
        {
            var name = prop.Name.ToLowerInvariant();
            Assert.DoesNotContain(ForbiddenPropertyFragments, frag => name.Contains(frag));
        }
    }

    [Fact]
    public void Map_point_dto_has_no_exact_coordinate_property()
    {
        var props = typeof(PublicMapPointDto).GetProperties().Select(p => p.Name).ToList();
        Assert.Contains("ApproxLatitude", props);
        Assert.Contains("ApproxLongitude", props);
        Assert.DoesNotContain("ExactLatitude", props);
        Assert.DoesNotContain("Latitude", props);
    }

    [Theory]
    [MemberData(nameof(Projections))]
    public void Projections_never_reference_restricted_entities_or_fields(LambdaExpression projection)
    {
        var collector = new MemberAccessCollector();
        collector.Visit(projection);

        // No member of a restricted satellite entity may be referenced.
        Assert.DoesNotContain(collector.Members, m =>
            m.DeclaringType == typeof(ReportRestricted) || m.DeclaringType == typeof(ReportLocation));

        // No restricted member of Report itself may be referenced.
        var forbiddenReportMembers = new[]
        {
            nameof(Report.Restricted), nameof(Report.PreciseLocation), nameof(Report.CreatedByUserId),
        };
        Assert.DoesNotContain(collector.Members, m =>
            m.DeclaringType == typeof(Report) && forbiddenReportMembers.Contains(m.Name));
    }

    public static IEnumerable<object[]> Projections() => new[]
    {
        new object[] { PublicReportProjections.ToMapPoint },
        new object[] { PublicReportProjections.ToListItem },
        new object[] { PublicReportProjections.ToDetail },
    };

    private sealed class MemberAccessCollector : ExpressionVisitor
    {
        public List<MemberInfo> Members { get; } = new();

        protected override Expression VisitMember(MemberExpression node)
        {
            Members.Add(node.Member);
            return base.VisitMember(node);
        }
    }
}
