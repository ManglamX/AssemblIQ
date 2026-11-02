using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using Xunit;
using Xunit.Abstractions;

namespace SmartTorque.Tests.Services;

public class HierarchyMigrationRunner
{
    private readonly ITestOutputHelper _output;

    public HierarchyMigrationRunner(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task MigrateRealDatabaseToRequestedHierarchy()
    {
        const string connStr = "Server=localhost;Database=SmartTorqueDb;Integrated Security=True;TrustServerCertificate=True;";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connStr)
            .Options;

        using var db = new AppDbContext(options);
        var hasher = new PasswordHasher<ApplicationUser>();

        _output.WriteLine("Connected to SmartTorqueDb. Preparing to reset hierarchy...");

        // 1. Clear existing HierarchyNodeRequests and clean HierarchyNodes
        await db.Database.ExecuteSqlRawAsync("DELETE FROM HierarchyNodeRequests");
        await db.Database.ExecuteSqlRawAsync("UPDATE AspNetUsers SET HierarchyNodeId = NULL");
        await db.Database.ExecuteSqlRawAsync("UPDATE HierarchyNodes SET ParentNodeId = NULL");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM HierarchyNodes");

        // 2. Company / Enterprise
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Code == "GODREJ");
        if (company == null)
        {
            company = new Company { Name = "Godrej Enterprises", Code = "GODREJ", IsActive = true, CreatedAt = DateTime.UtcNow };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
        }

        // 3. Plant 17
        var plant17 = await db.Plants.FirstOrDefaultAsync(p => p.Code == "PLANT-17");
        if (plant17 == null)
        {
            plant17 = new Plant
            {
                CompanyId = company.CompanyId,
                Name = "Plant 17",
                Code = "PLANT-17",
                Location = "Godrej Complex, Pirojshanagar, Vikhroli",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Plants.Add(plant17);
            await db.SaveChangesAsync();
        }

        // 4. Departments under Plant 17: Engg Cell, SDLC, Tech Cell
        var deptEngg = await db.Departments.FirstOrDefaultAsync(d => d.Code == "ENGG-CELL");
        if (deptEngg == null)
        {
            deptEngg = new Department { PlantId = plant17.PlantId, Name = "Engg Cell", Code = "ENGG-CELL", IsActive = true, CreatedAt = DateTime.UtcNow };
            db.Departments.Add(deptEngg);
        }
        else
        {
            deptEngg.PlantId = plant17.PlantId;
            deptEngg.Name = "Engg Cell";
        }

        var deptSdlc = await db.Departments.FirstOrDefaultAsync(d => d.Code == "SDLC");
        if (deptSdlc == null)
        {
            deptSdlc = new Department { PlantId = plant17.PlantId, Name = "SDLC", Code = "SDLC", IsActive = true, CreatedAt = DateTime.UtcNow };
            db.Departments.Add(deptSdlc);
        }
        else
        {
            deptSdlc.PlantId = plant17.PlantId;
            deptSdlc.Name = "SDLC";
        }

        var deptTech = await db.Departments.FirstOrDefaultAsync(d => d.Code == "TECH-CELL");
        if (deptTech == null)
        {
            deptTech = new Department { PlantId = plant17.PlantId, Name = "Tech Cell", Code = "TECH-CELL", IsActive = true, CreatedAt = DateTime.UtcNow };
            db.Departments.Add(deptTech);
        }
        else
        {
            deptTech.PlantId = plant17.PlantId;
            deptTech.Name = "Tech Cell";
        }

        await db.SaveChangesAsync();

        // 5. Lines and Cells
        async Task<AssemblyLine> EnsureLineAsync(string code, string name, int deptId, string deptName, ProductionUnitType unitType)
        {
            var line = await db.AssemblyLines.FirstOrDefaultAsync(l => l.AssemblyCode == code);
            if (line == null)
            {
                line = new AssemblyLine
                {
                    AssemblyCode = code,
                    AssemblyName = name,
                    DepartmentId = deptId,
                    Department = deptName,
                    UnitType = unitType,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                };
                db.AssemblyLines.Add(line);
                await db.SaveChangesAsync();
            }
            else
            {
                line.AssemblyName = name;
                line.DepartmentId = deptId;
                line.Department = deptName;
                line.UnitType = unitType;
                await db.SaveChangesAsync();
            }
            return line;
        }

        var asm1 = await EnsureLineAsync("ASM-01", "Assembly 1", deptEngg.DepartmentId, deptEngg.Name, ProductionUnitType.Line);
        var asm2 = await EnsureLineAsync("ASM-02", "Assembly 2", deptEngg.DepartmentId, deptEngg.Name, ProductionUnitType.Line);
        var cellElec = await EnsureLineAsync("CELL-ELEC", "Electronics Cell", deptEngg.DepartmentId, deptEngg.Name, ProductionUnitType.Cell);

        var asm3 = await EnsureLineAsync("ASM-03", "Assembly 3", deptSdlc.DepartmentId, deptSdlc.Name, ProductionUnitType.Line);
        var asm4 = await EnsureLineAsync("ASM-04", "Assembly 4", deptSdlc.DepartmentId, deptSdlc.Name, ProductionUnitType.Line);

        var asm5 = await EnsureLineAsync("ASM-05", "Assembly 5", deptTech.DepartmentId, deptTech.Name, ProductionUnitType.Line);
        var asm6 = await EnsureLineAsync("ASM-06", "Assembly 6", deptTech.DepartmentId, deptTech.Name, ProductionUnitType.Line);

        // Subcell under Electronics Cell
        var subcell = await db.WorkStations.FirstOrDefaultAsync(w => w.StationCode == "SUB-ELEC-01");
        if (subcell == null)
        {
            subcell = new WorkStation
            {
                AssemblyLineId = cellElec.AssemblyLineId,
                StationCode = "SUB-ELEC-01",
                StationName = "Electronics Subcell 1",
                StationType = StationType.Subcell,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.WorkStations.Add(subcell);
            await db.SaveChangesAsync();
        }

        // 6. Build HierarchyNodes graph
        // Level 0: Enterprise
        var enterpriseNode = new HierarchyNode
        {
            Name = "Godrej Enterprises",
            Code = "GODREJ",
            NodeType = HierarchyNodeType.Enterprise,
            Depth = 0,
            Path = "",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.HierarchyNodes.Add(enterpriseNode);
        await db.SaveChangesAsync();
        enterpriseNode.Path = $"/{enterpriseNode.NodeId}/";
        await db.SaveChangesAsync();

        // Level 1: Business (Security Solution Business)
        var businessNode = new HierarchyNode
        {
            Name = "Security Solution Business",
            Code = "SSB-50",
            NodeType = HierarchyNodeType.Business,
            ParentNodeId = enterpriseNode.NodeId,
            Depth = 1,
            Path = "",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.HierarchyNodes.Add(businessNode);
        await db.SaveChangesAsync();
        businessNode.Path = $"{enterpriseNode.Path}{businessNode.NodeId}/";
        await db.SaveChangesAsync();

        // Level 2: Plant (Plant 17)
        var plant17Node = new HierarchyNode
        {
            Name = "Plant 17",
            Code = "PLANT-17",
            NodeType = HierarchyNodeType.Plant,
            ParentNodeId = businessNode.NodeId,
            Depth = 2,
            Path = "",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.HierarchyNodes.Add(plant17Node);
        await db.SaveChangesAsync();
        plant17Node.Path = $"{businessNode.Path}{plant17Node.NodeId}/";
        await db.SaveChangesAsync();

        // Level 3: Departments
        async Task<HierarchyNode> CreateDeptNodeAsync(string name, string code)
        {
            var node = new HierarchyNode
            {
                Name = name,
                Code = code,
                NodeType = HierarchyNodeType.Department,
                ParentNodeId = plant17Node.NodeId,
                Depth = 3,
                Path = "",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.HierarchyNodes.Add(node);
            await db.SaveChangesAsync();
            node.Path = $"{plant17Node.Path}{node.NodeId}/";
            await db.SaveChangesAsync();
            return node;
        }

        var nodeDeptEngg = await CreateDeptNodeAsync("Engg Cell", "ENGG-CELL");
        var nodeDeptSdlc = await CreateDeptNodeAsync("SDLC", "SDLC");
        var nodeDeptTech = await CreateDeptNodeAsync("Tech Cell", "TECH-CELL");

        // Level 4: Assembly Lines & Cells under Engg Cell
        async Task<HierarchyNode> CreateChildNodeAsync(string name, string code, HierarchyNodeType nodeType, int parentNodeId, string parentPath)
        {
            var node = new HierarchyNode
            {
                Name = name,
                Code = code,
                NodeType = nodeType,
                ParentNodeId = parentNodeId,
                Depth = 4,
                Path = "",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.HierarchyNodes.Add(node);
            await db.SaveChangesAsync();
            node.Path = $"{parentPath}{node.NodeId}/";
            await db.SaveChangesAsync();
            return node;
        }

        var nodeAsm1 = await CreateChildNodeAsync("Assembly 1", "ASM-01", HierarchyNodeType.AssemblyLine, nodeDeptEngg.NodeId, nodeDeptEngg.Path);
        var nodeAsm2 = await CreateChildNodeAsync("Assembly 2", "ASM-02", HierarchyNodeType.AssemblyLine, nodeDeptEngg.NodeId, nodeDeptEngg.Path);
        var nodeCellElec = await CreateChildNodeAsync("Electronics Cell", "CELL-ELEC", HierarchyNodeType.Cell, nodeDeptEngg.NodeId, nodeDeptEngg.Path);

        // Subcell under Electronics Cell
        var nodeSubcell = new HierarchyNode
        {
            Name = "Electronics Subcell 1",
            Code = "SUB-ELEC-01",
            NodeType = HierarchyNodeType.Subcell,
            ParentNodeId = nodeCellElec.NodeId,
            Depth = 5,
            Path = "",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.HierarchyNodes.Add(nodeSubcell);
        await db.SaveChangesAsync();
        nodeSubcell.Path = $"{nodeCellElec.Path}{nodeSubcell.NodeId}/";
        await db.SaveChangesAsync();

        // Level 4: Lines under SDLC
        var nodeAsm3 = await CreateChildNodeAsync("Assembly 3", "ASM-03", HierarchyNodeType.AssemblyLine, nodeDeptSdlc.NodeId, nodeDeptSdlc.Path);
        var nodeAsm4 = await CreateChildNodeAsync("Assembly 4", "ASM-04", HierarchyNodeType.AssemblyLine, nodeDeptSdlc.NodeId, nodeDeptSdlc.Path);

        // Level 4: Lines under Tech Cell
        var nodeAsm5 = await CreateChildNodeAsync("Assembly 5", "ASM-05", HierarchyNodeType.AssemblyLine, nodeDeptTech.NodeId, nodeDeptTech.Path);
        var nodeAsm6 = await CreateChildNodeAsync("Assembly 6", "ASM-06", HierarchyNodeType.AssemblyLine, nodeDeptTech.NodeId, nodeDeptTech.Path);

        // 7. Ensure Roles exist
        var roles = new[] { "Admin", "Supervisor", "Worker" };
        foreach (var r in roles)
        {
            if (!await db.Roles.AnyAsync(role => role.Name == r))
            {
                db.Roles.Add(new IdentityRole { Id = Guid.NewGuid().ToString(), Name = r, NormalizedName = r.ToUpper() });
            }
        }
        await db.SaveChangesAsync();

        // 8. Create / Update Users for each level
        async Task CreateOrUpdateUserAsync(
            string email,
            string password,
            string fullName,
            string empNo,
            string role,
            int? deptId,
            int? lineId,
            int hierarchyNodeId,
            bool isMainAdmin = false,
            bool isPlantHead = false)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = email,
                    NormalizedUserName = email.ToUpper(),
                    Email = email,
                    NormalizedEmail = email.ToUpper(),
                    EmailConfirmed = true,
                    FullName = fullName,
                    EmployeeNo = empNo,
                    IsActive = true,
                    RegistrationStatus = RegistrationStatus.Approved,
                    RequestedRole = role,
                    CompanyId = company.CompanyId,
                    PlantId = plant17.PlantId,
                    DepartmentId = deptId,
                    CurrentAssemblyLineId = lineId,
                    HierarchyNodeId = hierarchyNodeId,
                    IsMainAdmin = isMainAdmin,
                    IsPlantHead = isPlantHead,
                    CreatedAt = DateTime.UtcNow,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    ConcurrencyStamp = Guid.NewGuid().ToString()
                };
                user.PasswordHash = hasher.HashPassword(user, password);
                db.Users.Add(user);
                await db.SaveChangesAsync();

                var roleEntity = await db.Roles.FirstAsync(r => r.Name == role);
                db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = roleEntity.Id });
                await db.SaveChangesAsync();
            }
            else
            {
                user.FullName = fullName;
                user.EmployeeNo = empNo;
                user.CompanyId = company.CompanyId;
                user.PlantId = plant17.PlantId;
                user.DepartmentId = deptId;
                user.CurrentAssemblyLineId = lineId;
                user.HierarchyNodeId = hierarchyNodeId;
                user.IsMainAdmin = isMainAdmin;
                user.IsPlantHead = isPlantHead;
                user.PasswordHash = hasher.HashPassword(user, password);
                await db.SaveChangesAsync();
            }
        }

        // Enterprise & System Admin
        await CreateOrUpdateUserAsync("admin@smarttorque.local", "Admin@12345", "Enterprise System Admin", "ADMIN-ENT", "Admin", null, null, enterpriseNode.NodeId, isMainAdmin: true, isPlantHead: true);
        await CreateOrUpdateUserAsync("admin@godrej.com", "Admin@12345", "Godrej Enterprise Admin", "ADM-GODREJ", "Admin", null, null, enterpriseNode.NodeId, isMainAdmin: true, isPlantHead: true);

        // Business Admin
        await CreateOrUpdateUserAsync("business.admin@godrej.com", "Business@12345", "Security Solutions Business Head", "BUS-SSB", "Admin", null, null, businessNode.NodeId);

        // Plant 17 Admin / Plant Head
        await CreateOrUpdateUserAsync("plant17.admin@godrej.com", "Plant17@12345", "Plant 17 Head", "PLT-17", "Admin", null, null, plant17Node.NodeId, isPlantHead: true);

        // Department Admins
        await CreateOrUpdateUserAsync("engg.admin@godrej.com", "EnggCell@12345", "Engg Cell Dept Head", "DEP-ENGG", "Admin", deptEngg.DepartmentId, null, nodeDeptEngg.NodeId);
        await CreateOrUpdateUserAsync("sdlc.admin@godrej.com", "Sdlc@12345", "SDLC Dept Head", "DEP-SDLC", "Admin", deptSdlc.DepartmentId, null, nodeDeptSdlc.NodeId);
        await CreateOrUpdateUserAsync("tech.admin@godrej.com", "TechCell@12345", "Tech Cell Dept Head", "DEP-TECH", "Admin", deptTech.DepartmentId, null, nodeDeptTech.NodeId);

        // Supervisors
        await CreateOrUpdateUserAsync("sup.asm1@godrej.com", "Supervisor@12345", "Assembly 1 Supervisor", "SUP-ASM1", "Supervisor", deptEngg.DepartmentId, asm1.AssemblyLineId, nodeAsm1.NodeId);
        await CreateOrUpdateUserAsync("sup.asm2@godrej.com", "Supervisor@12345", "Assembly 2 Supervisor", "SUP-ASM2", "Supervisor", deptEngg.DepartmentId, asm2.AssemblyLineId, nodeAsm2.NodeId);
        await CreateOrUpdateUserAsync("sup.elec@godrej.com", "Supervisor@12345", "Electronics Cell Supervisor", "SUP-ELEC", "Supervisor", deptEngg.DepartmentId, cellElec.AssemblyLineId, nodeCellElec.NodeId);

        await CreateOrUpdateUserAsync("sup.asm3@godrej.com", "Supervisor@12345", "Assembly 3 Supervisor", "SUP-ASM3", "Supervisor", deptSdlc.DepartmentId, asm3.AssemblyLineId, nodeAsm3.NodeId);
        await CreateOrUpdateUserAsync("sup.asm4@godrej.com", "Supervisor@12345", "Assembly 4 Supervisor", "SUP-ASM4", "Supervisor", deptSdlc.DepartmentId, asm4.AssemblyLineId, nodeAsm4.NodeId);

        await CreateOrUpdateUserAsync("sup.asm5@godrej.com", "Supervisor@12345", "Assembly 5 Supervisor", "SUP-ASM5", "Supervisor", deptTech.DepartmentId, asm5.AssemblyLineId, nodeAsm5.NodeId);
        await CreateOrUpdateUserAsync("sup.asm6@godrej.com", "Supervisor@12345", "Assembly 6 Supervisor", "SUP-ASM6", "Supervisor", deptTech.DepartmentId, asm6.AssemblyLineId, nodeAsm6.NodeId);

        // Workers in each department
        await CreateOrUpdateUserAsync("worker.engg@godrej.com", "Worker@12345", "Engg Cell Line Worker", "WRK-ENGG1", "Worker", deptEngg.DepartmentId, asm1.AssemblyLineId, nodeAsm1.NodeId);
        await CreateOrUpdateUserAsync("worker.sdlc@godrej.com", "Worker@12345", "SDLC Assembly Worker", "WRK-SDLC1", "Worker", deptSdlc.DepartmentId, asm3.AssemblyLineId, nodeAsm3.NodeId);
        await CreateOrUpdateUserAsync("worker.tech@godrej.com", "Worker@12345", "Tech Cell Line Worker", "WRK-TECH1", "Worker", deptTech.DepartmentId, asm5.AssemblyLineId, nodeAsm5.NodeId);

        _output.WriteLine("Successfully created exact hierarchy and all requested logins for each level!");
    }

    [Fact]
    public async Task VerifyHierarchyTree()
    {
        const string connStr = "Server=localhost;Database=SmartTorqueDb;Integrated Security=True;TrustServerCertificate=True;";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connStr)
            .Options;

        using var db = new AppDbContext(options);
        var nodes = await db.HierarchyNodes.Include(n => n.ParentNode).OrderBy(n => n.Depth).ThenBy(n => n.NodeId).ToListAsync();
        foreach (var n in nodes)
        {
            _output.WriteLine($"Node #{n.NodeId} [{n.NodeType}] {n.Name} ({n.Code}) -> Parent: {n.ParentNode?.Name ?? "ROOT"}");
        }
        var users = await db.Users.Include(u => u.HierarchyNode).Include(u => u.Department).OrderBy(u => u.UserName).ToListAsync();
        _output.WriteLine($"Total Users: {users.Count}");
        await new SmartTorque.Services.HierarchyService(db).SyncHierarchyWithRelationalEntitiesAsync();
        foreach (var u in users)
        {
            _output.WriteLine($"User: {u.Email} | Role: {u.RequestedRole} | Node: {u.HierarchyNode?.Name} | Dept: {u.Department?.Name}");
        }
    }
}
