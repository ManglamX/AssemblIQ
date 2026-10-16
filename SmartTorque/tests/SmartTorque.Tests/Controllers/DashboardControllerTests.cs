using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.Areas.Admin.Controllers;

using Xunit;

namespace SmartTorque.Tests.Controllers;

public class DashboardControllerTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Index_ReturnsViewWithDashboardStatsDto()
    {
        using var db = CreateInMemoryDbContext();
        var reportService = new ReportService(db);
        var controller = new DashboardController(reportService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Index(CancellationToken.None);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DashboardStatsDto>().Subject;
        model.JobsByStatus.Should().NotBeNull();
        model.RecentSessions.Should().NotBeNull();
    }
}