using System.Security.Claims;

using FluentAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.Controllers;
using SmartTorque.Web.ViewModels.Product;

using Xunit;

namespace SmartTorque.Tests.Controllers;

public class ProductControllerTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ApplicationName { get; set; } = "SmartTorque.Test";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
    }

    private static async Task<Template> AddTemplateAsync(AppDbContext db, string code, string name)
    {
        var template = new Template
        {
            TemplateCode = code,
            Version = 1,
            IsLatest = true,
            Name = name,
            Mode = TemplateMode.Torque,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
            Steps =
            {
                new TemplateStep
                {
                    SequenceNo = 1,
                    Instrument = InstrumentType.Torque,
                    Label = "Tighten Bolt",
                    Target = 25m,
                    TolerancePlus = 2m,
                    ToleranceMinus = 2m,
                    ToleranceMode = ToleranceMode.Absolute,
                    MaxRetries = 3
                }
            }
        };
        db.Templates.Add(template);
        await db.SaveChangesAsync();
        return template;
    }

    [Fact]
    public async Task Create_WhenComponentNameIsEmpty_DefaultsToTemplateNameAndSucceeds()
    {
        using var db = CreateInMemoryDbContext();
        var tmpl1 = await AddTemplateAsync(db, "TMPL-A", "Back Enclosure Panel");
        var tmpl2 = await AddTemplateAsync(db, "TMPL-B", "Top Roof Cover");

        var productService = new ProductService(db);
        var imageService = new ImageUploadService(Path.GetTempPath());
        var evaluator = new ToleranceService();
        var controller = new ProductController(productService, imageService, evaluator, NullLogger<ProductController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider()),
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "testuser")]))
                }
            }
        };

        var model = new ProductBuilderViewModel
        {
            ProductCode = "PRD-AUTO-01",
            ProductName = "Auto Panel Assembly",
            Components =
            [
                new ProductComponentRowViewModel
                {
                    TemplateId = tmpl1.TemplateId,
                    ComponentName = "", // User left blank
                    Quantity = 1
                },
                new ProductComponentRowViewModel
                {
                    TemplateId = tmpl2.TemplateId,
                    ComponentName = "   ", // User typed whitespace
                    Quantity = 2
                }
            ]
        };

        var result = await controller.Create(model);

        result.Should().BeOfType<RedirectToActionResult>();
        var redirect = (RedirectToActionResult)result;
        redirect.ActionName.Should().Be("Detail");

        var created = await productService.GetByCodeAsync("PRD-AUTO-01");
        created.Should().NotBeNull();
        created!.Components.Should().HaveCount(2);
        created.Components.ElementAt(0).ComponentName.Should().Be("Back Enclosure Panel");
        created.Components.ElementAt(1).ComponentName.Should().Be("Top Roof Cover");
    }
}
