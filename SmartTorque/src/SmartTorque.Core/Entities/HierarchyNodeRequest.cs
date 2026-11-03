using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// A 1-click request submitted by an onboarding user or admin to add an immediate child node under a parent node.
/// Routed strictly to the immediate parent node's administrator.
/// </summary>
public class HierarchyNodeRequest
{
    public int RequestId { get; set; }

    public int ParentNodeId { get; set; }

    public HierarchyNode? ParentNode { get; set; }

    public string RequestedNodeName { get; set; } = string.Empty;

    public HierarchyNodeType RequestedNodeType { get; set; } = HierarchyNodeType.Department;

    public string? RequestedByUserId { get; set; }

    public ApplicationUser? RequestedByUser { get; set; }

    public NodeRequestStatus RequestStatus { get; set; } = NodeRequestStatus.Pending;

    public string? ApproverUserId { get; set; }

    public ApplicationUser? ApproverUser { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }
}