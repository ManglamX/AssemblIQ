using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SmartTorque.Services;
using SmartTorque.Web.ViewModels.ColourMap;

namespace SmartTorque.Web.Controllers;

/// <summary>
/// Colour map admin screen (PRD BM-3, US-A5): add / edit / deactivate the shades
/// that the BM importer matches row colours against (Architecture §7.3).
/// </summary>
[Authorize(Roles = "Admin")]
public class ColourMapController : Controller
{
    private readonly IColourMapService _colourMap;
    private readonly ILogger<ColourMapController> _logger;

    public ColourMapController(IColourMapService colourMap, ILogger<ColourMapController> logger)
    {
        _colourMap = colourMap;
        _logger = logger;
    }

    // GET /ColourMap
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var vm = await BuildListAsync(new ColourMapFormViewModel());
        ViewData["Title"] = "Colour map";
        ViewData["ActiveNav"] = "Workload";
        return View(vm);
    }

    // GET /ColourMap/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entry = await _colourMap.GetByIdAsync(id);
        if (entry == null) return NotFound();

        var vm = await BuildListAsync(new ColourMapFormViewModel
        {
            EditingMapId = entry.MapId,
            ColourHex = entry.ColourHex,
            Priority = (int)entry.Priority,
            Label = entry.Label,
            MatchTolerance = entry.MatchTolerance
        });

        ViewData["Title"] = "Colour map";
        ViewData["ActiveNav"] = "Workload";
        return View("Index", vm);
    }

    // POST /ColourMap/Create
    [HttpPost]
    public async Task<IActionResult> Create(ColourMapFormViewModel model)
    {
        try
        {
            await _colourMap.CreateAsync(ToRequest(model), UserName());
            _logger.LogInformation("Colour map shade {Hex} created by {User}", model.ColourHex, UserName());
            TempData["Success"] = "Shade added.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await RenderWithErrorAsync(model);
        }
    }

    // POST /ColourMap/Update
    [HttpPost]
    public async Task<IActionResult> Update(ColourMapFormViewModel model)
    {
        if (!model.EditingMapId.HasValue) return BadRequest();

        try
        {
            await _colourMap.UpdateAsync(model.EditingMapId.Value, ToRequest(model), UserName());
            _logger.LogInformation("Colour map shade {Hex} updated by {User}", model.ColourHex, UserName());
            TempData["Success"] = "Shade updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await RenderWithErrorAsync(model);
        }
    }

    // POST /ColourMap/SetActive
    [HttpPost]
    public async Task<IActionResult> SetActive(int id, bool isActive)
    {
        try
        {
            await _colourMap.SetActiveAsync(id, isActive, UserName());
            TempData["Success"] = isActive ? "Shade restored." : "Shade deactivated.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    // ================================================================
    // HELPERS
    // ================================================================
    private string UserName() => User.Identity?.Name ?? "system";

    private static ColourMapRequest ToRequest(ColourMapFormViewModel model)
        => new(
            model.ColourHex,
            (Core.Enums.PriorityLevel)model.Priority,
            model.Label,
            model.MatchTolerance);

    private async Task<IActionResult> RenderWithErrorAsync(ColourMapFormViewModel model)
    {
        var vm = await BuildListAsync(model);
        ViewData["Title"] = "Colour map";
        ViewData["ActiveNav"] = "Workload";
        return View("Index", vm);
    }

    private async Task<ColourMapListViewModel> BuildListAsync(ColourMapFormViewModel form)
    {
        var entries = await _colourMap.ListAsync(includeInactive: true);

        return new ColourMapListViewModel
        {
            Form = form,
            Items = entries.Select(e => new ColourMapRowViewModel
            {
                MapId = e.MapId,
                ColourHex = e.ColourHex,
                Priority = e.Priority,
                Label = e.Label,
                MatchTolerance = e.MatchTolerance,
                IsActive = e.IsActive
            }).ToList()
        };
    }
}