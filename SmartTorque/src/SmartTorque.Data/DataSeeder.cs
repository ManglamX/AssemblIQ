using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;

namespace SmartTorque.Data;

/// <summary>
/// Seeds roles, the first Admin user, and default settings. Idempotent.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<AppDbContext>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var config = services.GetRequiredService<IConfiguration>();
        var db = services.GetRequiredService<AppDbContext>();

        await SeedHierarchyAsync(db, logger);
        await SeedHierarchyGraphNodesAsync(db, logger);
        await SeedRolesAsync(roleManager, userManager, logger);
        await SeedAdminAsync(userManager, config, db, logger);
        await SeedSupervisorAsync(userManager, db, logger);
        await SeedWorkerAsync(userManager, db, logger);
        await SeedSettingsAsync(db, logger);
        await SeedTemplatesAsync(db, logger);
        await SeedProductsAsync(db, logger);
        await SeedColourMapAsync(db, logger);
        await SeedDevicesAsync(db, logger);
        await SeedApiKeyDirectoryAsync(db, logger);
    }

    private static async Task SeedRolesAsync(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        string[] roles = ["Admin", "Worker"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Seeded role: {Role}", role);
            }
        }

        // Migrate any legacy Supervisor users to Worker with IsLineSupervisor designation
        var supRole = await roleManager.FindByNameAsync("Supervisor");
        if (supRole != null)
        {
            var supUsers = await userManager.GetUsersInRoleAsync("Supervisor");
            foreach (var u in supUsers)
            {
                u.IsLineSupervisor = true;
                u.RequestedRole = "Worker";
                await userManager.RemoveFromRoleAsync(u, "Supervisor");
                if (!await userManager.IsInRoleAsync(u, "Worker"))
                {
                    await userManager.AddToRoleAsync(u, "Worker");
                }
                await userManager.UpdateAsync(u);
            }
            await roleManager.DeleteAsync(supRole);
            logger.LogInformation("Migrated legacy Supervisor accounts to Worker with Line Supervisor designation.");
        }
    }

    private static async Task SeedAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration config,
        AppDbContext db,
        ILogger logger)
    {
        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Seed:AdminEmail or Seed:AdminPassword not configured. " +
                "Set them in user-secrets to create the first admin.");
            return;
        }

        var company = await db.Companies.FirstOrDefaultAsync(co => co.Code == "GODREJ");
        var plant = await db.Plants.FirstOrDefaultAsync(p => p.Code == "PLANT-01");

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            bool changed = false;
            if (!existingUser.IsMainAdmin) { existingUser.IsMainAdmin = true; changed = true; }
            if (!existingUser.IsPlantHead) { existingUser.IsPlantHead = true; changed = true; }
            if (existingUser.CompanyId == null && company != null) { existingUser.CompanyId = company.CompanyId; changed = true; }
            if (existingUser.PlantId == null && plant != null) { existingUser.PlantId = plant.PlantId; changed = true; }
            if (changed)
            {
                await userManager.UpdateAsync(existingUser);
                logger.LogInformation("Updated {Email} with Main Admin and Plant Head hierarchy", email);
            }
            return; // Already seeded
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "System Admin",
            EmployeeNo = "ADMIN-001",
            IsActive = true,
            IsMainAdmin = true,
            IsPlantHead = true,
            CompanyId = company?.CompanyId,
            PlantId = plant?.PlantId,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
            logger.LogInformation("Seeded default main admin: {Email}", email);
        }
        else
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            logger.LogError("Failed to seed admin: {Errors}", errors);
        }
    }

    private static async Task SeedWorkerAsync(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        ILogger logger)
    {
        const string email = "worker1@smarttorque.local";
        const string password = "Worker@12345";

        var company = await db.Companies.FirstOrDefaultAsync(co => co.Code == "GODREJ");
        var plant = await db.Plants.FirstOrDefaultAsync(p => p.Code == "PLANT-01");
        var mechDept = await db.Departments.FirstOrDefaultAsync(d => d.Code == "MECH-ASM");
        var line1 = await db.AssemblyLines.FirstOrDefaultAsync(a => a.AssemblyCode == "ASM-01");
        var st1 = await db.WorkStations.FirstOrDefaultAsync(w => w.StationCode == "ASM-01-ST01");

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            bool changed = false;
            if (existingUser.CompanyId == null && company != null) { existingUser.CompanyId = company.CompanyId; changed = true; }
            if (existingUser.PlantId == null && plant != null) { existingUser.PlantId = plant.PlantId; changed = true; }
            if (existingUser.DepartmentId == null && mechDept != null) { existingUser.DepartmentId = mechDept.DepartmentId; changed = true; }
            if (existingUser.CurrentAssemblyLineId == null && line1 != null) { existingUser.CurrentAssemblyLineId = line1.AssemblyLineId; changed = true; }
            if (existingUser.WorkStationId == null && st1 != null) { existingUser.WorkStationId = st1.WorkStationId; changed = true; }
            if (changed)
            {
                await userManager.UpdateAsync(existingUser);
                logger.LogInformation("Updated {Email} with worker hierarchy", email);
            }
            return;
        }

        var worker = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "John Worker (Assembly)",
            EmployeeNo = "WRK-001",
            IsActive = true,
            EmailConfirmed = true,
            RegistrationStatus = RegistrationStatus.Approved,
            RequestedRole = "Worker",
            CompanyId = company?.CompanyId,
            PlantId = plant?.PlantId,
            DepartmentId = mechDept?.DepartmentId,
            CurrentAssemblyLineId = line1?.AssemblyLineId,
            WorkStationId = st1?.WorkStationId,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(worker, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(worker, "Worker");
            logger.LogInformation("Seeded default worker: {Email}", email);
        }
        else
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            logger.LogError("Failed to seed worker: {Errors}", errors);
        }
    }

    private static async Task SeedSettingsAsync(AppDbContext db, ILogger logger)
    {
        var defaults = new Dictionary<string, (string Value, string Description)>
        {
            ["MaxRetriesDefault"] = ("3", "Default max retries per step before marking as fail"),
            ["StableHoldMs"] = ("800", "Milliseconds a reading must be stable before acceptance"),
            ["ImageMaxMb"] = ("5", "Maximum image upload size in megabytes"),
            ["DeviceOfflineSeconds"] = ("30", "Seconds without heartbeat before device is offline"),
            ["SessionIdleTimeoutMin"] = ("60", "Minutes of inactivity before session is abandoned"),
            ["BmColourMatchTolerance"] = ("40", "Max RGB distance for BM colour matching"),
            ["RequireManualEntryApproval"] = ("true", "Whether manual entry needs admin approval"),
        };

        foreach (var (key, (value, desc)) in defaults)
        {
            var exists = await db.Settings.AnyAsync(s => s.Key == key);
            if (!exists)
            {
                db.Settings.Add(new Setting
                {
                    Key = key,
                    Value = value,
                    Description = desc,
                    UpdatedAt = DateTime.UtcNow
                });
                logger.LogInformation("Seeded setting: {Key} = {Value}", key, value);
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedTemplatesAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Templates.AnyAsync()) return;

        var templates = new List<Core.Entities.Template>
        {
            new()
            {
                TemplateCode = "LK-BASE",
                Version = 1,
                IsLatest = true,
                Name = "Base Frame Assembly",
                Description = "Locker bottom frame anchor bolts",
                Mode = SmartTorque.Core.Enums.TemplateMode.Torque,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Steps =
                [
                    new TemplateStep { SequenceNo = 1, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Front-Left Anchor Bolt", Target = 25.0m, TolerancePlus = 2.0m, ToleranceMinus = 2.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 20.0m, MarkerY = 30.0m },
                    new TemplateStep { SequenceNo = 2, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Front-Right Anchor Bolt", Target = 25.0m, TolerancePlus = 2.0m, ToleranceMinus = 2.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 80.0m, MarkerY = 30.0m },
                    new TemplateStep { SequenceNo = 3, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Rear-Left Anchor Bolt", Target = 25.0m, TolerancePlus = 2.0m, ToleranceMinus = 2.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 20.0m, MarkerY = 70.0m },
                    new TemplateStep { SequenceNo = 4, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Rear-Right Anchor Bolt", Target = 25.0m, TolerancePlus = 2.0m, ToleranceMinus = 2.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 80.0m, MarkerY = 70.0m }
                ]
            },
            new()
            {
                TemplateCode = "LK-LEFT",
                Version = 1,
                IsLatest = true,
                Name = "Left Side Panel",
                Description = "Left upright structural panel with width check",
                Mode = SmartTorque.Core.Enums.TemplateMode.Mixed,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Steps =
                [
                    new TemplateStep { SequenceNo = 1, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Top-Front Flange Bolt", Target = 18.0m, TolerancePlus = 1.5m, ToleranceMinus = 1.5m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 30.0m, MarkerY = 20.0m },
                    new TemplateStep { SequenceNo = 2, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Top-Rear Flange Bolt", Target = 18.0m, TolerancePlus = 1.5m, ToleranceMinus = 1.5m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 70.0m, MarkerY = 20.0m },
                    new TemplateStep { SequenceNo = 3, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Bottom-Front Flange Bolt", Target = 18.0m, TolerancePlus = 1.5m, ToleranceMinus = 1.5m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 30.0m, MarkerY = 80.0m },
                    new TemplateStep { SequenceNo = 4, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Bottom-Rear Flange Bolt", Target = 18.0m, TolerancePlus = 1.5m, ToleranceMinus = 1.5m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 70.0m, MarkerY = 80.0m },
                    new TemplateStep { SequenceNo = 5, Instrument = SmartTorque.Core.Enums.InstrumentType.Vernier, Label = "Flange Diagonal Dimension", Target = 850.0m, TolerancePlus = 1.0m, ToleranceMinus = 1.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 50.0m, MarkerY = 50.0m }
                ]
            },
            new()
            {
                TemplateCode = "LK-RIGHT",
                Version = 1,
                IsLatest = true,
                Name = "Right Side Panel",
                Description = "Right upright structural panel with width check",
                Mode = SmartTorque.Core.Enums.TemplateMode.Mixed,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Steps =
                [
                    new TemplateStep { SequenceNo = 1, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Top-Front Flange Bolt", Target = 18.0m, TolerancePlus = 1.5m, ToleranceMinus = 1.5m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 30.0m, MarkerY = 20.0m },
                    new TemplateStep { SequenceNo = 2, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Top-Rear Flange Bolt", Target = 18.0m, TolerancePlus = 1.5m, ToleranceMinus = 1.5m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 70.0m, MarkerY = 20.0m },
                    new TemplateStep { SequenceNo = 3, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Bottom-Front Flange Bolt", Target = 18.0m, TolerancePlus = 1.5m, ToleranceMinus = 1.5m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 30.0m, MarkerY = 80.0m },
                    new TemplateStep { SequenceNo = 4, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Bottom-Rear Flange Bolt", Target = 18.0m, TolerancePlus = 1.5m, ToleranceMinus = 1.5m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 70.0m, MarkerY = 80.0m },
                    new TemplateStep { SequenceNo = 5, Instrument = SmartTorque.Core.Enums.InstrumentType.Vernier, Label = "Flange Diagonal Dimension", Target = 850.0m, TolerancePlus = 1.0m, ToleranceMinus = 1.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 50.0m, MarkerY = 50.0m }
                ]
            },
            new()
            {
                TemplateCode = "LK-BACK",
                Version = 1,
                IsLatest = true,
                Name = "Back Enclosure Panel",
                Description = "Rear sheet metal enclosure panel fasteners",
                Mode = SmartTorque.Core.Enums.TemplateMode.Torque,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Steps =
                [
                    new TemplateStep { SequenceNo = 1, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Top-Left Fastener", Target = 15.0m, TolerancePlus = 1.0m, ToleranceMinus = 1.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 15.0m, MarkerY = 15.0m },
                    new TemplateStep { SequenceNo = 2, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Top-Center Fastener", Target = 15.0m, TolerancePlus = 1.0m, ToleranceMinus = 1.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 50.0m, MarkerY = 15.0m },
                    new TemplateStep { SequenceNo = 3, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Top-Right Fastener", Target = 15.0m, TolerancePlus = 1.0m, ToleranceMinus = 1.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 85.0m, MarkerY = 15.0m },
                    new TemplateStep { SequenceNo = 4, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Bottom-Left Fastener", Target = 15.0m, TolerancePlus = 1.0m, ToleranceMinus = 1.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 15.0m, MarkerY = 85.0m },
                    new TemplateStep { SequenceNo = 5, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Bottom-Center Fastener", Target = 15.0m, TolerancePlus = 1.0m, ToleranceMinus = 1.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 50.0m, MarkerY = 85.0m },
                    new TemplateStep { SequenceNo = 6, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Bottom-Right Fastener", Target = 15.0m, TolerancePlus = 1.0m, ToleranceMinus = 1.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 85.0m, MarkerY = 85.0m }
                ]
            },
            new()
            {
                TemplateCode = "LK-TOP",
                Version = 1,
                IsLatest = true,
                Name = "Top Roof Cover",
                Description = "Top roof plate fasteners",
                Mode = SmartTorque.Core.Enums.TemplateMode.Torque,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Steps =
                [
                    new TemplateStep { SequenceNo = 1, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Corner Fastener A", Target = 20.0m, TolerancePlus = 2.0m, ToleranceMinus = 2.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 25.0m, MarkerY = 25.0m },
                    new TemplateStep { SequenceNo = 2, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Corner Fastener B", Target = 20.0m, TolerancePlus = 2.0m, ToleranceMinus = 2.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 75.0m, MarkerY = 25.0m },
                    new TemplateStep { SequenceNo = 3, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Corner Fastener C", Target = 20.0m, TolerancePlus = 2.0m, ToleranceMinus = 2.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 25.0m, MarkerY = 75.0m },
                    new TemplateStep { SequenceNo = 4, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Corner Fastener D", Target = 20.0m, TolerancePlus = 2.0m, ToleranceMinus = 2.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 75.0m, MarkerY = 75.0m }
                ]
            },
            new()
            {
                TemplateCode = "LK-DOOR",
                Version = 1,
                IsLatest = true,
                Name = "Front Door & Hinges",
                Description = "Front door hinge screws and gap clearance measurements",
                Mode = SmartTorque.Core.Enums.TemplateMode.Mixed,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Steps =
                [
                    new TemplateStep { SequenceNo = 1, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Upper Hinge Screw", Target = 12.0m, TolerancePlus = 1.0m, ToleranceMinus = 1.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 15.0m, MarkerY = 20.0m },
                    new TemplateStep { SequenceNo = 2, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Middle Hinge Screw", Target = 12.0m, TolerancePlus = 1.0m, ToleranceMinus = 1.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 15.0m, MarkerY = 50.0m },
                    new TemplateStep { SequenceNo = 3, Instrument = SmartTorque.Core.Enums.InstrumentType.Torque, Label = "Lower Hinge Screw", Target = 12.0m, TolerancePlus = 1.0m, ToleranceMinus = 1.0m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 15.0m, MarkerY = 80.0m },
                    new TemplateStep { SequenceNo = 4, Instrument = SmartTorque.Core.Enums.InstrumentType.Vernier, Label = "Top Edge Gap", Target = 3.5m, TolerancePlus = 0.5m, ToleranceMinus = 0.5m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 50.0m, MarkerY = 10.0m },
                    new TemplateStep { SequenceNo = 5, Instrument = SmartTorque.Core.Enums.InstrumentType.Vernier, Label = "Bottom Edge Gap", Target = 3.5m, TolerancePlus = 0.5m, ToleranceMinus = 0.5m, ToleranceMode = SmartTorque.Core.Enums.ToleranceMode.Absolute, MaxRetries = 3, MarkerX = 50.0m, MarkerY = 90.0m }
                ]
            }
        };

        db.Templates.AddRange(templates);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} initial templates for 6-panel locker", templates.Count);
    }

    private static async Task SeedProductsAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Products.AnyAsync()) return;

        var templates = await db.Templates
            .Where(t => t.IsLatest)
            .ToDictionaryAsync(t => t.TemplateCode, StringComparer.OrdinalIgnoreCase);

        // The reference product from the PRD: a 6-panel locker.
        var spec = new (string Code, string Name)[]
        {
            ("LK-BASE", "Base Frame"),
            ("LK-BACK", "Back Panel"),
            ("LK-LEFT", "Left Panel"),
            ("LK-RIGHT", "Right Panel"),
            ("LK-TOP", "Top Cover"),
            ("LK-DOOR", "Front Door"),
        };

        var mechDept = await db.Departments.FirstOrDefaultAsync(d => d.Code == "MECH-ASM");
        var product = new Product
        {
            ProductCode = "LK-6P",
            ProductName = "Locker 6-Panel",
            DepartmentId = mechDept?.DepartmentId,
            Description = "Six-panel factory locker assembled from the standard panel templates.",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        var order = 1;
        foreach (var (code, name) in spec)
        {
            if (templates.TryGetValue(code, out var template))
            {
                product.Components.Add(new ProductComponent
                {
                    TemplateId = template.TemplateId,
                    ComponentName = name,
                    Quantity = 1,
                    AssemblyOrder = order++
                });
            }
        }

        if (product.Components.Count == 0) return;

        db.Products.Add(product);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded product {Code} with {Count} components", product.ProductCode, product.Components.Count);
    }

    private static async Task SeedColourMapAsync(AppDbContext db, ILogger logger)
    {
        // 1. If table already seeded, ensure White is Priority 6 and Blue is added
        var existingWhite = await db.PriorityColourMap.FirstOrDefaultAsync(m => m.Label == "White" && m.Priority == (PriorityLevel)5);
        if (existingWhite != null)
        {
            existingWhite.Priority = PriorityLevel.White;
            await db.SaveChangesAsync();
        }

        if (await db.PriorityColourMap.AnyAsync() && !await db.PriorityColourMap.AnyAsync(m => m.Priority == PriorityLevel.Blue))
        {
            db.PriorityColourMap.AddRange(
                new PriorityColourMap { ColourHex = "#0070C0", Priority = PriorityLevel.Blue, Label = "Blue", MatchTolerance = 60, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "system" },
                new PriorityColourMap { ColourHex = "#4472C4", Priority = PriorityLevel.Blue, Label = "Blue (Theme)", MatchTolerance = 60, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "system" }
            );
            await db.SaveChangesAsync();
            logger.LogInformation("Added Blue to existing PriorityColourMap");
        }

        if (await db.PriorityColourMap.AnyAsync()) return;

        // Default palette from Architecture Â§5.3 (BM-3); Admin can add extra shades.
        var map = new List<PriorityColourMap>
        {
            new() { ColourHex = "#000000", Priority = PriorityLevel.Black,  Label = "Black",        MatchTolerance = 40 },
            new() { ColourHex = "#FF0000", Priority = PriorityLevel.Red,    Label = "Red",          MatchTolerance = 40 },
            new() { ColourHex = "#FFFF00", Priority = PriorityLevel.Yellow, Label = "Yellow",       MatchTolerance = 40 },
            new() { ColourHex = "#00B050", Priority = PriorityLevel.Green,  Label = "Green",        MatchTolerance = 40 },
            new() { ColourHex = "#0070C0", Priority = PriorityLevel.Blue,   Label = "Blue",         MatchTolerance = 60 },
            new() { ColourHex = "#4472C4", Priority = PriorityLevel.Blue,   Label = "Blue (Theme)", MatchTolerance = 60 },
            new() { ColourHex = "#FFFFFF", Priority = PriorityLevel.White,  Label = "White",        MatchTolerance = 40 },
        };

        db.PriorityColourMap.AddRange(map);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} priority colour map entries", map.Count);
    }

    private static async Task SeedDevicesAsync(AppDbContext db, ILogger logger)
    {
        // Purge any legacy simulated devices from the database
        var simDevices = await db.Devices
            .Where(d => d.TransmitterId.StartsWith("SIM-") || d.Name.Contains("Simulated"))
            .ToListAsync();
        if (simDevices.Count > 0)
        {
            var simIds = simDevices.Select(d => d.DeviceId).ToList();
            var simTransmitters = simDevices.Select(d => d.TransmitterId).ToList();

            var sessions = await db.Sessions
                .Where(s => (s.TorqueDeviceId.HasValue && simIds.Contains(s.TorqueDeviceId.Value)) ||
                            (s.VernierDeviceId.HasValue && simIds.Contains(s.VernierDeviceId.Value)))
                .ToListAsync();
            foreach (var s in sessions)
            {
                if (s.TorqueDeviceId.HasValue && simIds.Contains(s.TorqueDeviceId.Value)) s.TorqueDeviceId = null;
                if (s.VernierDeviceId.HasValue && simIds.Contains(s.VernierDeviceId.Value)) s.VernierDeviceId = null;
            }

            var simKeys = await db.ApiKeyDirectoryEntries
                .Where(k => simTransmitters.Contains(k.ApiScope) || k.KeyName.Contains("Simulated"))
                .ToListAsync();
            db.ApiKeyDirectoryEntries.RemoveRange(simKeys);

            db.Devices.RemoveRange(simDevices);
            await db.SaveChangesAsync();
            logger.LogInformation("Purged {Count} legacy simulated devices from database.", simDevices.Count);
        }
    }

    private static async Task SeedApiKeyDirectoryAsync(AppDbContext db, ILogger logger)
    {
        // Remove any non-device legacy seed entries so ONLY registered device API keys are in the table
        var nonDeviceEntries = await db.ApiKeyDirectoryEntries
            .Where(k => !db.Devices.Any(d => d.TransmitterId == k.ApiScope))
            .ToListAsync();

        if (nonDeviceEntries.Count > 0)
        {
            db.ApiKeyDirectoryEntries.RemoveRange(nonDeviceEntries);
            await db.SaveChangesAsync();
            logger.LogInformation("Purged {Count} non-device API key entries", nonDeviceEntries.Count);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedHierarchyAsync(AppDbContext db, ILogger logger)
    {
        // If modern flexible HierarchyNodes already exist, do not seed old legacy sample hierarchy
        if (await db.HierarchyNodes.AnyAsync()) return;

        // 1. Company
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Code == "GODREJ");
        if (company == null)
        {
            company = new Company
            {
                Name = "Godrej Enterprises",
                Code = "GODREJ",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded Company: {Name}", company.Name);
        }

        // 2. Plant
        var plant = await db.Plants.FirstOrDefaultAsync(p => p.CompanyId == company.CompanyId && p.Code == "PLANT-01");
        if (plant == null)
        {
            plant = new Plant
            {
                CompanyId = company.CompanyId,
                Name = "Plant 1 - Vikhroli",
                Code = "PLANT-01",
                Location = "Vikhroli, Mumbai",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Plants.Add(plant);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded Plant: {Name}", plant.Name);
        }

        // 3. Departments
        var depts = new (string Code, string Name)[]
        {
            ("MECH-ASM", "Mechanical Assembly"),
            ("FAB", "Fabrication"),
            ("ELEC", "Electrical")
        };

        foreach (var (code, name) in depts)
        {
            var dept = await db.Departments.FirstOrDefaultAsync(d => d.PlantId == plant.PlantId && d.Code == code);
            if (dept == null)
            {
                dept = new Department
                {
                    PlantId = plant.PlantId,
                    Code = code,
                    Name = name,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.Departments.Add(dept);
                await db.SaveChangesAsync();
                logger.LogInformation("Seeded Department: {Name}", name);
            }
        }

        var mechDept = await db.Departments.FirstAsync(d => d.PlantId == plant.PlantId && d.Code == "MECH-ASM");
        var fabDept = await db.Departments.FirstAsync(d => d.PlantId == plant.PlantId && d.Code == "FAB");

        // 4. Production Units (Lines and Cells)
        // Mechanical Assembly Line 1
        var line1 = await db.AssemblyLines.FirstOrDefaultAsync(a => a.AssemblyCode == "ASM-01");
        if (line1 == null)
        {
            line1 = new AssemblyLine
            {
                AssemblyCode = "ASM-01",
                AssemblyName = "Chassis Final Assembly Line",
                Department = mechDept.Name,
                DepartmentId = mechDept.DepartmentId,
                UnitType = ProductionUnitType.Line,
                Description = "Primary manual assembly line for structural lockers and cabinetry",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };
            db.AssemblyLines.Add(line1);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded Assembly Line: {Code}", line1.AssemblyCode);
        }
        else
        {
            if (line1.DepartmentId == null)
            {
                line1.DepartmentId = mechDept.DepartmentId;
                line1.Department = mechDept.Name;
                line1.UnitType = ProductionUnitType.Line;
                await db.SaveChangesAsync();
            }
        }

        // Mechanical Assembly Cell 1
        var cell1 = await db.AssemblyLines.FirstOrDefaultAsync(a => a.AssemblyCode == "CELL-01");
        if (cell1 == null)
        {
            cell1 = new AssemblyLine
            {
                AssemblyCode = "CELL-01",
                AssemblyName = "Modular Assembly Cell 1",
                Department = mechDept.Name,
                DepartmentId = mechDept.DepartmentId,
                UnitType = ProductionUnitType.Cell,
                Description = "Flexible assembly cell for sub-assemblies and custom lockers",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };
            db.AssemblyLines.Add(cell1);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded Assembly Cell: {Code}", cell1.AssemblyCode);
        }

        // Fabrication Line 1
        var fabLine = await db.AssemblyLines.FirstOrDefaultAsync(a => a.AssemblyCode == "FAB-01");
        if (fabLine == null)
        {
            fabLine = new AssemblyLine
            {
                AssemblyCode = "FAB-01",
                AssemblyName = "Sheet Metal Fabrication Line",
                Department = fabDept.Name,
                DepartmentId = fabDept.DepartmentId,
                UnitType = ProductionUnitType.Line,
                Description = "High precision sheet metal punching, forming, and bending line",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };
            db.AssemblyLines.Add(fabLine);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded Fabrication Line: {Code}", fabLine.AssemblyCode);
        }

        // 5. WorkStations (Stations for Lines, Subcells for Cells)
        var stations = new (string Code, string Name, StationType Type, int LineId)[]
        {
            ("ASM-01-ST01", "Base & Chassis Station", StationType.Station, line1.AssemblyLineId),
            ("ASM-01-ST02", "Fastening & Torque Station", StationType.Station, line1.AssemblyLineId),
            ("CELL-01-SUB01", "Frame Pre-assembly Subcell", StationType.Subcell, cell1.AssemblyLineId),
            ("CELL-01-SUB02", "Door & Panel Subcell", StationType.Subcell, cell1.AssemblyLineId),
            ("FAB-01-ST01", "Cutting & Bending Station", StationType.Station, fabLine.AssemblyLineId)
        };

        foreach (var (code, name, type, lineId) in stations)
        {
            var ws = await db.WorkStations.FirstOrDefaultAsync(w => w.AssemblyLineId == lineId && w.StationCode == code);
            if (ws == null)
            {
                ws = new WorkStation
                {
                    AssemblyLineId = lineId,
                    StationCode = code,
                    StationName = name,
                    StationType = type,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.WorkStations.Add(ws);
                await db.SaveChangesAsync();
                logger.LogInformation("Seeded WorkStation: {Code} ({Type})", code, type);
            }
        }

        // 6. Associate any existing unassociated products with Mechanical Assembly
        var unassignedProducts = await db.Products.Where(p => p.DepartmentId == null).ToListAsync();
        foreach (var p in unassignedProducts)
        {
            p.DepartmentId = mechDept.DepartmentId;
        }
        if (unassignedProducts.Count > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Associated {Count} legacy products with Department: {Dept}", unassignedProducts.Count, mechDept.Name);
        }
    }


    private static async Task SeedHierarchyGraphNodesAsync(AppDbContext db, ILogger logger)
    {
        if (await db.HierarchyNodes.AnyAsync()) return;

        // 1. Enterprise Root
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Code == "GODREJ");
        if (company == null) return;

        var enterpriseNode = new HierarchyNode
        {
            Name = company.Name,
            Code = company.Code,
            NodeType = HierarchyNodeType.Enterprise,
            Depth = 0,
            Path = string.Empty,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.HierarchyNodes.Add(enterpriseNode);
        await db.SaveChangesAsync();
        enterpriseNode.Path = "/" + enterpriseNode.NodeId + "/";
        await db.SaveChangesAsync();

        // 2. Plant Node
        var plants = await db.Plants.Where(p => p.CompanyId == company.CompanyId).ToListAsync();
        var plantNodeMap = new Dictionary<int, HierarchyNode>();
        foreach (var p in plants)
        {
            var pNode = new HierarchyNode
            {
                Name = p.Name,
                Code = p.Code,
                NodeType = HierarchyNodeType.Plant,
                ParentNodeId = enterpriseNode.NodeId,
                Depth = 1,
                Path = string.Empty,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.HierarchyNodes.Add(pNode);
            await db.SaveChangesAsync();
            pNode.Path = enterpriseNode.Path + pNode.NodeId + "/";
            await db.SaveChangesAsync();
            plantNodeMap[p.PlantId] = pNode;
        }

        // 3. Department Nodes
        var depts = await db.Departments.ToListAsync();
        var deptNodeMap = new Dictionary<int, HierarchyNode>();
        foreach (var d in depts)
        {
            if (plantNodeMap.TryGetValue(d.PlantId, out var parentPlantNode))
            {
                var dNode = new HierarchyNode
                {
                    Name = d.Name,
                    Code = d.Code,
                    NodeType = HierarchyNodeType.Department,
                    ParentNodeId = parentPlantNode.NodeId,
                    Depth = parentPlantNode.Depth + 1,
                    Path = string.Empty,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.HierarchyNodes.Add(dNode);
                await db.SaveChangesAsync();
                dNode.Path = parentPlantNode.Path + dNode.NodeId + "/";
                await db.SaveChangesAsync();
                deptNodeMap[d.DepartmentId] = dNode;
            }
        }

        // 4. Lines
        var lines = await db.AssemblyLines.ToListAsync();
        var lineNodeMap = new Dictionary<int, HierarchyNode>();
        foreach (var l in lines)
        {
            if (l.DepartmentId.HasValue && deptNodeMap.TryGetValue(l.DepartmentId.Value, out var parentDeptNode))
            {
                var lNode = new HierarchyNode
                {
                    Name = l.AssemblyName,
                    Code = l.AssemblyCode,
                    NodeType = l.UnitType == ProductionUnitType.Cell ? HierarchyNodeType.Cell : HierarchyNodeType.AssemblyLine,
                    ParentNodeId = parentDeptNode.NodeId,
                    Depth = parentDeptNode.Depth + 1,
                    Path = string.Empty,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.HierarchyNodes.Add(lNode);
                await db.SaveChangesAsync();
                lNode.Path = parentDeptNode.Path + lNode.NodeId + "/";
                await db.SaveChangesAsync();
                lineNodeMap[l.AssemblyLineId] = lNode;
            }
        }

        // 5. Stations
        var stations = await db.WorkStations.ToListAsync();
        foreach (var s in stations)
        {
            if (lineNodeMap.TryGetValue(s.AssemblyLineId, out var parentLineNode))
            {
                var sNode = new HierarchyNode
                {
                    Name = s.StationName,
                    Code = s.StationCode,
                    NodeType = s.StationType == StationType.Subcell ? HierarchyNodeType.Subcell : HierarchyNodeType.WorkStation,
                    ParentNodeId = parentLineNode.NodeId,
                    Depth = parentLineNode.Depth + 1,
                    Path = string.Empty,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.HierarchyNodes.Add(sNode);
                await db.SaveChangesAsync();
                sNode.Path = parentLineNode.Path + sNode.NodeId + "/";
                await db.SaveChangesAsync();
            }
        }

        logger.LogInformation("Successfully seeded initial flexible hierarchy graph.");
    }

    private static async Task SeedSupervisorAsync(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        ILogger logger)
    {
        const string email = "supervisor@smarttorque.com";
        const string password = "Supervisor@12345";

        var company = await db.Companies.FirstOrDefaultAsync(co => co.Code == "GODREJ");
        var plant = await db.Plants.FirstOrDefaultAsync(p => p.Code == "PLANT-01");
        var mechDept = await db.Departments.FirstOrDefaultAsync(d => d.Code == "MECH-ASM");
        var deptNode = await db.HierarchyNodes.FirstOrDefaultAsync(n => n.Code == "MECH-ASM");

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            existingUser.IsLineSupervisor = true;
            existingUser.RequestedRole = "Worker";
            if (await userManager.IsInRoleAsync(existingUser, "Supervisor"))
            {
                await userManager.RemoveFromRoleAsync(existingUser, "Supervisor");
            }
            if (!await userManager.IsInRoleAsync(existingUser, "Worker"))
            {
                await userManager.AddToRoleAsync(existingUser, "Worker");
            }
            if (deptNode != null && existingUser.HierarchyNodeId == null)
            {
                existingUser.HierarchyNodeId = deptNode.NodeId;
            }
            await userManager.UpdateAsync(existingUser);
            return;
        }

        var supervisor = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Rajesh Supervisor (Assembly)",
            EmployeeNo = "SUP-001",
            IsActive = true,
            IsLineSupervisor = true,
            EmailConfirmed = true,
            RegistrationStatus = RegistrationStatus.Approved,
            RequestedRole = "Worker",
            CompanyId = company?.CompanyId,
            PlantId = plant?.PlantId,
            DepartmentId = mechDept?.DepartmentId,
            HierarchyNodeId = deptNode?.NodeId,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(supervisor, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(supervisor, "Worker");
            logger.LogInformation("Seeded default supervisor worker: {Email}", email);
        }
        else
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            logger.LogError("Failed to seed supervisor: {Errors}", errors);
        }
    }
}