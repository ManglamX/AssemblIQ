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
        if (ctx != null && !ctx.HasFullPlantAccess && !ctx.IsMainAdmin && !ctx.IsDeptAdmin)
        {
            return Forbid();
        }

        ViewData["Title"] = "Hierarchy Canvas";
        ViewData["ActiveNav"] = "Hierarchy";

        var currentUser = await _userManager.GetUserAsync(User);
        var currentUserId = currentUser?.Id;
        var pendingRequests = await _hierarchy.GetPendingRequestsAsync(currentUserId);
        ViewBag.PendingRequestsCount = pendingRequests.Count;

        // Scoping info for Canvas
        int? deptNodeId = null;
        if (ctx != null && ctx.IsDeptAdmin && currentUser != null)
        {
            deptNodeId = await GetUserDeptNodeIdAsync(currentUser);
        }

        ViewBag.IsMainAdmin = ctx?.IsMainAdmin ?? false;
        ViewBag.IsPlantHead = ctx?.IsPlantHead ?? false;
        ViewBag.IsDeptAdmin = ctx?.IsDeptAdmin ?? false;
        ViewBag.UserDeptNodeId = deptNodeId;
        ViewBag.UserDeptId = ctx?.DepartmentId;
        ViewBag.UserDeptName = ctx?.DepartmentName;

        return View("~/Views/Hierarchy/Canvas.cshtml");
    }

    // GET /api/v1/hierarchy/tree
    [HttpGet]
    [Route("api/v1/hierarchy/tree")]
    public async Task<IActionResult> GetTree(int? rootNodeId = null)
    {
        // Make sure graph is seeded if empty
        await _hierarchy.SeedHierarchyGraphFromLegacyAsync();

        var currentUser = await _userManager.GetUserAsync(User);
        var ctx = await _userContext.GetCurrentUserContextAsync(User);

        // If Department Admin, strictly scope graph to only their department subtree
        if (ctx != null && ctx.IsDeptAdmin && currentUser != null)
        {
            var deptNodeId = await GetUserDeptNodeIdAsync(currentUser);
            if (deptNodeId.HasValue)
            {
                rootNodeId = deptNodeId.Value;
            }
        }

        var graph = await _hierarchy.GetHierarchyGraphAsync(rootNodeId, null);
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

    private async Task<int?> GetUserDeptNodeIdAsync(ApplicationUser user)
    {
        if (user.HierarchyNodeId.HasValue)
        {
            var hn = await _db.HierarchyNodes.FindAsync(user.HierarchyNodeId.Value);
            if (hn != null && hn.NodeType == HierarchyNodeType.Department) return hn.NodeId;
            var curr = hn;
            while (curr != null && curr.NodeType != HierarchyNodeType.Department && curr.ParentNodeId.HasValue)
            {
                curr = await _db.HierarchyNodes.FindAsync(curr.ParentNodeId.Value);
                if (curr != null && curr.NodeType == HierarchyNodeType.Department) return curr.NodeId;
            }
        }
        if (user.DepartmentId.HasValue)
        {
            var dept = await _db.Departments.FindAsync(user.DepartmentId.Value);
            if (dept != null)
            {
                var dn = await _db.HierarchyNodes.FirstOrDefaultAsync(n => n.NodeType == HierarchyNodeType.Department && n.IsActive &&
                    (n.Name.ToLower() == dept.Name.ToLower() || n.Code.ToLower() == dept.Code.ToLower() || n.Name.Contains(dept.Name) || dept.Name.Contains(n.Name)));
                if (dn != null) return dn.NodeId;
            }
        }
        return null;
    }

    private async Task<bool> IsNodeUnderDepartmentAsync(int nodeId, int deptNodeId)
    {
        if (nodeId == deptNodeId) return true;
        var targetNode = await _db.HierarchyNodes.FindAsync(nodeId);
        if (targetNode == null) return false;
        var deptNode = await _db.HierarchyNodes.FindAsync(deptNodeId);
        if (deptNode == null) return false;
        if (!string.IsNullOrEmpty(targetNode.Path) && !string.IsNullOrEmpty(deptNode.Path) && targetNode.Path.StartsWith(deptNode.Path))
        {
            return true;
        }

        // Trace upwards via ParentNodeId in case Path is missing or not yet populated
        var curr = targetNode;
        while (curr != null && curr.ParentNodeId.HasValue)
        {
            if (curr.ParentNodeId.Value == deptNodeId) return true;
            curr = await _db.HierarchyNodes.FindAsync(curr.ParentNodeId.Value);
        }
        return false;
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

        var currentUser = await _userManager.GetUserAsync(User);
        var ctx = await _userContext.GetCurrentUserContextAsync(User);

        if (ctx != null && ctx.IsDeptAdmin && currentUser != null)
        {
            var deptNodeId = await GetUserDeptNodeIdAsync(currentUser);
            if (!deptNodeId.HasValue)
                return Forbid();

            // Department Admin can only create child nodes under his department or below
            if (!request.ParentNodeId.HasValue)
                return BadRequest(new { success = false, message = "Department Admins cannot create top-level nodes." });

            var isUnderDept = await IsNodeUnderDepartmentAsync(request.ParentNodeId.Value, deptNodeId.Value);
            if (!isUnderDept)
                return BadRequest(new { success = false, message = "Department Admins can only create nodes below their own department level." });

            // Department Admin can only create AssemblyLine, Cell, WorkStation, Subcell
            if (request.NodeType is not (HierarchyNodeType.AssemblyLine or HierarchyNodeType.Cell or HierarchyNodeType.WorkStation or HierarchyNodeType.Subcell))
                return BadRequest(new { success = false, message = "Department Admins can only create Assembly Lines, Cells, WorkStations, or Subcells." });
        }

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
        var ctx = await _userContext.GetCurrentUserContextAsync(User);

        if (ctx != null && ctx.IsDeptAdmin && currentUser != null)
        {
            var deptNodeId = await GetUserDeptNodeIdAsync(currentUser);
            if (!deptNodeId.HasValue) return Forbid();

            if (request.NodeId == deptNodeId.Value)
                return BadRequest(new { success = false, message = "Department Admins cannot reparent their department node." });

            var isChildUnderDept = await IsNodeUnderDepartmentAsync(request.NodeId, deptNodeId.Value);
            if (!isChildUnderDept)
                return BadRequest(new { success = false, message = "Department Admins can only move nodes within their own department branch." });

            if (!request.NewParentNodeId.HasValue)
                return BadRequest(new { success = false, message = "Department Admins cannot detach nodes to top-level." });

            var isParentUnderDept = await IsNodeUnderDepartmentAsync(request.NewParentNodeId.Value, deptNodeId.Value);
            if (!isParentUnderDept)
                return BadRequest(new { success = false, message = "Target parent must be within your department branch." });
        }

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
        var ctx = await _userContext.GetCurrentUserContextAsync(User);

        if (ctx != null && ctx.IsDeptAdmin && currentUser != null)
        {
            var deptNodeId = await GetUserDeptNodeIdAsync(currentUser);
            if (!deptNodeId.HasValue) return Forbid();

            if (id == deptNodeId.Value)
                return BadRequest(new { success = false, message = "Department Admins cannot delete their own department node." });

            var isBelowDept = await IsNodeUnderDepartmentAsync(id, deptNodeId.Value);
            if (!isBelowDept)
                return BadRequest(new { success = false, message = "Department Admins can only delete nodes below their own department level." });
        }

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
