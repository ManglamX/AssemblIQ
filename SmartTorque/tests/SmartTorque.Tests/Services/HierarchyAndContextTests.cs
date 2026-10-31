using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using Xunit;

namespace SmartTorque.Tests.Services;

public class HierarchyAndContextTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task HierarchyService_GetCascadeOptionsAsync_ReturnsFullEnterpriseTree()
    {
        using var db = CreateInMemoryDbContext();

        var company = new Company { CompanyId = 1, Code = "GODREJ", Name = "Godrej Enterprises", IsActive = true };
        var plant = new Plant { PlantId = 1, CompanyId = 1, Code = "PLANT-01", Name = "Plant 1 - Vikhroli", IsActive = true };
        var dept1 = new Department { DepartmentId = 1, PlantId = 1, Code = "MECH-ASM", Name = "Mechanical Assembly", IsActive = true };
        var dept2 = new Department { DepartmentId = 2, PlantId = 1, Code = "FAB", Name = "Fabrication", IsActive = true };

        var line = new AssemblyLine { AssemblyLineId = 1, DepartmentId = 1, AssemblyCode = "ASM-01", AssemblyName = "Assembly Line 01", UnitType = ProductionUnitType.Line, IsActive = true };
        var cell = new AssemblyLine { AssemblyLineId = 2, DepartmentId = 1, AssemblyCode = "CELL-01", AssemblyName = "Robotic Cell 01", UnitType = ProductionUnitType.Cell, IsActive = true };

        var station = new WorkStation { WorkStationId = 1, AssemblyLineId = 1, StationCode = "ST-01", StationName = "Station 1", StationType = StationType.Station, IsActive = true };
        var subcell = new WorkStation { WorkStationId = 2, AssemblyLineId = 2, StationCode = "SUB-01", StationName = "Subcell 1", StationType = StationType.Subcell, IsActive = true };

        db.Companies.Add(company);
        db.Plants.Add(plant);
        db.Departments.AddRange(dept1, dept2);
        db.AssemblyLines.AddRange(line, cell);
        db.WorkStations.AddRange(station, subcell);
        await db.SaveChangesAsync();

        var hierarchy = new HierarchyService(db);
        var options = await hierarchy.GetCascadeOptionsAsync();

        options.Companies.Should().HaveCount(1);
        options.Plants.Should().HaveCount(1);
        options.Departments.Should().HaveCount(2);
        options.Units.Should().HaveCount(2);
        options.Stations.Should().HaveCount(2);

        options.Units.First(u => u.Id == 1).Type.Should().Be("Line");
        options.Units.First(u => u.Id == 2).Type.Should().Be("Cell");
        options.Stations.First(s => s.Id == 1).Type.Should().Be("Station");
        options.Stations.First(s => s.Id == 2).Type.Should().Be("Subcell");
    }

    [Fact]
    public void UserContextDto_ScopingRules_EnforceZeroCrossSeeingForDeptPersonnel()
    {
        var plantHead = new UserContextDto
        {
            UserId = "admin-1",
            Email = "head@godrej.com",
            RoleName = "Admin",
            IsMainAdmin = true,
            IsPlantHead = true,
            DepartmentId = 1,
            DepartmentName = "Mechanical Assembly"
        };

        var deptSupervisor = new UserContextDto
        {
            UserId = "sup-1",
            Email = "supervisor@godrej.com",
            RoleName = "Admin",
            IsMainAdmin = false,
            IsPlantHead = false,
            DepartmentId = 1,
            DepartmentName = "Mechanical Assembly"
        };

        var workerDept2 = new UserContextDto
        {
            UserId = "worker-2",
            Email = "fabworker@godrej.com",
            RoleName = "Worker",
            IsWorker = true,
            DepartmentId = 2,
            DepartmentName = "Fabrication"
        };

        // Plant Head can see all departments across the plant
        plantHead.HasFullPlantAccess.Should().BeTrue();
        plantHead.CanAccessDepartment(1).Should().BeTrue();
        plantHead.CanAccessDepartment(2).Should().BeTrue();
        plantHead.CanAccessDepartment(999).Should().BeTrue();
        // Plant Head can filter to a specific department or view all (null)
        plantHead.GetEffectiveDepartmentId(2).Should().Be(2);
        plantHead.GetEffectiveDepartmentId(null).Should().BeNull();

        // Department supervisor is strictly locked to Dept 1
        deptSupervisor.HasFullPlantAccess.Should().BeFalse();
        deptSupervisor.CanAccessDepartment(1).Should().BeTrue();
        deptSupervisor.CanAccessDepartment(2).Should().BeFalse();
        // Effective department is always locked to 1 regardless of requested filter
        deptSupervisor.GetEffectiveDepartmentId(2).Should().Be(1);
        deptSupervisor.GetEffectiveDepartmentId(null).Should().Be(1);

        // Worker in Dept 2 is strictly locked to Dept 2
        workerDept2.HasFullPlantAccess.Should().BeFalse();
        workerDept2.CanAccessDepartment(2).Should().BeTrue();
        workerDept2.CanAccessDepartment(1).Should().BeFalse();
        workerDept2.GetEffectiveDepartmentId(1).Should().Be(2);
    }

    [Fact]
    public async Task ProductService_ListAsync_FiltersByDepartmentIdStrictly()
    {
        using var db = CreateInMemoryDbContext();

        var p1 = new Product { ProductId = 1, ProductCode = "MECH-01", ProductName = "Locker Door", DepartmentId = 1, IsActive = true, CreatedBy = "admin" };
        var p2 = new Product { ProductId = 2, ProductCode = "MECH-02", ProductName = "Locker Frame", DepartmentId = 1, IsActive = true, CreatedBy = "admin" };
        var p3 = new Product { ProductId = 3, ProductCode = "FAB-01", ProductName = "Bracket Panel", DepartmentId = 2, IsActive = true, CreatedBy = "admin" };

        db.Products.AddRange(p1, p2, p3);
        await db.SaveChangesAsync();

        var productService = new ProductService(db);

        // Filter Dept 1: should return only MECH products
        var dept1Products = await productService.ListAsync(new ProductFilter(null, null, 1, 20, 1));
        dept1Products.Should().HaveCount(2);
        dept1Products.Select(p => p.ProductCode).Should().Contain(["MECH-01", "MECH-02"]);
        dept1Products.Select(p => p.ProductCode).Should().NotContain("FAB-01");

        // Filter Dept 2: should return only FAB products
        var dept2Products = await productService.ListAsync(new ProductFilter(null, null, 1, 20, 2));
        dept2Products.Should().HaveCount(1);
        dept2Products.First().ProductCode.Should().Be("FAB-01");

        // Filter null (Plant Head viewing all): should return all 3 products
        var allProducts = await productService.ListAsync(new ProductFilter(null, null, 1, 20, null));
        allProducts.Should().HaveCount(3);
    }
}
