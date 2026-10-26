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
        var controller = new ProductController(productService, new TemplateService(db, imageService), imageService, evaluator, new SettingsService(db), NullLogger<ProductController>.Instance)
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

    [Fact]
    public async Task SetActive_CanDeactivateAndReactivateProduct()
    {
        using var db = CreateInMemoryDbContext();
        var tmpl = await AddTemplateAsync(db, "TMPL-X", "Side Wall");

        var productService = new ProductService(db);
        var imageService = new ImageUploadService(Path.GetTempPath());
        var evaluator = new ToleranceService();
        var controller = new ProductController(productService, new TemplateService(db, imageService), imageService, evaluator, new SettingsService(db), NullLogger<ProductController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider()),
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")]))
                }
            }
        };

        var product = await productService.CreateAsync(new SaveProductRequest(
            "PRD-ACT-01", "Toggle Product", null, null,
            [new ProductComponentRequest(tmpl.TemplateId, "Wall", 1)]), "admin");

        product.IsActive.Should().BeTrue();

        // 1. Deactivate
        var deactResult = await controller.SetActive(product.ProductId, false);
        deactResult.Should().BeOfType<RedirectToActionResult>();
        controller.TempData["Success"].Should().Be("Product deactivated.");

        var deactProduct = await productService.GetByIdAsync(product.ProductId);
        deactProduct!.IsActive.Should().BeFalse();

        // 2. Reactivate (explicit true)
        var actResult = await controller.SetActive(product.ProductId, true);
        actResult.Should().BeOfType<RedirectToActionResult>();
        controller.TempData["Success"].Should().Be("Product activated.");

        var actProduct = await productService.GetByIdAsync(product.ProductId);
        actProduct!.IsActive.Should().BeTrue();

        // 3. Toggle when isActive is omitted/null
        var toggleDeact = await controller.SetActive(product.ProductId, null);
        toggleDeact.Should().BeOfType<RedirectToActionResult>();
        var toggled1 = await productService.GetByIdAsync(product.ProductId);
        toggled1!.IsActive.Should().BeFalse();

        var toggleAct = await controller.SetActive(product.ProductId, null);
        toggleAct.Should().BeOfType<RedirectToActionResult>();
        var toggled2 = await productService.GetByIdAsync(product.ProductId);
        toggled2!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CheckTemplateSimilarity_WhenMatchingTemplateExists_ReturnsSimilarFoundAndSideBySideSpecs()
    {
        using var db = CreateInMemoryDbContext();
        var tmpl = await AddTemplateAsync(db, "TW-2B-25NM-T2", "Main Panel Bolts");

        var productService = new ProductService(db);
        var imageService = new ImageUploadService(Path.GetTempPath());
        var templateService = new TemplateService(db, imageService);
        var evaluator = new ToleranceService();
        var controller = new ProductController(productService, templateService, imageService, evaluator, new SettingsService(db), NullLogger<ProductController>.Instance)
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

        var req = new CheckTemplateSimilarityRequest
        {
            Name = "New Motor Mount",
            Mode = TemplateMode.Torque,
            Steps =
            [
                new QuickStepDto { SequenceNo = 1, Label = "Bolt 1", Instrument = InstrumentType.Torque, Target = 25m, TolerancePlus = 2m, ToleranceMinus = 2m },
                new QuickStepDto { SequenceNo = 2, Label = "Bolt 2", Instrument = InstrumentType.Torque, Target = 25m, TolerancePlus = 2m, ToleranceMinus = 2m }
            ]
        };

        var result = await controller.CheckTemplateSimilarity(req);
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().NotBeNull();

        // Check anonymous properties via reflection
        var val = ok.Value!;
        var isSimilarFound = (bool)val.GetType().GetProperty("isSimilarFound")!.GetValue(val)!;
        isSimilarFound.Should().BeTrue();

        var existing = val.GetType().GetProperty("existing")!.GetValue(val)!;
        existing.GetType().GetProperty("name")!.GetValue(existing)!.Should().Be("Main Panel Bolts");
    }

    [Fact]
    public async Task CheckTemplateSimilarity_WhenNoMatch_ReturnsSimilarFalse()
    {
        using var db = CreateInMemoryDbContext();
        var productService = new ProductService(db);
        var imageService = new ImageUploadService(Path.GetTempPath());
        var templateService = new TemplateService(db, imageService);
        var evaluator = new ToleranceService();
        var controller = new ProductController(productService, templateService, imageService, evaluator, new SettingsService(db), NullLogger<ProductController>.Instance)
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

        var req = new CheckTemplateSimilarityRequest
        {
            Name = "Brand New Custom Part",
            Mode = TemplateMode.Torque,
            Steps =
            [
                new QuickStepDto { SequenceNo = 1, Label = "B1", Instrument = InstrumentType.Torque, Target = 99m, TolerancePlus = 1m, ToleranceMinus = 1m }
            ]
        };

        var result = await controller.CheckTemplateSimilarity(req);
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var val = ok.Value!;
        var isSimilarFound = (bool)val.GetType().GetProperty("isSimilarFound")!.GetValue(val)!;
        isSimilarFound.Should().BeFalse();
    }

    [Fact]
    public async Task CreateInlineTemplate_CreatesTemplateAndReturnsMetadata()
    {
        using var db = CreateInMemoryDbContext();
        var productService = new ProductService(db);
        var imageService = new ImageUploadService(Path.GetTempPath());
        var templateService = new TemplateService(db, imageService);
        var evaluator = new ToleranceService();
        var controller = new ProductController(productService, templateService, imageService, evaluator, new SettingsService(db), NullLogger<ProductController>.Instance)
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

        var req = new CreateInlineTemplateRequest
        {
            Name = "Inline Fastener Assembly",
            Mode = TemplateMode.Torque,
            Steps =
            [
                new QuickStepDto { SequenceNo = 1, Label = "Bolt A", Instrument = InstrumentType.Torque, Target = 18m, TolerancePlus = 1.5m, ToleranceMinus = 1.5m },
                new QuickStepDto { SequenceNo = 2, Label = "Bolt B", Instrument = InstrumentType.Torque, Target = 18m, TolerancePlus = 1.5m, ToleranceMinus = 1.5m }
            ]
        };

        var result = await controller.CreateInlineTemplate(req);
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var val = ok.Value!;
        var success = (bool)val.GetType().GetProperty("success")!.GetValue(val)!;
        success.Should().BeTrue();

        var tplId = (int)val.GetType().GetProperty("templateId")!.GetValue(val)!;
        var saved = await templateService.GetByIdAsync(tplId);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Inline Fastener Assembly");
        saved.Steps.Should().HaveCount(2);
    }
}
