using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;

namespace SmartTorque.Services;

public record HierarchyItemDto(
    int Id,
    string Code,
    string Name,
    int? ParentId = null,
    string? Type = null,
    int? EnterpriseId = null,
    IReadOnlyList<int>? AncestorIds = null)
{
    public HierarchyItemDto(int id, string code, string name, int? parentId, string? type)
        : this(id, code, name, parentId, type, null, null)
    {
    }
}

public record CascadeOptionsDto(
    IReadOnlyList<HierarchyItemDto> Companies,
    IReadOnlyList<HierarchyItemDto> Plants,
    IReadOnlyList<HierarchyItemDto> Departments,
    IReadOnlyList<HierarchyItemDto> Units,
    IReadOnlyList<HierarchyItemDto> Stations);

public class HierarchyNodeDto
{
    public int NodeId { get; set; }
    public int? ParentNodeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public HierarchyNodeType NodeType { get; set; }
    public string Path { get; set; } = string.Empty;
    public int Depth { get; set; }
    public string? AdminUserId { get; set; }
    public string? AdminUserName { get; set; }
    public string? AdminEmail { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public int PendingRequestsCount { get; set; }
    public int WorkerCount { get; set; }
    public List<HierarchyNodeDto> Children { get; set; } = new();
}

public class HierarchyNodeRequestDto
{
    public int RequestId { get; set; }
    public int ParentNodeId { get; set; }
    public string ParentNodeName { get; set; } = string.Empty;
    public string RequestedNodeName { get; set; } = string.Empty;
    public HierarchyNodeType RequestedNodeType { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByUserName { get; set; } = string.Empty;
    public string RequestedByUserEmail { get; set; } = string.Empty;
    public NodeRequestStatus RequestStatus { get; set; }
    public string? ApproverUserId { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public interface IHierarchyService
{
    // Legacy support
    Task<IReadOnlyList<Company>> GetCompaniesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Plant>> GetPlantsAsync(int? companyId = null, CancellationToken ct = default);
    Task<IReadOnlyList<Department>> GetDepartmentsAsync(int? plantId = null, CancellationToken ct = default);
    Task<IReadOnlyList<AssemblyLine>> GetProductionUnitsAsync(int? departmentId = null, ProductionUnitType? unitType = null, CancellationToken ct = default);
    Task<IReadOnlyList<WorkStation>> GetWorkStationsAsync(int? assemblyLineId = null, StationType? stationType = null, CancellationToken ct = default);
    Task<CascadeOptionsDto> GetCascadeOptionsAsync(CancellationToken ct = default);
    Task<(int? CompanyId, int? PlantId, int? DepartmentId)> ResolveOrCreateHierarchyAsync(
        string? companyName,
        string? plantName,
        string? departmentName,
        int? companyId = null,
        int? plantId = null,
        int? departmentId = null,
        CancellationToken ct = default);

    // Flexible Dynamic Hierarchy Graph
    Task<List<HierarchyNodeDto>> GetHierarchyGraphAsync(int? rootNodeId = null, string? adminUserId = null, CancellationToken ct = default);
    Task<List<HierarchyNode>> GetImmediateChildrenAsync(int? parentNodeId, CancellationToken ct = default);
    Task<List<int>> GetScopedNodeIdsAsync(int rootNodeId, CancellationToken ct = default);
    Task<List<HierarchyNode>> SearchNodesAsync(string term, HierarchyNodeType? type = null, int? parentNodeId = null, CancellationToken ct = default);
    Task<HierarchyNode> CreateNodeAsync(string name, HierarchyNodeType type, int? parentNodeId, string? code = null, string? adminUserId = null, CancellationToken ct = default);
    Task<bool> AttachOrLinkNodeAsync(int nodeId, int? newParentNodeId, string modifiedByUserId, CancellationToken ct = default);
    Task<bool> DeleteNodeAsync(int nodeId, string deletedByUserId, CancellationToken ct = default);
    Task<HierarchyNodeRequest> RequestChildNodeAsync(int parentNodeId, string name, HierarchyNodeType type, string requestedByUserId, CancellationToken ct = default);
    Task<HierarchyNode> ApproveChildNodeRequestAsync(int requestId, string approverUserId, CancellationToken ct = default);
    Task<bool> RejectChildNodeRequestAsync(int requestId, string approverUserId, string reason, CancellationToken ct = default);
    Task<List<HierarchyNodeRequestDto>> GetPendingRequestsAsync(string? adminUserId = null, CancellationToken ct = default);
    Task SeedHierarchyGraphFromLegacyAsync(CancellationToken ct = default);
    Task SyncHierarchyWithRelationalEntitiesAsync(CancellationToken ct = default);
}

public class HierarchyService : IHierarchyService
{
    private readonly AppDbContext _db;

    public HierarchyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Company>> GetCompaniesAsync(CancellationToken ct = default)
    {
        return await _db.Companies
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Plant>> GetPlantsAsync(int? companyId = null, CancellationToken ct = default)
    {
        var q = _db.Plants.Where(p => p.IsActive);
        if (companyId.HasValue)
            q = q.Where(p => p.CompanyId == companyId.Value);

        return await q.OrderBy(p => p.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Department>> GetDepartmentsAsync(int? plantId = null, CancellationToken ct = default)
    {
        var q = _db.Departments.Where(d => d.IsActive);
        if (plantId.HasValue)
            q = q.Where(d => d.PlantId == plantId.Value);

        return await q.OrderBy(d => d.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AssemblyLine>> GetProductionUnitsAsync(int? departmentId = null, ProductionUnitType? unitType = null, CancellationToken ct = default)
    {
        var q = _db.AssemblyLines.Where(a => a.IsActive);
        if (departmentId.HasValue)
            q = q.Where(a => a.DepartmentId == departmentId.Value);
        if (unitType.HasValue)
            q = q.Where(a => a.UnitType == unitType.Value);

        return await q.OrderBy(a => a.AssemblyName).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkStation>> GetWorkStationsAsync(int? assemblyLineId = null, StationType? stationType = null, CancellationToken ct = default)
    {
        var q = _db.WorkStations.Where(w => w.IsActive);
        if (assemblyLineId.HasValue)
            q = q.Where(w => w.AssemblyLineId == assemblyLineId.Value);
        if (stationType.HasValue)
            q = q.Where(w => w.StationType == stationType.Value);

        return await q.OrderBy(w => w.StationName).ToListAsync(ct);
    }

    public async Task<CascadeOptionsDto> GetCascadeOptionsAsync(CancellationToken ct = default)
    {
        // 1. Ensure relational tables are synchronized with active HierarchyNodes
        await SyncHierarchyWithRelationalEntitiesAsync(ct);

        var companies = await _db.Companies
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        var plants = await _db.Plants
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var departments = await _db.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

        var graphNodes = await _db.HierarchyNodes
            .AsNoTracking()
            .Where(n => n.IsActive)
            .ToListAsync(ct);

        var nodeLookup = graphNodes.ToDictionary(n => n.NodeId);

        // Map graph nodes to relational entities
        var nodeToCompanyId = new Dictionary<int, int>();
        var nodeToPlantId = new Dictionary<int, int>();
        var nodeToDeptId = new Dictionary<int, int>();

        foreach (var gn in graphNodes)
        {
            if (gn.NodeType is HierarchyNodeType.Enterprise or HierarchyNodeType.Business)
            {
                var comp = companies.FirstOrDefault(c => c.Code.Equals(gn.Code, StringComparison.OrdinalIgnoreCase) || c.Name.Equals(gn.Name, StringComparison.OrdinalIgnoreCase));
                if (comp != null) nodeToCompanyId[gn.NodeId] = comp.CompanyId;
            }
            else if (gn.NodeType == HierarchyNodeType.Plant)
            {
                var p = plants.FirstOrDefault(pl => pl.Code.Equals(gn.Code, StringComparison.OrdinalIgnoreCase) || pl.Name.Equals(gn.Name, StringComparison.OrdinalIgnoreCase));
                if (p != null) nodeToPlantId[gn.NodeId] = p.PlantId;
            }
            else if (gn.NodeType == HierarchyNodeType.Department)
            {
                var d = departments.FirstOrDefault(dp => dp.Code.Equals(gn.Code, StringComparison.OrdinalIgnoreCase) || dp.Name.Equals(gn.Name, StringComparison.OrdinalIgnoreCase));
                if (d != null) nodeToDeptId[gn.NodeId] = d.DepartmentId;
            }
        }

        var compList = new List<HierarchyItemDto>();
        // Only active Enterprise and Business nodes from canvas
        foreach (var gn in graphNodes.Where(n => n.NodeType is HierarchyNodeType.Enterprise or HierarchyNodeType.Business).OrderBy(n => n.Depth).ThenBy(n => n.Name))
        {
            int id = nodeToCompanyId.TryGetValue(gn.NodeId, out var cid) ? cid : gn.NodeId;
            int? parentId = gn.ParentNodeId.HasValue && nodeToCompanyId.TryGetValue(gn.ParentNodeId.Value, out var pid) ? pid : gn.ParentNodeId;
            var ancestorIds = GetAllAncestorIds(gn, nodeLookup, nodeToCompanyId);
            compList.Add(new HierarchyItemDto(id, gn.Code, gn.Name, parentId, gn.NodeType.ToString(), id, ancestorIds));
        }

        // Fallback to relational companies if canvas has none
        if (compList.Count == 0)
        {
            compList.AddRange(companies.Select(c => new HierarchyItemDto(c.CompanyId, c.Code, c.Name, null, "Enterprise", c.CompanyId, new List<int> { c.CompanyId })));
        }

        var plantList = new List<HierarchyItemDto>();
        foreach (var gn in graphNodes.Where(n => n.NodeType == HierarchyNodeType.Plant).OrderBy(n => n.Name))
        {
            int id = nodeToPlantId.TryGetValue(gn.NodeId, out var pid) ? pid : gn.NodeId;
            var enterpriseNode = FindAncestor(gn, n => n.NodeType == HierarchyNodeType.Enterprise, nodeLookup);
            int? enterpriseCompId = enterpriseNode != null && nodeToCompanyId.TryGetValue(enterpriseNode.NodeId, out var ecid) ? ecid : enterpriseNode?.NodeId;

            // Primary parent: prefer root Enterprise so it matches the Company dropdown directly
            var parentCompNode = enterpriseNode
                ?? FindAncestor(gn, n => n.NodeType is HierarchyNodeType.Enterprise or HierarchyNodeType.Business, nodeLookup);
            int? parentCompId = parentCompNode != null && nodeToCompanyId.TryGetValue(parentCompNode.NodeId, out var pcid) ? pcid : (enterpriseCompId ?? gn.ParentNodeId);

            var ancestorIds = GetAllAncestorIds(gn, nodeLookup, nodeToCompanyId);
            if (parentCompId.HasValue && !ancestorIds.Contains(parentCompId.Value)) ancestorIds.Add(parentCompId.Value);
            if (enterpriseCompId.HasValue && !ancestorIds.Contains(enterpriseCompId.Value)) ancestorIds.Add(enterpriseCompId.Value);

            plantList.Add(new HierarchyItemDto(
                id,
                gn.Code,
                gn.Name,
                parentCompId,
                "Plant",
                enterpriseCompId,
                ancestorIds
            ));
        }

        if (plantList.Count == 0)
        {
            plantList.AddRange(plants.Select(p => new HierarchyItemDto(p.PlantId, p.Code, p.Name, p.CompanyId, "Plant", p.CompanyId, new List<int> { p.CompanyId })));
        }

        var deptList = new List<HierarchyItemDto>();
        foreach (var gn in graphNodes.Where(n => n.NodeType == HierarchyNodeType.Department).OrderBy(n => n.Name))
        {
            int id = nodeToDeptId.TryGetValue(gn.NodeId, out var did) ? did : gn.NodeId;
            var plantNode = FindAncestor(gn, n => n.NodeType == HierarchyNodeType.Plant, nodeLookup);
            int? parentPlantId = plantNode != null && nodeToPlantId.TryGetValue(plantNode.NodeId, out var ppid) ? ppid : (plantNode?.NodeId ?? gn.ParentNodeId);

            var ancestorIds = GetAllAncestorIds(gn, nodeLookup, nodeToPlantId);
            if (parentPlantId.HasValue && !ancestorIds.Contains(parentPlantId.Value)) ancestorIds.Add(parentPlantId.Value);

            deptList.Add(new HierarchyItemDto(
                id,
                gn.Code,
                gn.Name,
                parentPlantId,
                "Department",
                null,
                ancestorIds
            ));
        }

        if (deptList.Count == 0)
        {
            deptList.AddRange(departments.Select(d => new HierarchyItemDto(d.DepartmentId, d.Code, d.Name, d.PlantId, "Department", null, new List<int> { d.PlantId })));
        }

        var units = await _db.AssemblyLines
            .Where(u => u.IsActive)
            .OrderBy(u => u.AssemblyName)
            .Select(u => new HierarchyItemDto(u.AssemblyLineId, u.AssemblyCode, $"{u.AssemblyName} ({(u.UnitType == ProductionUnitType.Cell ? "Cell" : "Line")})", u.DepartmentId, u.UnitType.ToString(), null, null))
            .ToListAsync(ct);

        var stations = await _db.WorkStations
            .Where(s => s.IsActive)
            .OrderBy(s => s.StationName)
            .Select(s => new HierarchyItemDto(s.WorkStationId, s.StationCode, $"{s.StationName} ({(s.StationType == StationType.Subcell ? "Subcell" : "Station")})", s.AssemblyLineId, s.StationType.ToString(), null, null))
            .ToListAsync(ct);

        return new CascadeOptionsDto(compList, plantList, deptList, units, stations);
    }

    public async Task<(int? CompanyId, int? PlantId, int? DepartmentId)> ResolveOrCreateHierarchyAsync(
        string? companyName,
        string? plantName,
        string? departmentName,
        int? companyId = null,
        int? plantId = null,
        int? departmentId = null,
        CancellationToken ct = default)
    {
        var trimmedCompany = companyName?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedCompany))
        {
            var existingComp = await _db.Companies
                .FirstOrDefaultAsync(co => co.Name.ToLower() == trimmedCompany.ToLower() || co.Code.ToLower() == trimmedCompany.ToLower(), ct);

            if (existingComp != null)
            {
                companyId = existingComp.CompanyId;
            }
            else
            {
                var code = GenerateCode(trimmedCompany);
                var newComp = new Company
                {
                    Name = trimmedCompany,
                    Code = code,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Companies.Add(newComp);
                await _db.SaveChangesAsync(ct);
                companyId = newComp.CompanyId;
            }
        }
        else if (companyId.HasValue)
        {
            var existing = await _db.Companies.FindAsync(new object[] { companyId.Value }, ct);
            if (existing == null) companyId = null;
        }

        var trimmedPlant = plantName?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedPlant))
        {
            var q = _db.Plants.AsQueryable();
            if (companyId.HasValue)
                q = q.Where(p => p.CompanyId == companyId.Value);

            var existingPlant = await q.FirstOrDefaultAsync(p => p.Name.ToLower() == trimmedPlant.ToLower() || p.Code.ToLower() == trimmedPlant.ToLower(), ct);
            if (existingPlant != null)
            {
                plantId = existingPlant.PlantId;
                if (!companyId.HasValue) companyId = existingPlant.CompanyId;
            }
            else if (companyId.HasValue)
            {
                var code = GenerateCode(trimmedPlant);
                var newPlant = new Plant
                {
                    CompanyId = companyId.Value,
                    Name = trimmedPlant,
                    Code = code,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Plants.Add(newPlant);
                await _db.SaveChangesAsync(ct);
                plantId = newPlant.PlantId;
            }
        }
        else if (plantId.HasValue)
        {
            var existing = await _db.Plants.FindAsync(new object[] { plantId.Value }, ct);
            if (existing == null) plantId = null;
            else if (!companyId.HasValue) companyId = existing.CompanyId;
        }

        var trimmedDept = departmentName?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedDept))
        {
            var q = _db.Departments.AsQueryable();
            if (plantId.HasValue)
                q = q.Where(d => d.PlantId == plantId.Value);

            var existingDept = await q.FirstOrDefaultAsync(d => d.Name.ToLower() == trimmedDept.ToLower() || d.Code.ToLower() == trimmedDept.ToLower(), ct);
            if (existingDept != null)
            {
                departmentId = existingDept.DepartmentId;
                if (!plantId.HasValue) plantId = existingDept.PlantId;
            }
            else if (plantId.HasValue)
            {
                var code = GenerateCode(trimmedDept);
                var newDept = new Department
                {
                    PlantId = plantId.Value,
                    Name = trimmedDept,
                    Code = code,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Departments.Add(newDept);
                await _db.SaveChangesAsync(ct);
                departmentId = newDept.DepartmentId;
            }
        }
        else if (departmentId.HasValue)
        {
            var existing = await _db.Departments.FindAsync(new object[] { departmentId.Value }, ct);
            if (existing == null) departmentId = null;
            else if (!plantId.HasValue) plantId = existing.PlantId;
        }

        return (companyId, plantId, departmentId);
    }

    // =========================================================================
    // Dynamic Parent-Child Graph Implementation
    // =========================================================================

    public async Task<List<HierarchyNodeDto>> GetHierarchyGraphAsync(int? rootNodeId = null, string? adminUserId = null, CancellationToken ct = default)
    {
        // Check if admin is scoped to a specific node
        if (!rootNodeId.HasValue && !string.IsNullOrEmpty(adminUserId))
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == adminUserId, ct);
            if (user != null && !user.IsMainAdmin)
            {
                // Scoped by HierarchyNodeId or administered node
                var managedNode = await _db.HierarchyNodes
                    .FirstOrDefaultAsync(n => n.AdminUserId == adminUserId && n.IsActive, ct);

                if (managedNode != null)
                {
                    rootNodeId = managedNode.NodeId;
                }
                else if (user.HierarchyNodeId.HasValue)
                {
                    rootNodeId = user.HierarchyNodeId.Value;
                }
            }
        }

        IQueryable<HierarchyNode> query = _db.HierarchyNodes.Where(n => n.IsActive);

        List<HierarchyNode> allNodes;
        if (rootNodeId.HasValue)
        {
            var root = await _db.HierarchyNodes.FindAsync(new object[] { rootNodeId.Value }, ct);
            if (root != null)
            {
                var allActive = await _db.HierarchyNodes.Where(n => n.IsActive).Include(n => n.AdminUser).ToListAsync(ct);
                var validNodeIds = new HashSet<int> { root.NodeId };
                bool addedNew = true;
                while (addedNew)
                {
                    addedNew = false;
                    foreach (var n in allActive)
                    {
                        if (!validNodeIds.Contains(n.NodeId) && n.ParentNodeId.HasValue && validNodeIds.Contains(n.ParentNodeId.Value))
                        {
                            validNodeIds.Add(n.NodeId);
                            addedNew = true;
                        }
                    }
                }
                allNodes = allActive.Where(n => validNodeIds.Contains(n.NodeId))
                    .OrderBy(n => n.Depth)
                    .ThenBy(n => n.SortOrder)
                    .ThenBy(n => n.Name)
                    .ToList();
            }
            else
            {
                allNodes = new List<HierarchyNode>();
            }
        }
        else
        {
            allNodes = await query
                .Include(n => n.AdminUser)
                .OrderBy(n => n.Depth)
                .ThenBy(n => n.SortOrder)
                .ThenBy(n => n.Name)
                .ToListAsync(ct);
        }

        // Fetch pending requests counts per parent node
        var pendingReqCounts = await _db.HierarchyNodeRequests
            .Where(r => r.RequestStatus == NodeRequestStatus.Pending)
            .GroupBy(r => r.ParentNodeId)
            .Select(g => new { ParentNodeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ParentNodeId, g => g.Count, ct);

        // Fetch worker counts per node
        var workerCounts = await _db.Users
            .Where(u => u.HierarchyNodeId != null && u.IsActive)
            .GroupBy(u => u.HierarchyNodeId!.Value)
            .Select(g => new { NodeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.NodeId, g => g.Count, ct);

        var dtoList = allNodes.Select(n => new HierarchyNodeDto
        {
            NodeId = n.NodeId,
            ParentNodeId = n.ParentNodeId,
            Code = n.Code,
            Name = n.Name,
            NodeType = n.NodeType,
            Path = n.Path,
            Depth = n.Depth,
            AdminUserId = n.AdminUserId,
            AdminUserName = n.AdminUser?.FullName,
            AdminEmail = n.AdminUser?.Email,
            IsActive = n.IsActive,
            SortOrder = n.SortOrder,
            PendingRequestsCount = pendingReqCounts.GetValueOrDefault(n.NodeId, 0),
            WorkerCount = workerCounts.GetValueOrDefault(n.NodeId, 0)
        }).ToList();

        // Build tree structure
        var lookup = dtoList.ToDictionary(d => d.NodeId);
        var roots = new List<HierarchyNodeDto>();

        foreach (var item in dtoList)
        {
            if (item.ParentNodeId.HasValue && lookup.TryGetValue(item.ParentNodeId.Value, out var parent))
            {
                parent.Children.Add(item);
            }
            else
            {
                roots.Add(item);
            }
        }

        return roots;
    }

    public async Task<List<HierarchyNode>> GetImmediateChildrenAsync(int? parentNodeId, CancellationToken ct = default)
    {
        return await _db.HierarchyNodes
            .Where(n => n.IsActive && n.ParentNodeId == parentNodeId)
            .OrderBy(n => n.SortOrder)
            .ThenBy(n => n.Name)
            .ToListAsync(ct);
    }

    public async Task<List<int>> GetScopedNodeIdsAsync(int rootNodeId, CancellationToken ct = default)
    {
        var root = await _db.HierarchyNodes.FindAsync(new object[] { rootNodeId }, ct);
        if (root == null) return new List<int> { rootNodeId };

        return await _db.HierarchyNodes
            .Where(n => n.IsActive && (n.NodeId == root.NodeId || n.Path.StartsWith(root.Path)))
            .Select(n => n.NodeId)
            .ToListAsync(ct);
    }

    public async Task<List<HierarchyNode>> SearchNodesAsync(string term, HierarchyNodeType? type = null, int? parentNodeId = null, CancellationToken ct = default)
    {
        var cleanTerm = (term ?? string.Empty).Trim().ToLowerInvariant();
        var q = _db.HierarchyNodes.Where(n => n.IsActive);

        if (type.HasValue)
            q = q.Where(n => n.NodeType == type.Value);

        if (parentNodeId.HasValue)
            q = q.Where(n => n.ParentNodeId == parentNodeId.Value);

        if (!string.IsNullOrWhiteSpace(cleanTerm))
        {
            q = q.Where(n => n.Name.ToLower().Contains(cleanTerm) || n.Code.ToLower().Contains(cleanTerm));
        }

        return await q.OrderBy(n => n.Name).Take(20).ToListAsync(ct);
    }

    public async Task<HierarchyNode> CreateNodeAsync(string name, HierarchyNodeType type, int? parentNodeId, string? code = null, string? adminUserId = null, CancellationToken ct = default)
    {
        HierarchyNode? parent = null;
        if (parentNodeId.HasValue)
        {
            parent = await _db.HierarchyNodes.FindAsync(new object[] { parentNodeId.Value }, ct)
                ?? throw new InvalidOperationException($"Parent node {parentNodeId.Value} not found.");
        }

        var generatedCode = string.IsNullOrWhiteSpace(code) ? GenerateCode(name) : code.Trim().ToUpperInvariant();

        var node = new HierarchyNode
        {
            Name = name.Trim(),
            Code = generatedCode,
            NodeType = type,
            ParentNodeId = parentNodeId,
            AdminUserId = adminUserId,
            IsActive = true,
            Depth = parent != null ? parent.Depth + 1 : 0,
            Path = string.Empty, // will set right after acquiring NodeId
            CreatedAt = DateTime.UtcNow
        };

        _db.HierarchyNodes.Add(node);
        await _db.SaveChangesAsync(ct);

        // Materialize path: e.g. /1/2/3/
        node.Path = parent != null ? $"{parent.Path}{node.NodeId}/" : $"/{node.NodeId}/";
        await _db.SaveChangesAsync(ct);

        await SyncHierarchyWithRelationalEntitiesAsync(ct);

        return node;
    }

    public async Task<bool> AttachOrLinkNodeAsync(int nodeId, int? newParentNodeId, string modifiedByUserId, CancellationToken ct = default)
    {
        var node = await _db.HierarchyNodes.FindAsync(new object[] { nodeId }, ct);
        if (node == null) return false;

        if (node.ParentNodeId == newParentNodeId) return true; // unchanged

        HierarchyNode? newParent = null;
        if (newParentNodeId.HasValue)
        {
            newParent = await _db.HierarchyNodes.FindAsync(new object[] { newParentNodeId.Value }, ct);
            if (newParent == null) return false;

            // Cycle detection: newParent cannot be a descendant of node
            if (newParent.Path.StartsWith(node.Path))
            {
                throw new InvalidOperationException("Cannot attach a node to one of its own descendants (cyclic relationship).");
            }
        }

        var oldPathPrefix = node.Path;
        var oldDepth = node.Depth;
        var newDepth = newParent != null ? newParent.Depth + 1 : 0;
        var newPathPrefix = newParent != null ? $"{newParent.Path}{node.NodeId}/" : $"/{node.NodeId}/";
        var depthDelta = newDepth - oldDepth;

        node.ParentNodeId = newParentNodeId;
        node.Depth = newDepth;
        node.Path = newPathPrefix;
        node.UpdatedAt = DateTime.UtcNow;

        // Reparent all descendants
        var descendants = await _db.HierarchyNodes
            .Where(n => n.NodeId != nodeId && n.Path.StartsWith(oldPathPrefix))
            .ToListAsync(ct);

        foreach (var desc in descendants)
        {
            desc.Path = newPathPrefix + desc.Path[oldPathPrefix.Length..];
            desc.Depth += depthDelta;
            desc.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await SyncHierarchyWithRelationalEntitiesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteNodeAsync(int nodeId, string deletedByUserId, CancellationToken ct = default)
    {
        var node = await _db.HierarchyNodes.FindAsync(new object[] { nodeId }, ct);
        if (node == null || !node.IsActive) return false;

        int? newParentId = node.ParentNodeId;
        HierarchyNode? newParent = null;
        if (newParentId.HasValue)
        {
            newParent = await _db.HierarchyNodes.FindAsync(new object[] { newParentId.Value }, ct);
        }

        string parentPathPrefix = newParent != null ? newParent.Path : "/";
        int parentDepth = newParent != null ? newParent.Depth : -1;

        // Fetch direct children of deleted node
        var directChildren = await _db.HierarchyNodes
            .Where(n => n.ParentNodeId == nodeId && n.IsActive)
            .ToListAsync(ct);

        foreach (var child in directChildren)
        {
            var oldChildPathPrefix = child.Path;
            var oldChildDepth = child.Depth;
            var newChildDepth = parentDepth + 1;
            var newChildPathPrefix = newParent != null ? $"{parentPathPrefix}{child.NodeId}/" : $"/{child.NodeId}/";
            var depthDelta = newChildDepth - oldChildDepth;

            child.ParentNodeId = newParentId;
            child.Depth = newChildDepth;
            child.Path = newChildPathPrefix;
            child.UpdatedAt = DateTime.UtcNow;

            // Update all descendants of this child
            var descendants = await _db.HierarchyNodes
                .Where(n => n.NodeId != child.NodeId && n.Path.StartsWith(oldChildPathPrefix) && n.IsActive)
                .ToListAsync(ct);

            foreach (var desc in descendants)
            {
                desc.Path = newChildPathPrefix + desc.Path[oldChildPathPrefix.Length..];
                desc.Depth += depthDelta;
                desc.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Re-associate users who were directly assigned to deleted node
        var assignedUsers = await _db.Users
            .Where(u => u.HierarchyNodeId == nodeId)
            .ToListAsync(ct);

        foreach (var u in assignedUsers)
        {
            u.HierarchyNodeId = newParentId;
        }

        // Reparent pending requests pointing to deleted node
        var pendingReqs = await _db.HierarchyNodeRequests
            .Where(r => r.ParentNodeId == nodeId && r.RequestStatus == NodeRequestStatus.Pending)
            .ToListAsync(ct);

        foreach (var req in pendingReqs)
        {
            if (newParentId.HasValue)
            {
                req.ParentNodeId = newParentId.Value;
                req.ApproverUserId = newParent?.AdminUserId;
            }
            else
            {
                req.RequestStatus = NodeRequestStatus.Rejected;
                req.RejectionReason = "Target parent node was deleted.";
                req.ResolvedAt = DateTime.UtcNow;
            }
        }

        // Mark node as inactive
        node.IsActive = false;
        node.ParentNodeId = null;
        node.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await SyncHierarchyWithRelationalEntitiesAsync(ct);
        return true;
    }

    public async Task<HierarchyNodeRequest> RequestChildNodeAsync(int parentNodeId, string name, HierarchyNodeType type, string requestedByUserId, CancellationToken ct = default)
    {
        var parent = await _db.HierarchyNodes.FindAsync(new object[] { parentNodeId }, ct)
            ?? throw new InvalidOperationException($"Parent node {parentNodeId} not found.");

        var req = new HierarchyNodeRequest
        {
            ParentNodeId = parentNodeId,
            RequestedNodeName = name.Trim(),
            RequestedNodeType = type,
            RequestedByUserId = requestedByUserId,
            RequestStatus = NodeRequestStatus.Pending,
            ApproverUserId = parent.AdminUserId, // Immediate parent admin
            CreatedAt = DateTime.UtcNow
        };

        _db.HierarchyNodeRequests.Add(req);
        await _db.SaveChangesAsync(ct);
        return req;
    }

    public async Task<HierarchyNode> ApproveChildNodeRequestAsync(int requestId, string approverUserId, CancellationToken ct = default)
    {
        var req = await _db.HierarchyNodeRequests
            .Include(r => r.ParentNode)
            .FirstOrDefaultAsync(r => r.RequestId == requestId, ct)
            ?? throw new InvalidOperationException($"Request {requestId} not found.");

        if (req.RequestStatus != NodeRequestStatus.Pending)
        {
            throw new InvalidOperationException($"Request {requestId} is already {req.RequestStatus}.");
        }

        // Create the requested child node under ParentNodeId
        var newNode = await CreateNodeAsync(req.RequestedNodeName, req.RequestedNodeType, req.ParentNodeId, null, null, ct);

        req.RequestStatus = NodeRequestStatus.Approved;
        req.ApproverUserId = approverUserId;
        req.ResolvedAt = DateTime.UtcNow;

        // If the requesting user was awaiting this department, associate them directly
        var requestingUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.RequestedByUserId, ct);
        if (requestingUser != null)
        {
            requestingUser.HierarchyNodeId = newNode.NodeId;
            if (requestingUser.RegistrationStatus == RegistrationStatus.Pending)
            {
                requestingUser.RegistrationStatus = RegistrationStatus.Approved;
                requestingUser.IsActive = true;
            }
        }

        await _db.SaveChangesAsync(ct);
        return newNode;
    }

    public async Task<bool> RejectChildNodeRequestAsync(int requestId, string approverUserId, string reason, CancellationToken ct = default)
    {
        var req = await _db.HierarchyNodeRequests.FindAsync(new object[] { requestId }, ct);
        if (req == null || req.RequestStatus != NodeRequestStatus.Pending) return false;

        req.RequestStatus = NodeRequestStatus.Rejected;
        req.ApproverUserId = approverUserId;
        req.RejectionReason = reason;
        req.ResolvedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<HierarchyNodeRequestDto>> GetPendingRequestsAsync(string? adminUserId = null, CancellationToken ct = default)
    {
        IQueryable<HierarchyNodeRequest> query = _db.HierarchyNodeRequests
            .Include(r => r.ParentNode)
            .Include(r => r.RequestedByUser)
            .Where(r => r.RequestStatus == NodeRequestStatus.Pending);

        if (!string.IsNullOrEmpty(adminUserId))
        {
            var user = await _db.Users.FindAsync(new object[] { adminUserId }, ct);
            if (user != null && !user.IsMainAdmin)
            {
                // Scoped strictly to requests where this user is the immediate parent admin
                query = query.Where(r => (r.ParentNode != null && r.ParentNode.AdminUserId == adminUserId) || r.ApproverUserId == adminUserId);
            }
        }

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new HierarchyNodeRequestDto
            {
                RequestId = r.RequestId,
                ParentNodeId = r.ParentNodeId,
                ParentNodeName = r.ParentNode != null ? r.ParentNode.Name : string.Empty,
                RequestedNodeName = r.RequestedNodeName,
                RequestedNodeType = r.RequestedNodeType,
                RequestedByUserId = r.RequestedByUserId ?? string.Empty,
                RequestedByUserName = r.RequestedByUser != null ? r.RequestedByUser.FullName : "System",
                RequestedByUserEmail = r.RequestedByUser != null ? (r.RequestedByUser.Email ?? string.Empty) : string.Empty,
                RequestStatus = r.RequestStatus,
                ApproverUserId = r.ApproverUserId,
                RejectionReason = r.RejectionReason,
                CreatedAt = r.CreatedAt,
                ResolvedAt = r.ResolvedAt
            })
            .ToListAsync(ct);
    }

    public async Task SeedHierarchyGraphFromLegacyAsync(CancellationToken ct = default)
    {
        if (await _db.HierarchyNodes.AnyAsync(ct)) return;

        // 1. Companies -> Enterprise Nodes
        var companies = await _db.Companies.ToListAsync(ct);
        var companyNodeMap = new Dictionary<int, HierarchyNode>();

        foreach (var comp in companies)
        {
            var node = await CreateNodeAsync(comp.Name, HierarchyNodeType.Enterprise, null, comp.Code, null, ct);
            companyNodeMap[comp.CompanyId] = node;
        }

        // 2. Plants -> Plant Nodes
        var plants = await _db.Plants.ToListAsync(ct);
        var plantNodeMap = new Dictionary<int, HierarchyNode>();

        foreach (var plant in plants)
        {
            int? parentNodeId = companyNodeMap.TryGetValue(plant.CompanyId, out var compNode) ? compNode.NodeId : null;
            var node = await CreateNodeAsync(plant.Name, HierarchyNodeType.Plant, parentNodeId, plant.Code, null, ct);
            plantNodeMap[plant.PlantId] = node;
        }

        // 3. Departments -> Department Nodes
        var depts = await _db.Departments.ToListAsync(ct);
        var deptNodeMap = new Dictionary<int, HierarchyNode>();

        foreach (var dept in depts)
        {
            int? parentNodeId = plantNodeMap.TryGetValue(dept.PlantId, out var plantNode) ? plantNode.NodeId : null;
            var node = await CreateNodeAsync(dept.Name, HierarchyNodeType.Department, parentNodeId, dept.Code, null, ct);
            deptNodeMap[dept.DepartmentId] = node;
        }

        // 4. Assembly Lines -> AssemblyLine / Cell Nodes
        var lines = await _db.AssemblyLines.ToListAsync(ct);
        var lineNodeMap = new Dictionary<int, HierarchyNode>();

        foreach (var line in lines)
        {
            int? parentNodeId = line.DepartmentId.HasValue && deptNodeMap.TryGetValue(line.DepartmentId.Value, out var deptNode) ? deptNode.NodeId : null;
            var nodeType = line.UnitType == ProductionUnitType.Cell ? HierarchyNodeType.Cell : HierarchyNodeType.AssemblyLine;
            var node = await CreateNodeAsync(line.AssemblyName, nodeType, parentNodeId, line.AssemblyCode, null, ct);
            lineNodeMap[line.AssemblyLineId] = node;
        }

        // 5. Stations -> WorkStation / Subcell Nodes
        var stations = await _db.WorkStations.ToListAsync(ct);

        foreach (var station in stations)
        {
            int? parentNodeId = lineNodeMap.TryGetValue(station.AssemblyLineId, out var lineNode) ? lineNode.NodeId : null;
            var nodeType = station.StationType == StationType.Subcell ? HierarchyNodeType.Subcell : HierarchyNodeType.WorkStation;
            await CreateNodeAsync(station.StationName, nodeType, parentNodeId, station.StationCode, null, ct);
        }

        // Sync existing users' HierarchyNodeId if they have DepartmentId or PlantId
        var users = await _db.Users.ToListAsync(ct);
        foreach (var u in users)
        {
            if (u.DepartmentId.HasValue && deptNodeMap.TryGetValue(u.DepartmentId.Value, out var dn))
            {
                u.HierarchyNodeId = dn.NodeId;
            }
            else if (u.PlantId.HasValue && plantNodeMap.TryGetValue(u.PlantId.Value, out var pn))
            {
                u.HierarchyNodeId = pn.NodeId;
            }
            else if (u.CompanyId.HasValue && companyNodeMap.TryGetValue(u.CompanyId.Value, out var cn))
            {
                u.HierarchyNodeId = cn.NodeId;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task SyncHierarchyWithRelationalEntitiesAsync(CancellationToken ct = default)
    {
        // 1. Fetch all active hierarchy nodes ordered by Depth
        var activeNodes = await _db.HierarchyNodes
            .AsNoTracking()
            .Where(n => n.IsActive)
            .OrderBy(n => n.Depth)
            .ToListAsync(ct);

        if (!activeNodes.Any())
        {
            return;
        }

        var nodeLookup = activeNodes.ToDictionary(n => n.NodeId);

        // 2. Fetch all relational entities
        var companies = await _db.Companies.ToListAsync(ct);
        var plants = await _db.Plants.ToListAsync(ct);
        var departments = await _db.Departments.ToListAsync(ct);
        var lines = await _db.AssemblyLines.ToListAsync(ct);
        var stations = await _db.WorkStations.ToListAsync(ct);

        // Track active matched entities so we can deactivate anything else (stale data purge)
        var matchedCompanyIds = new HashSet<int>();
        var matchedPlantIds = new HashSet<int>();
        var matchedDeptIds = new HashSet<int>();
        var matchedLineIds = new HashSet<int>();
        var matchedStationIds = new HashSet<int>();

        var nodeToCompanyId = new Dictionary<int, int>();
        var nodeToPlantId = new Dictionary<int, int>();
        var nodeToDeptId = new Dictionary<int, int>();
        var nodeToLineId = new Dictionary<int, int>();

        // A. Synchronize Enterprise / Business nodes -> Companies
        foreach (var node in activeNodes.Where(n => n.NodeType is HierarchyNodeType.Enterprise or HierarchyNodeType.Business))
        {
            var comp = companies.FirstOrDefault(c =>
                c.Code.Equals(node.Code, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Equals(node.Name, StringComparison.OrdinalIgnoreCase));

            if (comp == null)
            {
                comp = new Company
                {
                    Name = node.Name,
                    Code = node.Code,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Companies.Add(comp);
                await _db.SaveChangesAsync(ct);
                companies.Add(comp);
            }
            else
            {
                comp.Name = node.Name;
                comp.Code = node.Code;
                comp.IsActive = true;
            }

            matchedCompanyIds.Add(comp.CompanyId);
            nodeToCompanyId[node.NodeId] = comp.CompanyId;
        }

        // B. Synchronize Plant nodes -> Plants
        foreach (var node in activeNodes.Where(n => n.NodeType == HierarchyNodeType.Plant))
        {
            var ancestorEnterpriseNode = FindAncestor(node, n => n.NodeType == HierarchyNodeType.Enterprise, nodeLookup);
            var ancestorCompNode = ancestorEnterpriseNode
                ?? FindAncestor(node, n => n.NodeType == HierarchyNodeType.Business, nodeLookup);
            int compId = ancestorCompNode != null && nodeToCompanyId.TryGetValue(ancestorCompNode.NodeId, out var cid)
                ? cid
                : matchedCompanyIds.FirstOrDefault();

            if (compId == 0 && companies.Any())
                compId = companies.First().CompanyId;

            var plant = plants.FirstOrDefault(p =>
                p.Code.Equals(node.Code, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals(node.Name, StringComparison.OrdinalIgnoreCase));

            if (plant == null)
            {
                plant = new Plant
                {
                    CompanyId = compId,
                    Name = node.Name,
                    Code = node.Code,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Plants.Add(plant);
                await _db.SaveChangesAsync(ct);
                plants.Add(plant);
            }
            else
            {
                plant.CompanyId = compId;
                plant.Name = node.Name;
                plant.Code = node.Code;
                plant.IsActive = true;
            }

            matchedPlantIds.Add(plant.PlantId);
            nodeToPlantId[node.NodeId] = plant.PlantId;
        }

        // C. Synchronize Department nodes -> Departments
        foreach (var node in activeNodes.Where(n => n.NodeType == HierarchyNodeType.Department))
        {
            var ancestorPlantNode = FindAncestor(node, n => n.NodeType == HierarchyNodeType.Plant, nodeLookup);
            int plantId = ancestorPlantNode != null && nodeToPlantId.TryGetValue(ancestorPlantNode.NodeId, out var pid)
                ? pid
                : matchedPlantIds.FirstOrDefault();

            if (plantId == 0 && plants.Any())
                plantId = plants.First().PlantId;

            var dept = departments.FirstOrDefault(d =>
                d.Code.Equals(node.Code, StringComparison.OrdinalIgnoreCase) ||
                d.Name.Equals(node.Name, StringComparison.OrdinalIgnoreCase));

            if (dept == null)
            {
                dept = new Department
                {
                    PlantId = plantId,
                    Name = node.Name,
                    Code = node.Code,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Departments.Add(dept);
                await _db.SaveChangesAsync(ct);
                departments.Add(dept);
            }
            else
            {
                dept.PlantId = plantId;
                dept.Name = node.Name;
                dept.Code = node.Code;
                dept.IsActive = true;
            }

            matchedDeptIds.Add(dept.DepartmentId);
            nodeToDeptId[node.NodeId] = dept.DepartmentId;
        }

        // D. Synchronize AssemblyLine / Cell nodes -> AssemblyLines
        foreach (var node in activeNodes.Where(n => n.NodeType is HierarchyNodeType.AssemblyLine or HierarchyNodeType.Cell))
        {
            var ancestorDeptNode = FindAncestor(node, n => n.NodeType == HierarchyNodeType.Department, nodeLookup);
            int? deptId = ancestorDeptNode != null && nodeToDeptId.TryGetValue(ancestorDeptNode.NodeId, out var did)
                ? did
                : null;
            string deptName = ancestorDeptNode?.Name ?? "Assembly";

            var unitType = node.NodeType == HierarchyNodeType.Cell ? ProductionUnitType.Cell : ProductionUnitType.Line;

            var line = lines.FirstOrDefault(l =>
                l.AssemblyCode.Equals(node.Code, StringComparison.OrdinalIgnoreCase) ||
                l.AssemblyName.Equals(node.Name, StringComparison.OrdinalIgnoreCase));

            if (line == null)
            {
                line = new AssemblyLine
                {
                    AssemblyCode = node.Code,
                    AssemblyName = node.Name,
                    DepartmentId = deptId,
                    Department = deptName,
                    UnitType = unitType,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "hierarchy-sync"
                };
                _db.AssemblyLines.Add(line);
                await _db.SaveChangesAsync(ct);
                lines.Add(line);
            }
            else
            {
                line.AssemblyCode = node.Code;
                line.AssemblyName = node.Name;
                line.DepartmentId = deptId;
                line.Department = deptName;
                line.UnitType = unitType;
                line.IsActive = true;
            }

            matchedLineIds.Add(line.AssemblyLineId);
            nodeToLineId[node.NodeId] = line.AssemblyLineId;
        }

        // E. Synchronize WorkStation / Subcell nodes -> WorkStations
        foreach (var node in activeNodes.Where(n => n.NodeType is HierarchyNodeType.WorkStation or HierarchyNodeType.Subcell))
        {
            var ancestorLineNode = FindAncestor(node, n => n.NodeType is HierarchyNodeType.AssemblyLine or HierarchyNodeType.Cell, nodeLookup);
            int lineId = ancestorLineNode != null && nodeToLineId.TryGetValue(ancestorLineNode.NodeId, out var lid)
                ? lid
                : 0;

            var stationType = node.NodeType == HierarchyNodeType.Subcell ? StationType.Subcell : StationType.Station;

            var ws = stations.FirstOrDefault(s =>
                s.StationCode.Equals(node.Code, StringComparison.OrdinalIgnoreCase) ||
                s.StationName.Equals(node.Name, StringComparison.OrdinalIgnoreCase));

            if (ws == null)
            {
                ws = new WorkStation
                {
                    AssemblyLineId = lineId,
                    StationCode = node.Code,
                    StationName = node.Name,
                    StationType = stationType,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.WorkStations.Add(ws);
                await _db.SaveChangesAsync(ct);
                stations.Add(ws);
            }
            else
            {
                ws.AssemblyLineId = lineId;
                ws.StationCode = node.Code;
                ws.StationName = node.Name;
                ws.StationType = stationType;
                ws.IsActive = true;
            }

            matchedStationIds.Add(ws.WorkStationId);
        }

        // F. DEACTIVATE ANY ENTITY NOT IN ACTIVE HIERARCHY (STALE DATA PURGE)
        foreach (var c in companies.Where(c => !matchedCompanyIds.Contains(c.CompanyId)))
        {
            c.IsActive = false;
        }

        foreach (var p in plants.Where(p => !matchedPlantIds.Contains(p.PlantId)))
        {
            p.IsActive = false;
        }

        foreach (var d in departments.Where(d => !matchedDeptIds.Contains(d.DepartmentId)))
        {
            d.IsActive = false;
        }

        foreach (var l in lines.Where(l => !matchedLineIds.Contains(l.AssemblyLineId)))
        {
            l.IsActive = false;
        }

        foreach (var s in stations.Where(s => !matchedStationIds.Contains(s.WorkStationId)))
        {
            s.IsActive = false;
        }

        // G. Clean / Repair ProductAssemblyMappings pointing to inactive lines
        var mappings = await _db.ProductAssemblyMappings.ToListAsync(ct);
        foreach (var m in mappings)
        {
            if (!matchedLineIds.Contains(m.AssemblyLineId))
            {
                var oldLine = lines.FirstOrDefault(l => l.AssemblyLineId == m.AssemblyLineId);
                var replacementLine = lines.FirstOrDefault(l => matchedLineIds.Contains(l.AssemblyLineId) &&
                    (l.AssemblyCode.Equals(oldLine?.AssemblyCode, StringComparison.OrdinalIgnoreCase) ||
                     l.AssemblyName.Equals(oldLine?.AssemblyName, StringComparison.OrdinalIgnoreCase) ||
                     (oldLine?.AssemblyCode == "ASSEMBLY 1" && l.AssemblyCode == "ASM-01") ||
                     (oldLine?.AssemblyCode == "ASSEMBLY 2" && l.AssemblyCode == "ASM-03")));

                if (replacementLine != null)
                {
                    m.AssemblyLineId = replacementLine.AssemblyLineId;
                }
                else
                {
                    _db.ProductAssemblyMappings.Remove(m);
                }
            }
        }

        // H. Sync active Users to their hierarchy context
        var users = await _db.Users.Where(u => u.HierarchyNodeId != null).ToListAsync(ct);
        foreach (var u in users)
        {
            if (nodeLookup.TryGetValue(u.HierarchyNodeId!.Value, out var uNode))
            {
                if (nodeToLineId.TryGetValue(uNode.NodeId, out var lid))
                    u.CurrentAssemblyLineId = lid;

                var deptNode = uNode.NodeType == HierarchyNodeType.Department
                    ? uNode
                    : FindAncestor(uNode, n => n.NodeType == HierarchyNodeType.Department, nodeLookup);
                if (deptNode != null && nodeToDeptId.TryGetValue(deptNode.NodeId, out var did))
                    u.DepartmentId = did;

                var plantNode = uNode.NodeType == HierarchyNodeType.Plant
                    ? uNode
                    : FindAncestor(uNode, n => n.NodeType == HierarchyNodeType.Plant, nodeLookup);
                if (plantNode != null && nodeToPlantId.TryGetValue(plantNode.NodeId, out var pid))
                    u.PlantId = pid;

                var compNode = (uNode.NodeType is HierarchyNodeType.Enterprise or HierarchyNodeType.Business)
                    ? uNode
                    : FindAncestor(uNode, n => n.NodeType == HierarchyNodeType.Enterprise, nodeLookup)
                      ?? FindAncestor(uNode, n => n.NodeType == HierarchyNodeType.Business, nodeLookup);
                if (compNode != null && nodeToCompanyId.TryGetValue(compNode.NodeId, out var cid))
                    u.CompanyId = cid;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private static List<int> GetAllAncestorIds(HierarchyNode node, Dictionary<int, HierarchyNode> lookup, Dictionary<int, int>? nodeToRelationalId = null)
    {
        var result = new List<int>();
        var curr = node;
        while (curr.ParentNodeId.HasValue && lookup.TryGetValue(curr.ParentNodeId.Value, out var parent))
        {
            result.Add(parent.NodeId);
            if (nodeToRelationalId != null && nodeToRelationalId.TryGetValue(parent.NodeId, out var relId))
            {
                result.Add(relId);
            }
            curr = parent;
        }
        return result.Distinct().ToList();
    }

    private static HierarchyNode? FindAncestor(HierarchyNode node, Func<HierarchyNode, bool> predicate, Dictionary<int, HierarchyNode> lookup)
    {
        var curr = node;
        while (curr.ParentNodeId.HasValue && lookup.TryGetValue(curr.ParentNodeId.Value, out var parent))
        {
            if (predicate(parent)) return parent;
            curr = parent;
        }
        return null;
    }

    private static string GenerateCode(string name)
    {
        var cleaned = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (cleaned.Length <= 8 && cleaned.Length >= 2) return cleaned;
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            var initials = new string(words.Select(w => char.ToUpperInvariant(w[0])).ToArray());
            return (initials + "-" + Random.Shared.Next(10, 99)).ToUpperInvariant();
        }
        var prefix = cleaned.Length > 0 ? cleaned[..Math.Min(cleaned.Length, 6)] : "NODE";
        return (prefix + "-" + Random.Shared.Next(10, 99)).ToUpperInvariant();
    }
}