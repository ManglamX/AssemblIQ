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
                new QuickStepDto { SequenceNo = 1, Label = "Bolt A", Instrument = InstrumentType.Torque, Target = 18m, TolerancePlus = 1.5m, ToleranceMinus = 1.5m, MarkerX = 25m, MarkerY = 30m, MarkerSize = 16 },
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
        saved.Steps.OrderBy(s => s.SequenceNo).First().MarkerSize.Should().Be(16);
        saved.Steps.OrderBy(s => s.SequenceNo).Last().MarkerSize.Should().BeNull();
    }

    [Fact]
    public async Task CreateInlineTemplate_ClampsOutOfRangeMarkerSize()
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
            Name = "Clamp Test Assembly",
            Mode = TemplateMode.Torque,
            Steps =
            [
                new QuickStepDto { SequenceNo = 1, Label = "Bolt A", Instrument = InstrumentType.Torque, Target = 18m, TolerancePlus = 1m, ToleranceMinus = 1m, MarkerX = 30m, MarkerY = 40m, MarkerSize = 999 },
                new QuickStepDto { SequenceNo = 2, Label = "Bolt B", Instrument = InstrumentType.Torque, Target = 18m, TolerancePlus = 1m, ToleranceMinus = 1m, MarkerX = 40m, MarkerY = 50m, MarkerSize = 0 }
            ]
        };

        var result = await controller.CreateInlineTemplate(req);
        result.Should().BeOfType<OkObjectResult>();
        var val = ((OkObjectResult)result).Value!;
        var tplId = (int)val.GetType().GetProperty("templateId")!.GetValue(val)!;
        var saved = await templateService.GetByIdAsync(tplId);
        saved.Should().NotBeNull();
        saved!.Steps.Should().HaveCount(2);
        saved.Steps.OrderBy(s => s.SequenceNo).First().MarkerSize.Should().Be(56);
        saved.Steps.OrderBy(s => s.SequenceNo).Last().MarkerSize.Should().Be(14);
    }

    private static ProductController BuildProductController(AppDbContext db)
    {
        var productService = new ProductService(db);
        var imageService = new ImageUploadService(Path.GetTempPath());
        return new ProductController(productService, new TemplateService(db, imageService), imageService, new ToleranceService(), new SettingsService(db), NullLogger<ProductController>.Instance)
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
    }

    private static async Task<Product> AddProductAsync(AppDbContext db, string code, string name)
    {
        var product = new Product
        {
            ProductCode = code,
            ProductName = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    [Fact]
    public async Task CheckCode_WhenCodeAlreadyUsed_ReturnsAvailableFalse()
    {
        using var db = CreateInMemoryDbContext();
        await AddProductAsync(db, "PRD-100", "Pump Assembly");
        var controller = BuildProductController(db);

        var result = await controller.CheckCode(new CheckCodeRequest { Code = "prd-100" });

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var val = ok.Value!;
        var available = (bool)val.GetType().GetProperty("available")!.GetValue(val)!;
        available.Should().BeFalse();
    }

    [Fact]
    public async Task CheckCode_WhenCodeAvailable_ReturnsAvailableTrue()
    {
        using var db = CreateInMemoryDbContext();
        await AddProductAsync(db, "PRD-100", "Pump Assembly");
        var controller = BuildProductController(db);

        var result = await controller.CheckCode(new CheckCodeRequest { Code = "PRD-200" });

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var val = ok.Value!;
        var available = (bool)val.GetType().GetProperty("available")!.GetValue(val)!;
        available.Should().BeTrue();
    }

    [Fact]
    public async Task CheckCode_WithExcludeProductId_TreatsEditedProductCodeAsAvailable()
    {
        using var db = CreateInMemoryDbContext();
        var product = await AddProductAsync(db, "PRD-100", "Pump Assembly");
        var controller = BuildProductController(db);

        var result = await controller.CheckCode(new CheckCodeRequest { Code = "PRD-100", ExcludeProductId = product.ProductId });

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var val = ok.Value!;
        var available = (bool)val.GetType().GetProperty("available")!.GetValue(val)!;
        available.Should().BeTrue();
    }

    [Fact]
    public async Task CheckCode_WhenCodeEmpty_ReturnsBadRequest()
    {
        using var db = CreateInMemoryDbContext();
        await AddProductAsync(db, "PRD-100", "Pump Assembly");
        var controller = BuildProductController(db);

        var result = await controller.CheckCode(new CheckCodeRequest { Code = "   " });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_WithGalleryImages_PersistsGalleryRowsAndPrimary()
    {
        using var db = CreateInMemoryDbContext();
        var tmpl = await AddTemplateAsync(db, "TMPL-GAL", "Panel Enclosure");
        var controller = BuildProductController(db);

        var model = new ProductBuilderViewModel
        {
            ProductCode = "PRD-GAL-01",
            ProductName = "Gallery Product",
            Images = ["uploads/products/p1.jpg", "uploads/products/p2.jpg"],
            Components =
            [
                new ProductComponentRowViewModel { TemplateId = tmpl.TemplateId, ComponentName = "Panel", Quantity = 1 }
            ]
        };

        var result = await controller.Create(model);

        result.Should().BeOfType<RedirectToActionResult>();
        var productService = new ProductService(db);
        var created = await productService.GetByCodeAsync("PRD-GAL-01");
        created.Should().NotBeNull();
        created!.ImagePath.Should().Be("uploads/products/p1.jpg");
        created.Images.OrderBy(i => i.SortOrder).Select(i => i.ImagePath)
            .Should().Equal("uploads/products/p1.jpg", "uploads/products/p2.jpg");
    }

    [Fact]
    public async Task Edit_WithGalleryImages_ReplacesExistingGallery()
    {
        using var db = CreateInMemoryDbContext();
        var tmpl = await AddTemplateAsync(db, "TMPL-GAL", "Panel Enclosure");
        var productService = new ProductService(db);
        var existing = await productService.CreateAsync(new SaveProductRequest(
            "PRD-GAL-02", "Gallery Edit Product", null, null,
            [new ProductComponentRequest(tmpl.TemplateId, "Panel", 1)],
            ["uploads/products/old1.jpg", "uploads/products/old2.jpg"]), "testuser");
        var controller = BuildProductController(db);

        var model = new ProductBuilderViewModel
        {
            EditingProductId = existing.ProductId,
            ProductCode = "PRD-GAL-02",
            ProductName = "Gallery Edit Product",
            Images = ["uploads/products/new1.jpg", "uploads/products/new2.jpg", "../evil.jpg"],
            Components =
            [
                new ProductComponentRowViewModel
                {
                    ProductComponentId = existing.Components.Single().ProductComponentId,
                    TemplateId = tmpl.TemplateId,
                    ComponentName = "Panel",
                    Quantity = 1
                }
            ]
        };

        var result = await controller.Edit(model);

        result.Should().BeOfType<RedirectToActionResult>();
        var updated = await productService.GetByIdAsync(existing.ProductId);
        updated.Should().NotBeNull();
        updated!.ImagePath.Should().Be("uploads/products/new1.jpg");
        updated.Images.OrderBy(i => i.SortOrder).Select(i => i.ImagePath)
            .Should().Equal("uploads/products/new1.jpg", "uploads/products/new2.jpg");
    }
}