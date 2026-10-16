using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.Controllers;
using SmartTorque.Web.ViewModels;

using Xunit;

namespace SmartTorque.Tests.Controllers;

public class ReportControllerTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ReportController CreateController(AppDbContext db, IReportService reportService)
    {
        var tempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider());
        var controller = new ReportController(reportService, db, NullLogger<ReportController>.Instance)
        {
            TempData = tempData,
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        return controller;
    }

    [Fact]
    public async Task Sessions_ReturnsViewWithViewModelAndSelectLists()
    {
        using var db = CreateInMemoryDbContext();
        var reportService = new ReportService(db);
        var controller = CreateController(db, reportService);

        var result = await controller.Sessions(new SessionReportFilter(), CancellationToken.None);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<SessionReportViewModel>().Subject;
        model.Workers.Should().NotBeNull();
        model.Products.Should().NotBeNull();
        model.Devices.Should().NotBeNull();
    }

    [Fact]
    public async Task SessionDetail_ReturnsView_WhenSessionExists()
    {
        using var db = CreateInMemoryDbContext();
        var now = DateTime.UtcNow;

        var product = new Product { ProductCode = "P-1", ProductName = "Prod 1" };
        var template = new Template { TemplateCode = "T-1", Name = "Tpl 1" };
        db.Products.Add(product);
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var comp = new ProductComponent { ProductId = product.ProductId, TemplateId = template.TemplateId, ComponentName = "Comp 1" };
        var job = new Job { JobNo = "JOB-1", ProductId = product.ProductId };
        var worker = new ApplicationUser { Id = "w1", UserName = "w1@t.com", FullName = "Worker One" };
        db.ProductComponents.Add(comp);
        db.Jobs.Add(job);
        db.Users.Add(worker);
        await db.SaveChangesAsync();

        var session = new Session
        {
            JobId = job.JobId,
            ProductComponentId = comp.ProductComponentId,
            WorkerId = worker.Id,
            StartTime = now,
            Status = SessionStatus.Completed,
            FinalResult = SmartTorque.Core.Enums.SessionResult.Pass
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var reportService = new ReportService(db);
        var controller = CreateController(db, reportService);

        var result = await controller.SessionDetail(session.SessionId, CancellationToken.None);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<SessionDetailDto>().Subject;
        model.SessionId.Should().Be(session.SessionId);
        model.JobNo.Should().Be("JOB-1");
    }

    [Fact]
    public async Task SessionDetail_Redirects_WhenSessionNotFound()
    {
        using var db = CreateInMemoryDbContext();
        var reportService = new ReportService(db);
        var controller = CreateController(db, reportService);

        var result = await controller.SessionDetail(99999, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ReportController.Sessions));
    }

    [Fact]
    public async Task Traceability_ReturnsViewWithTraceabilityReportViewModel()
    {
        using var db = CreateInMemoryDbContext();
        var reportService = new ReportService(db);
        var controller = CreateController(db, reportService);

        var result = await controller.Traceability("SN-12345", CancellationToken.None);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<TraceabilityReportViewModel>().Subject;
        model.Query.Should().Be("SN-12345");
        model.Report.Should().NotBeNull();
    }

    [Fact]
    public async Task Summary_ReturnsViewWithPassFailSummaryViewModel()
    {
        using var db = CreateInMemoryDbContext();
        var reportService = new ReportService(db);
        var controller = CreateController(db, reportService);

        var result = await controller.Summary(null, null, CancellationToken.None);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<PassFailSummaryViewModel>().Subject;
        model.Summary.Should().NotBeNull();
    }

    [Fact]
    public async Task Workers_ReturnsViewWithWorkerPerformanceViewModel()
    {
        using var db = CreateInMemoryDbContext();
        var reportService = new ReportService(db);
        var controller = CreateController(db, reportService);

        var result = await controller.Workers(null, null, CancellationToken.None);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<WorkerPerformanceViewModel>().Subject;
        model.Workers.Should().NotBeNull();
    }

    [Fact]
    public async Task Devices_ReturnsViewWithDeviceUsageViewModel()
    {
        using var db = CreateInMemoryDbContext();
        var reportService = new ReportService(db);
        var controller = CreateController(db, reportService);

        var result = await controller.Devices(CancellationToken.None);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DeviceUsageViewModel>().Subject;
        model.Devices.Should().NotBeNull();
    }

    [Fact]
    public async Task ExportSessionsExcel_ReturnsFileResultWithExcelContentType()
    {
        using var db = CreateInMemoryDbContext();
        var reportService = new ReportService(db);
        var controller = CreateController(db, reportService);

        var result = await controller.ExportSessionsExcel(new SessionReportFilter(), CancellationToken.None);

        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        fileResult.FileDownloadName.Should().StartWith("smarttorque_sessions_");
    }

    [Fact]
    public async Task ExportSessionsPdf_ReturnsFileResultWithPdfContentType()
    {
        using var db = CreateInMemoryDbContext();
        var reportService = new ReportService(db);
        var controller = CreateController(db, reportService);

        var result = await controller.ExportSessionsPdf(new SessionReportFilter(), CancellationToken.None);

        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileDownloadName.Should().StartWith("smarttorque_sessions_");
    }
}