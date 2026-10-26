using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Core.Interfaces;
using SmartTorque.Services;
using SmartTorque.Web.ViewModels.Product;

namespace SmartTorque.Web.Controllers;

[Authorize(Roles = "Admin")]
public class ProductController : Controller
{
    private readonly IProductService _products;
    private readonly ITemplateService _templates;
    private readonly IImageUploadService _images;
    private readonly IMeasurementEvaluator _evaluator;
    private readonly ISettingsService _settings;
    private readonly ILogger<ProductController> _logger;

    public ProductController(
        IProductService products,
        ITemplateService templates,
        IImageUploadService images,
        IMeasurementEvaluator evaluator,
        ISettingsService settings,
        ILogger<ProductController> logger)
    {
        _products = products;
        _templates = templates;
        _images = images;
        _evaluator = evaluator;
        _settings = settings;
        _logger = logger;
    }

    // GET /Product
    [HttpGet]
    public async Task<IActionResult> Index(string? search, bool? active, int page = 1)
    {
        var filter = new ProductFilter(search, active, page, 25);
        var items = await _products.ListAsync(filter);
        var allItems = await _products.ListAsync(filter with { Page = 1, PageSize = int.MaxValue });

        var vm = new ProductListViewModel
        {
            Items = items.Select(p => new ProductRowViewModel
            {
                ProductId = p.ProductId,
                ProductCode = p.ProductCode,
                ProductName = p.ProductName,
                ComponentCount = p.Components.Count,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt
            }).ToList(),
            TotalCount = allItems.Count,
            Page = page,
            PageSize = 25,
            Search = search,
            ActiveFilter = active
        };

        ViewData["Title"] = "Products";
        ViewData["ActiveNav"] = "Products";
        return View(vm);
    }

    // GET /Product/Create
    [HttpGet]
    public async Task<IActionResult> Create(string? templateCode = null, int? templateId = null, string? productCode = null)
    {
        var vm = await BuildBuilderViewModelAsync(null);
        if (!string.IsNullOrWhiteSpace(productCode))
        {
            vm.ProductCode = productCode.Trim().ToUpperInvariant();
        }

        if (templateId.HasValue || !string.IsNullOrWhiteSpace(templateCode))
        {
            var match = vm.AvailableTemplates.FirstOrDefault(t =>
                (templateId.HasValue && t.TemplateId == templateId.Value) ||
                (!string.IsNullOrWhiteSpace(templateCode) && string.Equals(t.TemplateCode, templateCode, StringComparison.OrdinalIgnoreCase)));

            if (match != null)
            {
                vm.Components.Add(new ProductComponentRowViewModel
                {
                    TemplateId = match.TemplateId,
                    ComponentName = match.Name,
                    TemplateCode = match.TemplateCode,
                    TemplateName = match.Name,
                    TemplateVersion = match.Version,
                    StepCount = match.StepCount,
                    Quantity = 1
                });
            }
        }

        ViewData["Title"] = "New Product";
        ViewData["ActiveNav"] = "Products";
        return View("Builder", vm);
    }

    // POST /Product/Create
    [HttpPost]
    public async Task<IActionResult> Create(ProductBuilderViewModel model)
    {
        await PopulateOptionsAsync(model);

        var components = NormalizeComponents(model);
        if (components.Count == 0)
            ModelState.AddModelError(string.Empty, "Add at least one component to the product.");

        if (string.IsNullOrWhiteSpace(model.ProductCode))
            ModelState.AddModelError(nameof(model.ProductCode), "Product code is required.");
        else if (!await _products.IsCodeUniqueAsync(model.ProductCode, null))
        {
            model.CodeAlreadyExists = true;
            ModelState.AddModelError(nameof(model.ProductCode), "This code is already used by another product.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "New Product";
            ViewData["ActiveNav"] = "Products";
            return View("Builder", model);
        }

        var request = new SaveProductRequest(model.ProductCode, model.ProductName, model.Description, model.ImagePath, components);
        var saved = await _products.CreateAsync(request, UserName());
        _logger.LogInformation("Product {Code} created by {User}", saved.ProductCode, UserName());

        TempData["Success"] = $"Product '{saved.ProductName}' saved.";
        return RedirectToAction(nameof(Detail), new { id = saved.ProductId });
    }

    // GET /Product/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _products.GetByIdAsync(id);
        if (product == null) return NotFound();

        var vm = await BuildBuilderViewModelAsync(product);
        ViewData["Title"] = $"Edit {product.ProductName}";
        ViewData["ActiveNav"] = "Products";
        return View("Builder", vm);
    }

    // POST /Product/Edit/{id}
    [HttpPost]
    public async Task<IActionResult> Edit(ProductBuilderViewModel model)
    {
        if (!model.EditingProductId.HasValue) return BadRequest();

        await PopulateOptionsAsync(model);

        var components = NormalizeComponents(model);
        if (components.Count == 0)
            ModelState.AddModelError(string.Empty, "Add at least one component to the product.");

        if (string.IsNullOrWhiteSpace(model.ProductCode))
            ModelState.AddModelError(nameof(model.ProductCode), "Product code is required.");
        else if (!await _products.IsCodeUniqueAsync(model.ProductCode, model.EditingProductId))
        {
            model.CodeAlreadyExists = true;
            ModelState.AddModelError(nameof(model.ProductCode), "This code is already used by another product.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edit Product";
            ViewData["ActiveNav"] = "Products";
            return View("Builder", model);
        }

        var request = new SaveProductRequest(model.ProductCode, model.ProductName, model.Description, model.ImagePath, components);
        Product saved;
        try
        {
            saved = await _products.UpdateAsync(model.EditingProductId.Value, request, UserName());
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var existing = await _products.GetByIdAsync(model.EditingProductId!.Value);
            model = await BuildBuilderViewModelAsync(existing);
            model.EditingProductId = existing?.ProductId;
            ViewData["Title"] = "Edit Product";
            ViewData["ActiveNav"] = "Products";
            return View("Builder", model);
        }
        _logger.LogInformation("Product {Code} updated by {User}", saved.ProductCode, UserName());

        TempData["Success"] = $"Product '{saved.ProductName}' updated.";
        return RedirectToAction(nameof(Detail), new { id = saved.ProductId });
    }

    // POST /Product/UploadImage (AJAX)
    [HttpPost]
    public async Task<IActionResult> UploadImage(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        try
        {
            await using var stream = file.OpenReadStream();
            var maxMb = Math.Max(1, await _settings.GetAsync<int>(SettingsService.KeyImageMaxMb) ?? 5);
            var path = await _images.SaveAsync(stream, file.FileName, file.Length, "products", maxBytes: maxMb * 1024L * 1024L);
            return Ok(new { path, url = $"/{path}" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ================================================================
    // CHECK TEMPLATE SIMILARITY (AJAX) POST /Product/CheckTemplateSimilarity
    // ================================================================
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CheckTemplateSimilarity([FromBody] CheckTemplateSimilarityRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name) || request.Steps == null || request.Steps.Count == 0)
        {
            return BadRequest(new { error = "Template name and at least one step are required." });
        }

        var stepSpecs = request.Steps.Select(s => new TemplateCodeGenerator.StepSpec(
            s.Instrument, s.Target, s.TolerancePlus, s.ToleranceMinus, s.ToleranceMode)).ToList();

        var generatedCode = TemplateCodeGenerator.Generate(request.Mode, stepSpecs);

        // 1. Check exact code match
        var matchingTemplate = await _templates.GetLatestByCodeAsync(generatedCode);

        // 2. Check identical step specs across latest templates if code didn't match directly
        if (matchingTemplate == null)
        {
            var latestTemplates = await _products.GetLatestTemplatesAsync();
            foreach (var t in latestTemplates)
            {
                if (t.Mode == request.Mode && t.Steps.Count == request.Steps.Count)
                {
                    var tSteps = t.Steps.OrderBy(s => s.SequenceNo).ToList();
                    var rSteps = request.Steps.OrderBy(s => s.SequenceNo).ToList();
                    bool allMatch = true;
                    for (int i = 0; i < tSteps.Count; i++)
                    {
                        if (tSteps[i].Instrument != rSteps[i].Instrument ||
                            tSteps[i].Target != rSteps[i].Target ||
                            tSteps[i].TolerancePlus != rSteps[i].TolerancePlus ||
                            tSteps[i].ToleranceMinus != rSteps[i].ToleranceMinus)
                        {
                            allMatch = false;
                            break;
                        }
                    }

                    if (allMatch)
                    {
                        matchingTemplate = t;
                        break;
                    }
                }
            }
        }

        // 3. Check exact name match
        if (matchingTemplate == null)
        {
            var latestTemplates = await _products.GetLatestTemplatesAsync();
            matchingTemplate = latestTemplates.FirstOrDefault(t =>
                string.Equals(t.Name.Trim(), request.Name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (matchingTemplate != null)
        {
            return Ok(new
            {
                isSimilarFound = true,
                generatedCode,
                existing = new
                {
                    templateId = matchingTemplate.TemplateId,
                    templateCode = matchingTemplate.TemplateCode,
                    name = matchingTemplate.Name,
                    version = matchingTemplate.Version,
                    mode = matchingTemplate.Mode.ToString(),
                    stepCount = matchingTemplate.Steps.Count,
                    imagePath = matchingTemplate.ImagePath,
                    steps = matchingTemplate.Steps.OrderBy(s => s.SequenceNo).Select(s => new
                    {
                        sequenceNo = s.SequenceNo,
                        label = s.Label,
                        instrument = s.Instrument.ToString(),
                        target = s.Target,
                        tolerancePlus = s.TolerancePlus,
                        toleranceMinus = s.ToleranceMinus,
                        toleranceMode = s.ToleranceMode.ToString(),
                        markerX = s.MarkerX,
                        markerY = s.MarkerY,
                        unit = s.Instrument == InstrumentType.Torque ? "N·m" : "mm"
                    }).ToList()
                },
                newSpec = new
                {
                    name = request.Name.Trim(),
                    generatedCode,
                    mode = request.Mode.ToString(),
                    stepCount = request.Steps.Count,
                    imagePath = request.ImagePath,
                    steps = request.Steps.Select((s, i) => new
                    {
                        sequenceNo = s.SequenceNo > 0 ? s.SequenceNo : i + 1,
                        label = string.IsNullOrWhiteSpace(s.Label) ? $"Step {i + 1}" : s.Label,
                        instrument = s.Instrument.ToString(),
                        target = s.Target,
                        tolerancePlus = s.TolerancePlus,
                        toleranceMinus = s.ToleranceMinus,
                        toleranceMode = s.ToleranceMode.ToString(),
                        markerX = s.MarkerX,
                        markerY = s.MarkerY,
                        unit = s.Instrument == InstrumentType.Torque ? "N·m" : "mm"
                    }).ToList()
                }
            });
        }

        return Ok(new
        {
            isSimilarFound = false,
            generatedCode
        });
    }

    // ================================================================
    // CREATE INLINE TEMPLATE (AJAX) POST /Product/CreateInlineTemplate
    // ================================================================
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CreateInlineTemplate([FromBody] CreateInlineTemplateRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name) || request.Steps == null || request.Steps.Count == 0)
        {
            return BadRequest(new { error = "Template name and at least one step are required." });
        }

        var stepSpecs = request.Steps.Select(s => new TemplateCodeGenerator.StepSpec(
            s.Instrument, s.Target, s.TolerancePlus, s.ToleranceMinus, s.ToleranceMode)).ToList();

        var generatedCode = TemplateCodeGenerator.Generate(request.Mode, stepSpecs);

        var stepRequests = request.Steps.Select((s, idx) => new StepRequest(
            SequenceNo: s.SequenceNo > 0 ? s.SequenceNo : idx + 1,
            Instrument: s.Instrument,
            Label: string.IsNullOrWhiteSpace(s.Label) ? $"Bolt {idx + 1}" : s.Label.Trim(),
            Target: s.Target,
            TolerancePlus: s.TolerancePlus,
            ToleranceMinus: s.ToleranceMinus,
            ToleranceMode: s.ToleranceMode,
            MaxRetries: s.MaxRetries <= 0 ? 3 : s.MaxRetries,
            MarkerX: s.MarkerX,
            MarkerY: s.MarkerY)).ToList();

        var createReq = new CreateTemplateRequest(
            TemplateCode: generatedCode,
            Name: request.Name.Trim(),
            Description: request.Description,
            Mode: request.Mode,
            ImagePath: request.ImagePath,
            Steps: stepRequests);

        var userName = User.Identity?.Name ?? "system";
        var created = await _templates.CreateAsync(createReq, userName);

        return Ok(new
        {
            success = true,
            templateId = created.TemplateId,
            templateCode = created.TemplateCode,
            name = created.Name,
            version = created.Version,
            stepCount = created.Steps.Count,
            imagePath = created.ImagePath,
            label = $"{created.TemplateCode} · {created.Name} · v{created.Version} · {created.Steps.Count} steps"
        });
    }


    // GET /Product/Detail/{id}
    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var product = await _products.GetByIdAsync(id);
        if (product == null) return NotFound();

        var latestCodes = await _products.GetLatestTemplatesAsync();
        var latestByCode = latestCodes.ToDictionary(t => t.TemplateCode, t => t.TemplateId, StringComparer.OrdinalIgnoreCase);

        var vm = new ProductDetailViewModel
        {
            ProductId = product.ProductId,
            ProductCode = product.ProductCode,
            ProductName = product.ProductName,
            Description = product.Description,
            ImagePath = product.ImagePath,
            IsActive = product.IsActive,
            CreatedBy = product.CreatedBy,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            Components = product.Components.OrderBy(c => c.AssemblyOrder).Select(c => new ProductComponentDetailViewModel
            {
                ProductComponentId = c.ProductComponentId,
                AssemblyOrder = c.AssemblyOrder,
                ComponentName = c.ComponentName,
                Quantity = c.Quantity,
                TemplateId = c.TemplateId,
                TemplateCode = c.Template.TemplateCode,
                TemplateName = c.Template.Name,
                TemplateVersion = c.Template.Version,
                IsLatestVersion = !latestByCode.TryGetValue(c.Template.TemplateCode, out var latestId) || latestId == c.TemplateId,
                Mode = c.Template.Mode,
                Steps = c.Template.Steps.OrderBy(s => s.SequenceNo).Select(s =>
                {
                    var (lo, hi) = _evaluator.ComputeBand(s.Target, s.ToleranceMinus, s.TolerancePlus, s.ToleranceMode);
                    return new ProductStepViewModel
                    {
                        SequenceNo = s.SequenceNo,
                        Label = s.Label,
                        Instrument = s.Instrument,
                        Target = s.Target,
                        Lower = lo,
                        Upper = hi,
                        ToleranceMode = s.ToleranceMode,
                        MaxRetries = s.MaxRetries
                    };
                }).ToList()
            }).ToList()
        };

        ViewData["Title"] = $"{product.ProductName} — Product";
        ViewData["ActiveNav"] = "Products";
        return View(vm);
    }

    // GET /Product/DetailByCode?code={code}
    [HttpGet]
    public async Task<IActionResult> DetailByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return NotFound();

        var product = await _products.GetByCodeAsync(code.Trim());
        if (product != null)
        {
            return RedirectToAction(nameof(Detail), new { id = product.ProductId });
        }

        TempData["Error"] = $"Product with item code '{code}' has not been configured yet.";
        return RedirectToAction(nameof(Index), new { search = code });
    }

    // POST /Product/Clone/{id}
    [HttpPost]
    public async Task<IActionResult> Clone(int id)
    {
        var clone = await _products.CloneAsync(id, UserName());
        TempData["Success"] = $"Cloned to '{clone.ProductCode}'.";
        return RedirectToAction(nameof(Edit), new { id = clone.ProductId });
    }

    // POST /Product/SetActive
    [HttpPost]
    public async Task<IActionResult> SetActive(int id, bool? isActive = null, string? returnUrl = null)
    {
        if (!isActive.HasValue)
        {
            var product = await _products.GetByIdAsync(id);
            if (product != null)
            {
                isActive = !product.IsActive;
            }
        }

        var activeValue = isActive ?? true;
        await _products.SetActiveAsync(id, activeValue, UserName());
        TempData["Success"] = activeValue ? "Product activated." : "Product deactivated.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    // GET /Product/UpdateComponent/{productComponentId}
    [HttpGet]
    public async Task<IActionResult> UpdateComponent(int productComponentId)
    {
        var preview = await _products.PreviewUpdateToLatestAsync(productComponentId);
        var product = await _products.GetByIdAsync(preview.ProductId);
        if (product == null) return NotFound();

        var vm = new ComponentUpdateViewModel
        {
            ProductComponentId = preview.ProductComponentId,
            ProductId = preview.ProductId,
            ProductName = product.ProductName,
            ComponentName = preview.ComponentName,
            OldVersion = preview.OldVersion,
            NewVersion = preview.NewVersion,
            IsAlreadyLatest = preview.IsAlreadyLatest,
            Steps = BuildStepDiff(preview.OldSteps, preview.NewSteps)
        };

        ViewData["Title"] = $"Update {preview.ComponentName}";
        ViewData["ActiveNav"] = "Products";
        return View(vm);
    }

    // POST /Product/ApplyComponentUpdate
    [HttpPost]
    public async Task<IActionResult> ApplyComponentUpdate(int productComponentId)
    {
        var product = await _products.ApplyUpdateToLatestAsync(productComponentId, UserName());
        TempData["Success"] = "Component updated to the latest template version.";
        return RedirectToAction(nameof(Detail), new { id = product.ProductId });
    }

    // ================================================================
    // HELPERS
    // ================================================================
    private string UserName() => User.Identity?.Name ?? "system";

    private async Task<ProductBuilderViewModel> BuildBuilderViewModelAsync(Product? existing)
    {
        var vm = new ProductBuilderViewModel
        {
            EditingProductId = existing?.ProductId,
            ProductCode = existing?.ProductCode ?? string.Empty,
            ProductName = existing?.ProductName ?? string.Empty,
            Description = existing?.Description,
            ImagePath = existing?.ImagePath
        };

        await PopulateOptionsAsync(vm);

        if (existing != null)
        {
            vm.Components = existing.Components.OrderBy(c => c.AssemblyOrder).Select(c => new ProductComponentRowViewModel
            {
                ProductComponentId = c.ProductComponentId,
                TemplateId = c.TemplateId,
                ComponentName = c.ComponentName,
                Quantity = c.Quantity,
                TemplateCode = c.Template.TemplateCode,
                TemplateName = c.Template.Name,
                TemplateVersion = c.Template.Version,
                StepCount = c.Template.Steps.Count
            }).ToList();
        }

        // Start a new product with one initial row for convenience.
        if (vm.Components.Count == 0 && vm.AvailableTemplates.Count > 0)
        {
            var first = vm.AvailableTemplates[0];
            vm.Components.Add(new ProductComponentRowViewModel
            {
                TemplateId = first.TemplateId,
                ComponentName = first.Name,
                Quantity = 1,
                TemplateCode = first.TemplateCode,
                TemplateName = first.Name,
                TemplateVersion = first.Version,
                StepCount = first.StepCount
            });
        }

        return vm;
    }

    private async Task PopulateOptionsAsync(ProductBuilderViewModel model)
    {
        var templates = await _products.GetLatestTemplatesAsync();
        model.AvailableTemplates = templates.Select(t => new TemplateOptionViewModel
        {
            TemplateId = t.TemplateId,
            TemplateCode = t.TemplateCode,
            Name = t.Name,
            Version = t.Version,
            Mode = t.Mode,
            StepCount = t.Steps.Count
        }).ToList();

        // Fill display-only fields for rows refreshed on a validation error or submission.
        var byId = model.AvailableTemplates.ToDictionary(t => t.TemplateId);
        foreach (var row in model.Components)
        {
            if (byId.TryGetValue(row.TemplateId, out var opt))
            {
                row.TemplateCode = opt.TemplateCode;
                row.TemplateName = opt.Name;
                row.TemplateVersion = opt.Version;
                row.StepCount = opt.StepCount;
                if (string.IsNullOrWhiteSpace(row.ComponentName))
                {
                    row.ComponentName = opt.Name;
                }
            }
        }
    }

    private static List<ProductComponentRequest> NormalizeComponents(ProductBuilderViewModel model)
    {
        var templateMap = (model.AvailableTemplates ?? []).ToDictionary(t => t.TemplateId);
        return (model.Components ?? [])
            .Where(c => c.TemplateId > 0)
            .Select(c =>
            {
                var name = !string.IsNullOrWhiteSpace(c.ComponentName)
                    ? c.ComponentName.Trim()
                    : (templateMap.TryGetValue(c.TemplateId, out var opt) ? opt.Name : (!string.IsNullOrWhiteSpace(c.TemplateName) ? c.TemplateName : $"Component {c.TemplateId}"));
                return new ProductComponentRequest(c.TemplateId, name, c.Quantity < 1 ? 1 : c.Quantity, c.ProductComponentId);
            })
            .ToList();
    }

    private static List<StepDiffViewModel> BuildStepDiff(List<StepPreview> oldSteps, List<StepPreview> newSteps)
    {
        var sequences = oldSteps.Select(s => s.SequenceNo)
            .Union(newSteps.Select(s => s.SequenceNo))
            .OrderBy(n => n)
            .ToList();

        var rows = new List<StepDiffViewModel>();
        foreach (var seq in sequences)
        {
            var oldStep = oldSteps.FirstOrDefault(s => s.SequenceNo == seq);
            var newStep = newSteps.FirstOrDefault(s => s.SequenceNo == seq);

            var changed = oldStep == null || newStep == null
                || !string.Equals(oldStep.Label, newStep.Label, StringComparison.Ordinal)
                || oldStep.Instrument != newStep.Instrument
                || oldStep.Target != newStep.Target
                || oldStep.TolerancePlus != newStep.TolerancePlus
                || oldStep.ToleranceMinus != newStep.ToleranceMinus
                || oldStep.ToleranceMode != newStep.ToleranceMode;

            rows.Add(new StepDiffViewModel
            {
                SequenceNo = seq,
                Old = oldStep == null ? null : ToStepVm(oldStep),
                New = newStep == null ? null : ToStepVm(newStep),
                IsChanged = changed
            });
        }
        return rows;
    }

    private static ProductStepViewModel ToStepVm(StepPreview s) => new()
    {
        SequenceNo = s.SequenceNo,
        Label = s.Label,
        Instrument = s.Instrument,
        Target = s.Target,
        ToleranceMode = s.ToleranceMode
    };
}