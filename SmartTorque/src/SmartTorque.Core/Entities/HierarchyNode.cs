using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// A flexible node in the organizational parent-child hierarchy graph.
/// Supports arbitrary plant layouts (Enterprise -> Plant -> Department -> Line/Cell -> Station/Subcell).
/// </summary>
public class HierarchyNode
{
    public int NodeId { get; set; }

    public int? ParentNodeId { get; set; }

    public HierarchyNode? ParentNode { get; set; }

    public ICollection<HierarchyNode> Children { get; set; } = new List<HierarchyNode>();

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public HierarchyNodeType NodeType { get; set; } = HierarchyNodeType.Department;

    /// <summary>
    /// Materialized path representing tree hierarchy for O(1) subtree scoping (e.g. "/1/4/12/").
    /// </summary>
    public string Path { get; set; } = "/";

    public int Depth { get; set; } = 0;

    /// <summary>
    /// Immediate designated administrator or supervisor responsible for this node and approving immediate child requests.
    /// </summary>
    public string? AdminUserId { get; set; }

    public ApplicationUser? AdminUser { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();

    public ICollection<HierarchyNodeRequest> Requests { get; set; } = new List<HierarchyNodeRequest>();
}
