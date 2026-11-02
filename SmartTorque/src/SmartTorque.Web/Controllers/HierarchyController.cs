using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;

namespace SmartTorque.Web.Controllers;

[Authorize(Roles = "Admin")]
public class HierarchyController : Controller
{
    private readonly IHierarchyService _hierarchy;
    private readonly IUserContextService _userContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly ILogger<HierarchyController> _logger;

    public HierarchyController(
        IHierarchyService hierarchy,
        IUserContextService userContext,
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        ILogger<HierarchyController> logger)
    {
        _hierarchy = hierarchy;
        _userContext = userContext;
        _userManager = userManager;
        _db = db;
        _logger = logger;
    }

    // GET /Hierarchy/Canvas
    [HttpGet]
    [Route("Hierarchy/Canvas")]
    public async Task<IActionResult> Canvas()
    {
        var ctx = await _userContext.GetCurrentUserContextAsync(User);
        if (ctx != null && !ctx.HasFullPlantAccess && !ctx.IsMainAdmin)
        {
            return Forbid();
        }

        ViewData["Title"] = "Hierarchy Canvas";
        ViewData["ActiveNav"] = "Hierarchy";

        var currentUser = await _userManager.GetUserAsync(User);
        var currentUserId = currentUser?.Id;
        var pendingRequests = await _hierarchy.GetPendingRequestsAsync(currentUserId);
        ViewBag.PendingRequestsCount = pendingRequests.Count;

        return View("~/Views/Hierarchy/Canvas.cshtml");
    }

    // GET /api/v1/hierarchy/tree
    [HttpGet]
    [Route("api/v1/hierarchy/tree")]
    public async Task<IActionResult> GetTree(int? rootNodeId = null)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var currentUserId = currentUser?.Id;

        // Make sure graph is seeded if empty
        await _hierarchy.SeedHierarchyGraphFromLegacyAsync();

        var graph = await _hierarchy.GetHierarchyGraphAsync(rootNodeId, currentUserId);
        return Json(new { success = true, nodes = graph });
    }

    // GET /api/v1/hierarchy/requests
    [HttpGet]
    [Route("api/v1/hierarchy/requests")]
    public async Task<IActionResult> GetPendingRequests()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var currentUserId = currentUser?.Id;
        var requests = await _hierarchy.GetPendingRequestsAsync(currentUserId);
        return Json(new { success = true, requests });
    }

    public record CreateNodeRequest(string Name, HierarchyNodeType NodeType, int? ParentNodeId, string? Code, string? AdminUserId);

    // POST /api/v1/hierarchy/create
    [HttpPost]
    [Route("api/v1/hierarchy/create")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CreateNode([FromBody] CreateNodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { success = false, message = "Node name is required." });

        try
        {
            var node = await _hierarchy.CreateNodeAsync(request.Name, request.NodeType, request.ParentNodeId, request.Code, request.AdminUserId);
            return Json(new { success = true, node = new { node.NodeId, node.Name, node.Code, node.NodeType, node.Depth, node.Path } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create node: {Name}", request.Name);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    public record LinkNodeRequest(int NodeId, int? NewParentNodeId);

    // POST /api/v1/hierarchy/link
    [HttpPost]
    [Route("api/v1/hierarchy/link")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> LinkNode([FromBody] LinkNodeRequest request)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var currentUserId = currentUser?.Id ?? "System";

        try
        {
            var success = await _hierarchy.AttachOrLinkNodeAsync(request.NodeId, request.NewParentNodeId, currentUserId);
            if (!success)
                return BadRequest(new { success = false, message = "Could not link node. Verify IDs." });

            return Json(new { success = true, message = "Node successfully linked." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to link node {NodeId} to {ParentId}", request.NodeId, request.NewParentNodeId);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    // POST /api/v1/hierarchy/delete/{id}
    [HttpPost]
    [Route("api/v1/hierarchy/delete/{id:int}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DeleteNode(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var currentUserId = currentUser?.Id ?? "System";

        try
        {
            var success = await _hierarchy.DeleteNodeAsync(id, currentUserId);
            if (!success)
                return NotFound(new { success = false, message = "Node not found or already deleted." });

            _logger.LogInformation("Hierarchy node {NodeId} deleted by user {User}", id, currentUserId);
            return Json(new { success = true, message = "Node deleted and its child branches were re-attached to the parent." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete node {NodeId}", id);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    // POST /api/v1/hierarchy/requests/{id}/approve
    [HttpPost]
    [Route("api/v1/hierarchy/requests/{id:int}/approve")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ApproveRequest(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var currentUserId = currentUser?.Id ?? "System";

        try
        {
            var createdNode = await _hierarchy.ApproveChildNodeRequestAsync(id, currentUserId);
            return Json(new { success = true, message = "Request approved and node created.", node = createdNode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve request {RequestId}", id);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    public record RejectRequestModel(string? Reason);

    // POST /api/v1/hierarchy/requests/{id}/reject
    [HttpPost]
    [Route("api/v1/hierarchy/requests/{id:int}/reject")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RejectRequest(int id, [FromBody] RejectRequestModel? model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var currentUserId = currentUser?.Id ?? "System";

        try
        {
            var success = await _hierarchy.RejectChildNodeRequestAsync(id, currentUserId, model?.Reason ?? "Declined by parent administrator");
            return Json(new { success, message = success ? "Request rejected." : "Request not found or already processed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject request {RequestId}", id);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // GET /api/v1/hierarchy/search
    [HttpGet]
    [Route("api/v1/hierarchy/search")]
    public async Task<IActionResult> Search(string? term, HierarchyNodeType? type = null, int? parentNodeId = null)
    {
        var results = await _hierarchy.SearchNodesAsync(term ?? string.Empty, type, parentNodeId);
        return Json(new { success = true, results = results.Select(r => new { r.NodeId, r.Code, r.Name, r.NodeType, r.ParentNodeId, r.Depth }) });
    }
}