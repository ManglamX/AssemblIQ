using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

public class FlexibleHierarchyGraphTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateNode_GeneratesCorrectDepthAndMaterializedPath()
    {
        using var db = CreateInMemoryDb();
        var hierarchy = new HierarchyService(db);

        // 1. Enterprise Root
        var root = await hierarchy.CreateNodeAsync("Godrej Enterprises", HierarchyNodeType.Enterprise, null, "GODREJ");
        root.Depth.Should().Be(0);
        root.Path.Should().Be($"/{root.NodeId}/");

        // 2. Child Plant
        var plant = await hierarchy.CreateNodeAsync("Pune Plant", HierarchyNodeType.Plant, root.NodeId, "PUNE-01");
        plant.Depth.Should().Be(1);
        plant.Path.Should().Be($"/{root.NodeId}/{plant.NodeId}/");

        // 3. Child Department
        var dept = await hierarchy.CreateNodeAsync("Mechanical Assembly", HierarchyNodeType.Department, plant.NodeId, "MECH-ASM");
        dept.Depth.Should().Be(2);
        dept.Path.Should().Be($"/{root.NodeId}/{plant.NodeId}/{dept.NodeId}/");
    }

    [Fact]
    public async Task AttachOrLinkNode_PreventsCyclicRelationships()
    {
        using var db = CreateInMemoryDb();
        var hierarchy = new HierarchyService(db);

        var enterprise = await hierarchy.CreateNodeAsync("Enterprise", HierarchyNodeType.Enterprise, null);
        var plant = await hierarchy.CreateNodeAsync("Plant 1", HierarchyNodeType.Plant, enterprise.NodeId);
        var dept = await hierarchy.CreateNodeAsync("Dept 1", HierarchyNodeType.Department, plant.NodeId);

        // Attempting to reparent enterprise under dept should throw InvalidOperationException (cycle)
        var act = async () => await hierarchy.AttachOrLinkNodeAsync(enterprise.NodeId, dept.NodeId, "admin-1");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cyclic*");
    }

    [Fact]
    public async Task AttachOrLinkNode_UpdatesDescendantPathsAndDepths()
    {
        using var db = CreateInMemoryDb();
        var hierarchy = new HierarchyService(db);

        var ent = await hierarchy.CreateNodeAsync("Enterprise", HierarchyNodeType.Enterprise, null);
        var plantA = await hierarchy.CreateNodeAsync("Plant A", HierarchyNodeType.Plant, ent.NodeId);
        var plantB = await hierarchy.CreateNodeAsync("Plant B", HierarchyNodeType.Plant, ent.NodeId);

        var dept = await hierarchy.CreateNodeAsync("Dept A1", HierarchyNodeType.Department, plantA.NodeId);
        var line = await hierarchy.CreateNodeAsync("Line 1", HierarchyNodeType.AssemblyLine, dept.NodeId);

        line.Path.Should().Be($"/{ent.NodeId}/{plantA.NodeId}/{dept.NodeId}/{line.NodeId}/");
        line.Depth.Should().Be(3);

        // Move Dept A1 under Plant B
        var success = await hierarchy.AttachOrLinkNodeAsync(dept.NodeId, plantB.NodeId, "admin-1");
        success.Should().BeTrue();

        var updatedDept = await db.HierarchyNodes.FindAsync(dept.NodeId);
        var updatedLine = await db.HierarchyNodes.FindAsync(line.NodeId);

        updatedDept!.ParentNodeId.Should().Be(plantB.NodeId);
        updatedDept.Path.Should().Be($"/{ent.NodeId}/{plantB.NodeId}/{dept.NodeId}/");
        updatedDept.Depth.Should().Be(2);

        updatedLine!.Path.Should().Be($"/{ent.NodeId}/{plantB.NodeId}/{dept.NodeId}/{line.NodeId}/");
        updatedLine.Depth.Should().Be(3);
    }

    [Fact]
    public async Task RequestAndApproveChildNode_CreatesNodeAndBindsUser()
    {
        using var db = CreateInMemoryDb();
        var hierarchy = new HierarchyService(db);

        var plantAdmin = new ApplicationUser
        {
            Id = "admin-plant",
            UserName = "admin@plant.godrej.com",
            FullName = "Plant Admin",
            Email = "admin@plant.godrej.com"
        };
        db.Users.Add(plantAdmin);

        var applicant = new ApplicationUser
        {
            Id = "worker-new",
            UserName = "newworker@godrej.com",
            FullName = "New Worker",
            Email = "newworker@godrej.com",
            RegistrationStatus = RegistrationStatus.Pending,
            IsActive = false
        };
        db.Users.Add(applicant);

        var plant = await hierarchy.CreateNodeAsync("Pune Plant", HierarchyNodeType.Plant, null, "PUNE-PLANT", plantAdmin.Id);

        // Worker requests missing department under Pune Plant
        var request = await hierarchy.RequestChildNodeAsync(plant.NodeId, "Robotics Cell", HierarchyNodeType.Department, applicant.Id);
        request.RequestStatus.Should().Be(NodeRequestStatus.Pending);
        request.ApproverUserId.Should().Be(plantAdmin.Id);

        // Plant admin approves request
        var createdDept = await hierarchy.ApproveChildNodeRequestAsync(request.RequestId, plantAdmin.Id);
        createdDept.Name.Should().Be("Robotics Cell");
        createdDept.ParentNodeId.Should().Be(plant.NodeId);

        var updatedUser = await db.Users.FindAsync(applicant.Id);
        updatedUser!.HierarchyNodeId.Should().Be(createdDept.NodeId);
        updatedUser.RegistrationStatus.Should().Be(RegistrationStatus.Approved);
        updatedUser.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteNode_ReattachesChildrenToGrandparent_NoDanglingNodes()
    {
        using var db = CreateInMemoryDb();
        var hierarchy = new HierarchyService(db);

        // Hierarchy: Ent (0) -> Plant (1) -> Dept (2) -> Line (3)
        var ent = await hierarchy.CreateNodeAsync("Godrej Enterprises", HierarchyNodeType.Enterprise, null);
        var plant = await hierarchy.CreateNodeAsync("Pune Plant", HierarchyNodeType.Plant, ent.NodeId);
        var dept = await hierarchy.CreateNodeAsync("Mechanical Assembly", HierarchyNodeType.Department, plant.NodeId);
        var line = await hierarchy.CreateNodeAsync("Assembly Line 1", HierarchyNodeType.AssemblyLine, dept.NodeId);

        // Delete 'plant' (the parent of 'dept').
        // 'dept' should now be directly attached to 'ent' (the grandparent).
        var deleted = await hierarchy.DeleteNodeAsync(plant.NodeId, "admin-user");
        deleted.Should().BeTrue();

        var updatedDept = await db.HierarchyNodes.FindAsync(dept.NodeId);
        var updatedLine = await db.HierarchyNodes.FindAsync(line.NodeId);
        var deletedPlant = await db.HierarchyNodes.FindAsync(plant.NodeId);

        deletedPlant!.IsActive.Should().BeFalse();

        // Dept should now be child of Enterprise
        updatedDept!.ParentNodeId.Should().Be(ent.NodeId);
        updatedDept.Depth.Should().Be(1);
        updatedDept.Path.Should().Be($"/{ent.NodeId}/{dept.NodeId}/");

        // Line should still be child of Dept with updated materialized path and depth
        updatedLine!.ParentNodeId.Should().Be(dept.NodeId);
        updatedLine.Depth.Should().Be(2);
        updatedLine.Path.Should().Be($"/{ent.NodeId}/{dept.NodeId}/{line.NodeId}/");
    }

    [Fact]
    public async Task DeleteRootNode_ChildrenBecomeRootNodes()
    {
        using var db = CreateInMemoryDb();
        var hierarchy = new HierarchyService(db);

        var root = await hierarchy.CreateNodeAsync("Root Enterprise", HierarchyNodeType.Enterprise, null);
        var plant = await hierarchy.CreateNodeAsync("Child Plant", HierarchyNodeType.Plant, root.NodeId);

        var deleted = await hierarchy.DeleteNodeAsync(root.NodeId, "admin-user");
        deleted.Should().BeTrue();

        var updatedPlant = await db.HierarchyNodes.FindAsync(plant.NodeId);
        updatedPlant!.ParentNodeId.Should().BeNull();
        updatedPlant.Depth.Should().Be(0);
        updatedPlant.Path.Should().Be($"/{plant.NodeId}/");
    }
}