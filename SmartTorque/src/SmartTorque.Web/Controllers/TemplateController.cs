using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SmartTorque.Core.Enums;
using SmartTorque.Core.Interfaces;
using SmartTorque.Services;
using SmartTorque.Web.ViewModels.Template;

namespace SmartTorque.Web.Controllers;

[Authorize(Roles = "Admin")]
public class TemplateController : Controller
{
    private readonly ITemplateService _templates;
    private readonly IImageUploadService _images;
    private readonly IMeasurementEvaluator _evaluator;
    private readonly ISettingsService _settings;
    private readonly ILogger<TemplateController> _logger;

    public TemplateController(
        ITemplateService templates,
        IImageUploadService images,
        IMeasurementEvaluator evaluator,
        ISettingsService settings,
        ILogger<TemplateController> logger)
    {
        _templates = templates;
        _images = images;
        _evaluator = evaluator;
        _settings = settings;
        _logger = logger;
    }

    // ================================================================
    // LIST  GET /Template
    // ================================================================
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        TemplateMode? mode,
        InstrumentType? instrument,
        bool? active,
        int page = 1)
    {
        var filter = new TemplateFilter(search, mode, instrument, active, page, 25);
        var items = await _templates.ListAsync(filter);

        // Total count for paging (without Skip/Take — lightweight second query)
        var totalFilter = filter with { Page = 1, PageSize = int.MaxValue };
        var allItems = await _templates.ListAsync(totalFilter);

        var vm = new TemplateListViewModel
        {
            Items = items.Select(t => new TemplateRowViewModel
            {
                TemplateId = t.TemplateId,
                TemplateCode = t.TemplateCode,
                Name = t.Name,
                Mode = t.Mode,
                Version = t.Version,
                StepCount = t.Steps.Count,
                IsActive = t.IsActive,
                CreatedAt = t.CreatedAt
            }).ToList(),
            TotalCount = allItems.Count,
            Page = page,
            PageSize = 25,
            Search = search,
            ModeFilter = mode,
            InstrumentFilter = instrument,
            ActiveFilter = active
        };

        ViewData["Title"] = "Templates";
        ViewData["ActiveNav"] = "Templates";
        return View(vm);
    }

    // ================================================================
    // WIZARD STAGE 1  GET /Template/Create  (or /Template/Edit/{id})
    // ================================================================
    [HttpGet]
    public async Task<IActionResult> Create(int? id)
    {
        Stage1ViewModel vm;
        if (id.HasValue)
        {
            var t = await _templates.GetByIdAsync(id.Value);
            if (t == null) return NotFound();
            vm = new Stage1ViewModel
            {
                TemplateCode = t.TemplateCode,
                Name = t.Name,
                Description = t.Description,
                Mode = t.Mode,
                EditingTemplateId = t.TemplateId
            };
        }
        else
        {
            vm = new Stage1ViewModel();
        }
        ViewData["Title"] = id.HasValue ? "Edit Template — Step 1" : "New Template — Step 1";
        ViewData["ActiveNav"] = "Templates";
        return View("WizardStage1", vm);
    }

    // POST Stage 1 → Stage 2
    [HttpPost]
    public async Task<IActionResult> Stage1(Stage1ViewModel model)
    {
        if (model.EditingTemplateId.HasValue && !string.IsNullOrWhiteSpace(model.TemplateCode))
        {
            var codeUnique = await _templates.IsCodeUniqueAsync(
                model.TemplateCode, model.EditingTemplateId);
            if (!codeUnique)
                model.CodeAlreadyExists = true;
        }

        // Safeguard: If Mode is 0, recover from database or default to Torque
        if ((int)model.Mode == 0)
        {
            if (model.EditingTemplateId.HasValue)
            {
                var existingT = await _templates.GetByIdAsync(model.EditingTemplateId.Value);
                model.Mode = existingT?.Mode ?? TemplateMode.Torque;
            }
            else
            {
                model.Mode = TemplateMode.Torque;
            }
            ModelState.Remove(nameof(model.Mode));
        }

        if (!ModelState.IsValid || model.CodeAlreadyExists)
        {
            ViewData["Title"] = "Template — Step 1";
            ViewData["ActiveNav"] = "Templates";
            return View("WizardStage1", model);
        }

        // Pre-populate Stage 2 from existing template if editing
        var vm = new Stage2ViewModel
        {
            TemplateCode = (model.TemplateCode ?? string.Empty).ToUpperInvariant(),
            Name = model.Name,
            Description = model.Description,
            Mode = model.Mode,
            EditingTemplateId = model.EditingTemplateId
        };

        if (model.EditingTemplateId.HasValue)
        {
            var t = await _templates.GetByIdAsync(model.EditingTemplateId.Value);
            if (t != null)
            {
                vm.StepCount = Math.Max(1, t.Steps.Count);
                vm.ImagePath = t.ImagePath;
                if ((int)vm.Mode == 0)
                {
                    vm.Mode = t.Mode;
                }
            }
        }

        if (vm.StepCount < 1) vm.StepCount = 1;
        if ((int)vm.Mode == 0) vm.Mode = TemplateMode.Torque;

        // Clear ModelState so Stage 2 tag-helpers render with vm values
        ModelState.Clear();

        ViewData["Title"] = "Template — Step 2";
        ViewData["ActiveNav"] = "Templates";
        return View("WizardStage2", vm);
    }

    // ================================================================
    // WIZARD STAGE 2 — Image Upload (AJAX) POST /Template/UploadImage
    // ================================================================
    [HttpPost]
    public async Task<IActionResult> UploadImage(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        try
        {
            await using var stream = file.OpenReadStream();
            var maxMb = Math.Max(1, await _settings.GetAsync<int>(SettingsService.KeyImageMaxMb) ?? 5);
            var path = await _images.SaveAsync(stream, file.FileName, file.Length, maxBytes: maxMb * 1024L * 1024L);
            return Ok(new { path, url = $"/{path}" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST Stage 2 → Stage 3
    [HttpPost]
    public async Task<IActionResult> Stage2(Stage2ViewModel model)
    {
        ModelState.Remove(nameof(model.TemplateCode));

        // Safeguard: If Mode is 0 or failed enum binding, recover from editing template or default to Torque
        if ((int)model.Mode == 0 || (ModelState.TryGetValue(nameof(model.Mode), out var modeEntry) && modeEntry.Errors.Count > 0))
        {
            if (model.EditingTemplateId.HasValue)
            {
                var existingT = await _templates.GetByIdAsync(model.EditingTemplateId.Value);
                model.Mode = existingT?.Mode ?? TemplateMode.Torque;
            }
            else
            {
                model.Mode = TemplateMode.Torque;
            }
            ModelState.Remove(nameof(model.Mode));
        }

        // Safeguard: If StepCount is less than 1 or failed binding, recover from editing template or default to 1
        if (model.StepCount < 1 || (ModelState.TryGetValue(nameof(model.StepCount), out var scEntry) && scEntry.Errors.Count > 0))
        {
            if (model.EditingTemplateId.HasValue)
            {
                var existingT = await _templates.GetByIdAsync(model.EditingTemplateId.Value);
                model.StepCount = Math.Max(1, existingT?.Steps.Count ?? 1);
            }
            else
            {
                model.StepCount = 1;
            }
            ModelState.Remove(nameof(model.StepCount));
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Template — Step 2";
            ViewData["ActiveNav"] = "Templates";
            return View("WizardStage2", model);
        }

        // Build step rows: pre-fill from existing if editing, else empty rows
        List<StepRowViewModel> steps;
        if (model.EditingTemplateId.HasValue)
        {
            var existing = await _templates.GetByIdAsync(model.EditingTemplateId.Value);
            if (existing != null && existing.Steps.Count == model.StepCount)
            {
                steps = existing.Steps.OrderBy(s => s.SequenceNo).Select(s => new StepRowViewModel
                {
                    SequenceNo = s.SequenceNo,
                    Instrument = s.Instrument,
                    Label = s.Label,
                    Target = s.Target,
                    TolerancePlus = s.TolerancePlus,
                    ToleranceMinus = s.ToleranceMinus,
                    ToleranceMode = s.ToleranceMode,
                    MaxRetries = s.MaxRetries,
                    MarkerX = s.MarkerX,
                    MarkerY = s.MarkerY
                }).ToList();
            }
            else
            {
                steps = await BuildEmptyStepsAsync(model.StepCount, model.Mode);
            }
        }
        else
        {
            steps = await BuildEmptyStepsAsync(model.StepCount, model.Mode);
        }

        var stepSpecs = steps.Select(s => new TemplateCodeGenerator.StepSpec(
            s.Instrument, s.Target, s.TolerancePlus, s.ToleranceMinus, s.ToleranceMode));
        var initialCode = TemplateCodeGenerator.Generate(model.Mode, stepSpecs);

        var vm = new Stage3ViewModel
        {
            TemplateCode = string.IsNullOrWhiteSpace(model.TemplateCode) ? initialCode : model.TemplateCode,
            GeneratedTemplateCode = initialCode,
            Name = model.Name,
            Description = model.Description,
            Mode = model.Mode,
            ImagePath = model.ImagePath,
            EditingTemplateId = model.EditingTemplateId,
            Steps = steps
        };

        ViewData["Title"] = "Template — Step 3";
        ViewData["ActiveNav"] = "Templates";
        return View("WizardStage3", vm);
    }

    // ================================================================
    // GENERATE CODE & CHECK DUPLICATE (AJAX) POST /Template/GenerateAndCheckCode
    // ================================================================
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> GenerateAndCheckCode([FromBody] GenerateCodeRequest request)
    {
        if (request == null)
            return BadRequest(new { error = "Invalid request." });

        var steps = request.Steps.Select(s => new TemplateCodeGenerator.StepSpec(
            s.Instrument, s.Target, s.TolerancePlus, s.ToleranceMinus, s.ToleranceMode));

        var code = TemplateCodeGenerator.Generate(request.Mode, steps);

        var existing = await _templates.GetLatestByCodeAsync(code);
        bool isDuplicate = existing != null && existing.TemplateId != request.EditingTemplateId;

        if (isDuplicate && existing != null)
        {
            return Ok(new
            {
                code,
                exists = true,
                template = new
                {
                    templateId = existing.TemplateId,
                    templateCode = existing.TemplateCode,
                    name = existing.Name,
                    description = existing.Description,
                    mode = existing.Mode.ToString(),
                    version = existing.Version,
                    stepCount = existing.Steps.Count,
                    steps = existing.Steps.OrderBy(s => s.SequenceNo).Select(s => new
                    {
                        sequenceNo = s.SequenceNo,
                        label = s.Label,
                        instrument = s.Instrument.ToString(),
                        target = s.Target,
                        tolerancePlus = s.TolerancePlus,
                        toleranceMinus = s.ToleranceMinus,
                        toleranceMode = s.ToleranceMode.ToString(),
                        unit = s.Instrument == InstrumentType.Torque ? "N·m" : "mm"
                    })
                }
            });
        }

        return Ok(new { code, exists = false });
    }

    // ================================================================
    // WIZARD STAGE 3 — Save  POST /Template/Save
    // ================================================================
    [HttpPost]
    public async Task<IActionResult> Save(Stage3ViewModel model, bool saveAnyway = false)
    {
        if ((int)model.Mode == 0)
        {
            if (model.EditingTemplateId.HasValue)
            {
                var orig = await _templates.GetByIdAsync(model.EditingTemplateId.Value);
                model.Mode = orig?.Mode ?? TemplateMode.Torque;
            }
            else
            {
                model.Mode = TemplateMode.Torque;
            }
            ModelState.Remove(nameof(model.Mode));
        }

        // Generate deterministic template code from instrument specifications
        var generatedCode = TemplateCodeGenerator.Generate(
            model.Mode,
            model.Steps.Select(s => new TemplateCodeGenerator.StepSpec(
                s.Instrument, s.Target, s.TolerancePlus, s.ToleranceMinus, s.ToleranceMode)));

        model.GeneratedTemplateCode = generatedCode;
        model.TemplateCode = generatedCode;
        ModelState.Remove(nameof(model.TemplateCode));

        // Check if an existing template with this code already exists
        var existing = await _templates.GetLatestByCodeAsync(generatedCode);
        if (existing != null && existing.TemplateId != model.EditingTemplateId && !saveAnyway)
        {
            model.ExistingMatchingTemplate = new MatchingTemplateViewModel
            {
                TemplateId = existing.TemplateId,
                TemplateCode = existing.TemplateCode,
                Name = existing.Name,
                Description = existing.Description,
                Mode = existing.Mode,
                Version = existing.Version,
                StepCount = existing.Steps.Count,
                Steps = existing.Steps.OrderBy(s => s.SequenceNo).Select(s => new MatchingStepViewModel
                {
                    SequenceNo = s.SequenceNo,
                    Label = s.Label,
                    Instrument = s.Instrument,
                    Target = s.Target,
                    TolerancePlus = s.TolerancePlus,
                    ToleranceMinus = s.ToleranceMinus,
                    ToleranceMode = s.ToleranceMode
                }).ToList()
            };

            ViewBag.MatchingNotice = $"Template '{existing.Name}' ({generatedCode}) already exists. You can use it in Product Builder, or click below to save as a new revision.";

            ViewData["Title"] = "Template — Step 3";
            ViewData["ActiveNav"] = "Templates";
            return View("WizardStage3", model);
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Template — Step 3";
            ViewData["ActiveNav"] = "Templates";
            return View("WizardStage3", model);
        }

        var request = new CreateTemplateRequest(
            TemplateCode: generatedCode,
            Name: model.Name,
            Description: model.Description,
            Mode: model.Mode,
            ImagePath: model.ImagePath,
            Steps: model.Steps.Select(s => new StepRequest(
                s.SequenceNo, s.Instrument, s.Label,
                s.Target, s.TolerancePlus, s.ToleranceMinus,
                s.ToleranceMode, s.MaxRetries,
                s.MarkerX, s.MarkerY)).ToList());

        var userName = User.Identity?.Name ?? "system";
        SmartTorque.Core.Entities.Template saved;

        if (model.EditingTemplateId.HasValue)
        {
            saved = await _templates.EditAsync(model.EditingTemplateId.Value, request, userName);
            _logger.LogInformation("Template {Code} v{Ver} saved by {User}",
                saved.TemplateCode, saved.Version, userName);
        }
        else
        {
            saved = await _templates.CreateAsync(request, userName);
            _logger.LogInformation("Template {Code} v{Ver} created by {User}",
                saved.TemplateCode, saved.Version, userName);
        }

        TempData["Success"] = $"Template '{saved.Name}' ({saved.TemplateCode}) saved successfully.";
        return RedirectToAction(nameof(Detail), new { id = saved.TemplateId });
    }

    // ================================================================
    // DETAIL / PREVIEW  GET /Template/Detail/{id}
    // ================================================================
    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var t = await _templates.GetByIdAsync(id);
        if (t == null) return NotFound();

        var steps = t.Steps.OrderBy(s => s.SequenceNo).Select(s =>
        {
            var (lo, hi) = _evaluator.ComputeBand(s.Target, s.ToleranceMinus, s.TolerancePlus, s.ToleranceMode);
            return new StepDetailViewModel
            {
                SequenceNo = s.SequenceNo,
                Label = s.Label,
                Instrument = s.Instrument,
                Target = s.Target,
                Lower = lo,
                Upper = hi,
                ToleranceMode = s.ToleranceMode,
                MaxRetries = s.MaxRetries,
                MarkerX = s.MarkerX,
                MarkerY = s.MarkerY
            };
        }).ToList();

        // All versions of this template code (v1, v2, etc.)
        var allVersions = await _templates.GetAllVersionsByCodeAsync(t.TemplateCode);
        var versions = allVersions
            .Select(ot => new VersionRowViewModel
            {
                TemplateId = ot.TemplateId,
                Version = ot.Version,
                IsLatest = ot.IsLatest,
                CreatedAt = ot.CreatedAt,
                CreatedBy = ot.CreatedBy
            })
            .ToList();

        var vm = new TemplateDetailViewModel
        {
            TemplateId = t.TemplateId,
            TemplateCode = t.TemplateCode,
            Name = t.Name,
            Description = t.Description,
            Mode = t.Mode,
            ImagePath = t.ImagePath,
            Version = t.Version,
            IsLatest = t.IsLatest,
            IsActive = t.IsActive,
            CreatedBy = t.CreatedBy,
            CreatedAt = t.CreatedAt,
            Steps = steps,
            OtherVersions = versions
        };

        ViewData["Title"] = $"{t.Name} — Preview";
        ViewData["ActiveNav"] = "Templates";
        return View(vm);
    }

    // ================================================================
    // TOGGLE ACTIVE  POST /Template/SetActive
    // ================================================================
    [HttpPost]
    public async Task<IActionResult> SetActive(int id, bool? isActive = null, string? returnUrl = null)
    {
        if (!isActive.HasValue)
        {
            var template = await _templates.GetByIdAsync(id);
            if (template != null)
            {
                isActive = !template.IsActive;
            }
        }

        var newStatus = isActive ?? true;
        await _templates.SetActiveAsync(id, newStatus, User.Identity?.Name ?? "system");
        TempData["Success"] = newStatus ? "Template activated." : "Template deactivated.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    // ================================================================
    // HELPERS
    // ================================================================
    private async Task<List<StepRowViewModel>> BuildEmptyStepsAsync(int count, TemplateMode mode)
    {
        var defaultInstrument = mode switch
        {
            TemplateMode.Vernier => InstrumentType.Vernier,
            _ => InstrumentType.Torque
        };

        // Pre-fill from the Admin-editable default (Settings key MaxRetriesDefault).
        // Individual steps may still override MaxRetries in Stage 3.
        var defaultRetries = Math.Max(
            SettingsService.MaxRetriesMin,
            Math.Min(
                SettingsService.MaxRetriesMax,
                await _settings.GetAsync<int>(SettingsService.KeyMaxRetriesDefault) ?? 3));

        return Enumerable.Range(1, count).Select(i => new StepRowViewModel
        {
            SequenceNo = i,
            Instrument = defaultInstrument,
            Label = $"Step {i}",
            MaxRetries = defaultRetries
        }).ToList();
    }
}