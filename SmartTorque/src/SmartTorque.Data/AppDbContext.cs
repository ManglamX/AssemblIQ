using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;

using SmartTorque.Core.Enums;

namespace SmartTorque.Data;

/// <summary>

/// Main database context. Extends IdentityDbContext for ASP.NET Core Identity tables.

/// </summary>

public class AppDbContext : IdentityDbContext<ApplicationUser>

{

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)

    {

    }

    public DbSet<Setting> Settings => Set<Setting>();

    public DbSet<Core.Entities.Template> Templates => Set<Core.Entities.Template>();

    public DbSet<Core.Entities.TemplateStep> TemplateSteps => Set<Core.Entities.TemplateStep>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductComponent> ProductComponents => Set<ProductComponent>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<BmUpload> BmUploads => Set<BmUpload>();

    public DbSet<BmRow> BmRows => Set<BmRow>();

    public DbSet<PriorityColourMap> PriorityColourMap => Set<PriorityColourMap>();

    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<Core.Entities.SessionResult> SessionResults => Set<Core.Entities.SessionResult>();

    public DbSet<JobUnit> JobUnits => Set<JobUnit>();

    public DbSet<JobUnitRework> JobUnitReworks => Set<JobUnitRework>();

    public DbSet<ApiKeyDirectoryEntry> ApiKeyDirectoryEntries => Set<ApiKeyDirectoryEntry>();

    public DbSet<ApiKeyAccessGrant> ApiKeyAccessGrants => Set<ApiKeyAccessGrant>();

    public DbSet<AssemblyLine> AssemblyLines => Set<AssemblyLine>();

    public DbSet<ProductAssemblyMapping> ProductAssemblyMappings => Set<ProductAssemblyMapping>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<Plant> Plants => Set<Plant>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<WorkStation> WorkStations => Set<WorkStation>();

    public DbSet<SessionActivityWindow> SessionActivityWindows => Set<SessionActivityWindow>();

    public DbSet<HierarchyNode> HierarchyNodes => Set<HierarchyNode>();

    public DbSet<HierarchyNodeRequest> HierarchyNodeRequests => Set<HierarchyNodeRequest>();

    protected override void OnModelCreating(ModelBuilder builder)

    {

        base.OnModelCreating(builder);

        // ApplicationUser configuration

        builder.Entity<ApplicationUser>(entity =>

        {

            entity.Property(u => u.FullName).HasMaxLength(200).IsRequired();

            entity.Property(u => u.EmployeeNo).HasMaxLength(50);

            entity.Property(u => u.IsActive).HasDefaultValue(true);

            entity.Property(u => u.IsMainAdmin).HasDefaultValue(false);

            entity.Property(u => u.RegistrationStatus).HasColumnType("tinyint").HasDefaultValue(RegistrationStatus.Approved).HasSentinel((RegistrationStatus)0);

            entity.Property(u => u.RequestedRole).HasMaxLength(50);

            entity.Property(u => u.RejectionReason).HasMaxLength(500);

            entity.Property(u => u.ReviewedBy).HasMaxLength(256);

            entity.Property(u => u.ReviewedAt).HasColumnType("datetime2");

            entity.Property(u => u.CreatedAt).HasColumnType("datetime2");

            entity.Property(u => u.UpdatedAt).HasColumnType("datetime2");

            entity.Property(u => u.IsPlantHead).HasDefaultValue(false);

            entity.Property(u => u.IsLineSupervisor).HasDefaultValue(false);

            entity.HasOne(u => u.Company)

                  .WithMany(c => c.Users)

                  .HasForeignKey(u => u.CompanyId)

                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(u => u.Plant)

                  .WithMany(p => p.Users)

                  .HasForeignKey(u => u.PlantId)

                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(u => u.Department)

                  .WithMany(d => d.Users)

                  .HasForeignKey(u => u.DepartmentId)

                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(u => u.CurrentAssemblyLine)

                  .WithMany(a => a.AssignedWorkers)

                  .HasForeignKey(u => u.CurrentAssemblyLineId)

                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(u => u.WorkStation)

                  .WithMany(w => w.AssignedWorkers)

                  .HasForeignKey(u => u.WorkStationId)

                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(u => u.HierarchyNode)

                  .WithMany(n => n.Users)

                  .HasForeignKey(u => u.HierarchyNodeId)

                  .OnDelete(DeleteBehavior.SetNull);

        });

        // Setting configuration

        builder.Entity<Setting>(entity =>

        {

            entity.HasKey(s => s.SettingId);

            entity.Property(s => s.Key).HasMaxLength(100).IsRequired();

            entity.HasIndex(s => s.Key).IsUnique();

            entity.Property(s => s.Value).HasMaxLength(500).IsRequired();

            entity.Property(s => s.Description).HasMaxLength(500);

            entity.Property(s => s.UpdatedAt).HasColumnType("datetime2");

            entity.Property(s => s.UpdatedBy).HasMaxLength(450);

        });

        // Template configuration

        builder.Entity<Core.Entities.Template>(entity =>

        {

            entity.HasKey(t => t.TemplateId);

            entity.Property(t => t.TemplateCode).HasMaxLength(50).IsRequired();

            entity.Property(t => t.Name).HasMaxLength(200).IsRequired();

            entity.Property(t => t.Description).HasMaxLength(1000);

            entity.Property(t => t.Mode).HasColumnType("tinyint");

            entity.Property(t => t.ImagePath).HasMaxLength(500);

            entity.Property(t => t.CreatedAt).HasColumnType("datetime2");

            entity.Property(t => t.UpdatedAt).HasColumnType("datetime2");

            entity.Property(t => t.CreatedBy).HasMaxLength(450);

            entity.Property(t => t.UpdatedBy).HasMaxLength(450);

            // Unique (TemplateCode, Version)

            entity.HasIndex(t => new { t.TemplateCode, t.Version }).IsUnique();

            entity.HasIndex(t => t.TemplateCode);

            entity.HasIndex(t => t.IsLatest);

        });

        // TemplateStep configuration

        builder.Entity<Core.Entities.TemplateStep>(entity =>

        {

            entity.HasKey(s => s.TemplateStepId);

            entity.Property(s => s.Instrument).HasColumnType("tinyint");

            entity.Property(s => s.Label).HasMaxLength(200).IsRequired();

            entity.Property(s => s.Target).HasColumnType("decimal(18,4)");

            entity.Property(s => s.TolerancePlus).HasColumnType("decimal(18,4)");

            entity.Property(s => s.ToleranceMinus).HasColumnType("decimal(18,4)");

            entity.Property(s => s.ToleranceMode).HasColumnType("tinyint");

            entity.Property(s => s.MarkerX).HasColumnType("decimal(6,3)");

            entity.Property(s => s.MarkerY).HasColumnType("decimal(6,3)");

            entity.Property(s => s.MarkerSize);

            // SequenceNo unique within a template

            entity.HasIndex(s => new { s.TemplateId, s.SequenceNo }).IsUnique();

            entity.HasOne(s => s.Template)

                  .WithMany(t => t.Steps)

                  .HasForeignKey(s => s.TemplateId)

                  .OnDelete(DeleteBehavior.Cascade);

        });

        // AuditLog configuration

        builder.Entity<AuditLog>(entity =>

        {

            entity.HasKey(a => a.AuditLogId);

            entity.Property(a => a.OccurredAt).HasColumnType("datetime2");

            entity.Property(a => a.UserId).HasMaxLength(450);

            entity.Property(a => a.UserName).HasMaxLength(256);

            entity.Property(a => a.EntityName).HasMaxLength(100).IsRequired();

            entity.Property(a => a.EntityId).HasMaxLength(100);

            entity.Property(a => a.Action).HasMaxLength(100).IsRequired();

            // Changes is NVARCHAR(MAX) — no length constraint

            entity.HasIndex(a => a.OccurredAt);

            entity.HasIndex(a => new { a.EntityName, a.EntityId });

        });

        // Product configuration

        builder.Entity<Product>(entity =>

        {

            entity.HasKey(p => p.ProductId);

            entity.Property(p => p.ProductCode).HasMaxLength(50).IsRequired();

            entity.Property(p => p.ProductName).HasMaxLength(150).IsRequired();

            entity.Property(p => p.Description).HasMaxLength(500);

            entity.Property(p => p.ImagePath).HasMaxLength(300);

            entity.Property(p => p.CreatedAt).HasColumnType("datetime2");

            entity.Property(p => p.UpdatedAt).HasColumnType("datetime2");

            entity.Property(p => p.CreatedBy).HasMaxLength(450);

            entity.Property(p => p.UpdatedBy).HasMaxLength(450);

            // Product codes are the join key between BM sheets and the catalogue —

            // ConfirmAsync keys ToDictionaryAsync on it, and the AddProducts

            // migration created IX_Products_ProductCode as unique (K-13).

            entity.HasIndex(p => p.ProductCode).IsUnique();

            entity.HasOne(p => p.Department)

                  .WithMany(d => d.Products)

                  .HasForeignKey(p => p.DepartmentId)

                  .OnDelete(DeleteBehavior.Restrict);

        });

        // ProductComponent configuration

        builder.Entity<ProductComponent>(entity =>

        {

            entity.HasKey(c => c.ProductComponentId);

            entity.Property(c => c.ComponentName).HasMaxLength(100).IsRequired();

            // Lookup by product in assembly order

            entity.HasIndex(c => new { c.ProductId, c.AssemblyOrder });

            entity.HasOne(c => c.Product)

                  .WithMany(p => p.Components)

                  .HasForeignKey(c => c.ProductId)

                  .OnDelete(DeleteBehavior.Cascade);

            // Templates are never hard-deleted (soft delete only), so restrict.

            entity.HasOne(c => c.Template)

                  .WithMany()

                  .HasForeignKey(c => c.TemplateId)

                  .OnDelete(DeleteBehavior.Restrict);

        });

        // ProductImage configuration (gallery; row 0 mirrors Products.ImagePath)

        builder.Entity<ProductImage>(entity =>

        {

            entity.HasKey(i => i.ProductImageId);

            entity.Property(i => i.ImagePath).HasMaxLength(300).IsRequired();

            entity.Property(i => i.CreatedAt).HasColumnType("datetime2");

            // Lookup by product in display order

            entity.HasIndex(i => new { i.ProductId, i.SortOrder });

            entity.HasOne(i => i.Product)

                  .WithMany(p => p.Images)

                  .HasForeignKey(i => i.ProductId)

                  .OnDelete(DeleteBehavior.Cascade);

        });

        // PriorityColourMap configuration (config table: audit columns, soft delete)

        builder.Entity<PriorityColourMap>(entity =>

        {

            entity.HasKey(m => m.MapId);

            entity.Property(m => m.ColourHex).HasMaxLength(9).IsRequired();

            entity.Property(m => m.Priority).HasColumnType("tinyint");

            entity.Property(m => m.Label).HasMaxLength(30).IsRequired();

            entity.Property(m => m.MatchTolerance).HasDefaultValue(40);

            entity.Property(m => m.IsActive).HasDefaultValue(true);

            entity.Property(m => m.CreatedAt).HasColumnType("datetime2");

            entity.Property(m => m.UpdatedAt).HasColumnType("datetime2");

            entity.Property(m => m.CreatedBy).HasMaxLength(450);

            entity.Property(m => m.UpdatedBy).HasMaxLength(450);

            entity.HasIndex(m => m.Priority);

        });

        // BmUpload configuration

        builder.Entity<BmUpload>(entity =>

        {

            entity.HasKey(u => u.UploadId);

            entity.Property(u => u.FileName).HasMaxLength(260).IsRequired();

            entity.Property(u => u.StoredPath).HasMaxLength(300).IsRequired();

            entity.Property(u => u.UploadedBy).HasMaxLength(450);

            entity.Property(u => u.UploadedAt).HasColumnType("datetime2");

            entity.Property(u => u.Status).HasColumnType("tinyint");

            entity.HasIndex(u => u.UploadedAt);

        });

        // BmRow configuration

        builder.Entity<BmRow>(entity =>

        {

            entity.HasKey(r => r.RowId);

            entity.Property(r => r.ExcelRowNo).IsRequired();

            entity.Property(r => r.ProductCode).HasMaxLength(50).IsRequired();

            entity.Property(r => r.Quantity).HasDefaultValue(1);

            entity.Property(r => r.DueDate).HasColumnType("date");

            entity.Property(r => r.ColourHex).HasMaxLength(9);

            entity.Property(r => r.Priority).HasColumnType("tinyint");

            entity.Property(r => r.Remarks).HasMaxLength(500);

            entity.Property(r => r.SalesOrder).HasMaxLength(100);

            entity.Property(r => r.PONo).HasMaxLength(100);

            entity.Property(r => r.ImportStatus).HasColumnType("tinyint");

            entity.Property(r => r.ImportMessage).HasMaxLength(300);

            entity.HasIndex(r => r.UploadId);

            entity.HasIndex(r => new { r.ProductCode, r.DueDate });

            entity.HasIndex(r => new { r.SalesOrder, r.PONo });

            entity.HasOne(r => r.Upload)

                  .WithMany(u => u.Rows)

                  .HasForeignKey(r => r.UploadId)

                  .OnDelete(DeleteBehavior.Cascade);

        });

        // Job configuration

        builder.Entity<Job>(entity =>

        {

            entity.HasKey(j => j.JobId);

            entity.Property(j => j.JobNo).HasMaxLength(30).IsRequired();

            entity.HasIndex(j => j.JobNo).IsUnique();

            entity.Property(j => j.ProductSerial).HasMaxLength(60);

            entity.Property(j => j.SalesOrder).HasMaxLength(100);

            entity.Property(j => j.PONo).HasMaxLength(100);

            entity.Property(j => j.Quantity).HasDefaultValue(1);

            entity.Property(j => j.QuantityCompleted).HasDefaultValue(0);

            entity.Property(j => j.Priority).HasColumnType("tinyint");

            entity.Property(j => j.DueDate).HasColumnType("date");

            entity.Property(j => j.AssignmentMode).HasColumnType("tinyint");

            entity.Property(j => j.AssignedWorkerId).HasMaxLength(450);

            entity.Property(j => j.AssignedAt).HasColumnType("datetime2");

            entity.Property(j => j.EstimatedWorkload).HasColumnType("decimal(18,2)").HasDefaultValue(1.0m);

            entity.Property(j => j.Status).HasColumnType("tinyint");

            entity.Property(j => j.ExecutionMode).HasColumnType("tinyint");

            entity.Property(j => j.CreatedAt).HasColumnType("datetime2");

            entity.Property(j => j.UpdatedAt).HasColumnType("datetime2");

            entity.Property(j => j.RowVersion).IsRowVersion();

            // Board ordering and "my jobs"

            entity.HasIndex(j => new { j.Status, j.Priority, j.DueDate });

            entity.HasIndex(j => new { j.AssignedWorkerId, j.Status });

            entity.HasIndex(j => new { j.AssemblyLineId, j.Status });

            entity.HasIndex(j => j.ProductSerial);

            entity.HasIndex(j => new { j.SalesOrder, j.PONo });

            // A product is never deleted when jobs exist

            entity.HasOne(j => j.Product)

                  .WithMany()

                  .HasForeignKey(j => j.ProductId)

                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(j => j.AssignedWorker)

                  .WithMany()

                  .HasForeignKey(j => j.AssignedWorkerId)

                  .OnDelete(DeleteBehavior.SetNull);

            entity.Property(j => j.AssignedSupervisorId).HasMaxLength(450);
            entity.HasOne(j => j.ParentJob)
                  .WithMany(j => j.ChildPackages)
                  .HasForeignKey(j => j.ParentJobId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.Property(j => j.ScopeType)
                  .HasColumnType("tinyint")
                  .HasDefaultValue(AllocationScopeType.FullProduct)
                  .HasSentinel((AllocationScopeType)0);

            entity.Property(j => j.AllowedTemplateIds).HasMaxLength(250);
            entity.Property(j => j.AllowedStepIds).HasMaxLength(500);
            entity.Property(j => j.ScopeSummary).HasMaxLength(300);

            entity.HasOne(j => j.AssignedSupervisor)

                  .WithMany()

                  .HasForeignKey(j => j.AssignedSupervisorId)

                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(j => j.AssemblyLine)

                  .WithMany(a => a.Jobs)

                  .HasForeignKey(j => j.AssemblyLineId)

                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(j => j.SourceBmRow)

                  .WithMany()

                  .HasForeignKey(j => j.SourceBmRowId)

                  .OnDelete(DeleteBehavior.SetNull);

        });

        // AssemblyLine configuration

        builder.Entity<AssemblyLine>(entity =>

        {

            entity.HasKey(a => a.AssemblyLineId);

            entity.Property(a => a.AssemblyCode).HasMaxLength(50).IsRequired();

            entity.HasIndex(a => a.AssemblyCode).IsUnique();

            entity.Property(a => a.AssemblyName).HasMaxLength(150).IsRequired();

            entity.Property(a => a.Department).HasMaxLength(100).IsRequired();

            entity.Property(a => a.Description).HasMaxLength(500);

            entity.Property(a => a.IsActive).HasDefaultValue(true);

            entity.Property(a => a.CreatedAt).HasColumnType("datetime2");

            entity.Property(a => a.UpdatedAt).HasColumnType("datetime2");

            entity.Property(a => a.CreatedBy).HasMaxLength(450);

            entity.Property(a => a.UpdatedBy).HasMaxLength(450);

            entity.Property(a => a.UnitType).HasColumnType("tinyint").HasDefaultValue(ProductionUnitType.Line).HasSentinel((ProductionUnitType)0);

            entity.HasIndex(a => a.IsActive);

            entity.HasOne(a => a.DepartmentNavigation)

                  .WithMany(d => d.ProductionUnits)

                  .HasForeignKey(a => a.DepartmentId)

                  .OnDelete(DeleteBehavior.Restrict);

        });

        // ProductAssemblyMapping configuration

        builder.Entity<ProductAssemblyMapping>(entity =>

        {

            entity.HasKey(m => m.ProductAssemblyMappingId);

            entity.Property(m => m.IsDefault).HasDefaultValue(true);

            entity.Property(m => m.ScopeType).HasColumnType("tinyint").HasDefaultValue(AllocationScopeType.FullProduct).HasSentinel((AllocationScopeType)0);

            entity.Property(m => m.AllowedTemplateIds).HasMaxLength(500);

            entity.Property(m => m.AllowedStepIds).HasMaxLength(1000);

            entity.Property(m => m.ScopeSummary).HasMaxLength(500);

            entity.Property(m => m.CreatedAt).HasColumnType("datetime2");

            entity.Property(m => m.UpdatedAt).HasColumnType("datetime2");

            entity.Property(m => m.CreatedBy).HasMaxLength(450);

            entity.Property(m => m.UpdatedBy).HasMaxLength(450);

            entity.HasIndex(m => new { m.ProductId, m.AssemblyLineId, m.WorkStationId });

            entity.HasIndex(m => new { m.AssemblyLineId, m.WorkStationId });

            entity.HasOne(m => m.Product)
                  .WithMany(p => p.AssemblyMappings)
                  .HasForeignKey(m => m.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.AssemblyLine)
                  .WithMany(a => a.ProductMappings)
                  .HasForeignKey(m => m.AssemblyLineId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.WorkStation)
                  .WithMany()
                  .HasForeignKey(m => m.WorkStationId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // JobUnit configuration (PRD §5.4: individual 40-character tracking tokens per product unit)

        builder.Entity<JobUnit>(entity =>

        {

            entity.HasKey(u => u.JobUnitId);

            entity.Property(u => u.TokenNo).HasMaxLength(40).IsFixedLength().IsRequired();

            entity.HasIndex(u => u.TokenNo).IsUnique();

            entity.Property(u => u.ProductSerial).HasMaxLength(60);

            entity.Property(u => u.Status).HasColumnType("tinyint");

            entity.Property(u => u.StartedAt).HasColumnType("datetime2");

            entity.Property(u => u.CompletedAt).HasColumnType("datetime2");

            entity.Property(u => u.CreatedAt).HasColumnType("datetime2");

            entity.Property(u => u.CompletedByWorkerId).HasMaxLength(450);

            entity.Property(u => u.IntegrityHash).HasMaxLength(64);

            entity.HasIndex(u => new { u.JobId, u.UnitIndex }).IsUnique();

            entity.HasOne(u => u.Job)

                  .WithMany(j => j.Units)

                  .HasForeignKey(u => u.JobId)

                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(u => u.CompletedByWorker)

                  .WithMany()

                  .HasForeignKey(u => u.CompletedByWorkerId)

                  .OnDelete(DeleteBehavior.SetNull);

        });

        // JobUnitRework configuration (multi-cycle rework tracking and audit logs)
        builder.Entity<JobUnitRework>(entity =>
        {
            entity.HasKey(r => r.JobUnitReworkId);
            entity.Property(r => r.TokenNo).HasMaxLength(40).IsFixedLength().IsRequired();
            entity.Property(r => r.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(r => r.DefectCategory).HasMaxLength(100);
            entity.Property(r => r.TargetTemplateIds).HasMaxLength(250);
            entity.Property(r => r.InitiatedByUserId).HasMaxLength(450);
            entity.Property(r => r.InitiatedByUserName).HasMaxLength(200);
            entity.Property(r => r.CompletedByUserId).HasMaxLength(450);
            entity.Property(r => r.CompletedByUserName).HasMaxLength(200);
            entity.Property(r => r.ResolutionNotes).HasMaxLength(1000);
            entity.Property(r => r.Status).HasColumnType("tinyint");
            entity.Property(r => r.InitiatedAt).HasColumnType("datetime2");
            entity.Property(r => r.CompletedAt).HasColumnType("datetime2");

            entity.HasIndex(r => r.TokenNo);
            entity.HasIndex(r => new { r.JobUnitId, r.ReworkIteration });

            entity.HasOne(r => r.JobUnit)
                  .WithMany(u => u.ReworkLogs)
                  .HasForeignKey(r => r.JobUnitId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Job)
                  .WithMany()
                  .HasForeignKey(r => r.JobId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ApiKeyDirectoryEntry configuration (Central API directory visible to Main Admin)

        builder.Entity<ApiKeyDirectoryEntry>(entity =>

        {

            entity.HasKey(k => k.ApiKeyId);

            entity.Property(k => k.KeyName).HasMaxLength(100).IsRequired();

            entity.Property(k => k.ApiScope).HasMaxLength(100).IsRequired();

            entity.Property(k => k.Description).HasMaxLength(500);

            entity.Property(k => k.RawApiKey).HasMaxLength(256).IsRequired();

            entity.Property(k => k.ApiKeyHash).HasMaxLength(128).IsRequired();

            entity.Property(k => k.IsActive).HasDefaultValue(true);

            entity.Property(k => k.CreatedAt).HasColumnType("datetime2");

            entity.Property(k => k.CreatedBy).HasMaxLength(450).IsRequired();

            entity.HasIndex(k => k.KeyName);

            entity.HasIndex(k => k.ApiKeyHash);

        });

        // ApiKeyAccessGrant configuration (Admin persistent access requests & approvals)

        builder.Entity<ApiKeyAccessGrant>(entity =>

        {

            entity.HasKey(g => g.GrantId);

            entity.Property(g => g.UserId).HasMaxLength(450).IsRequired();

            entity.Property(g => g.Status).HasColumnType("tinyint");

            entity.Property(g => g.RequestReason).HasMaxLength(500);

            entity.Property(g => g.RequestedAt).HasColumnType("datetime2");

            entity.Property(g => g.DecidedAt).HasColumnType("datetime2");

            entity.Property(g => g.DecidedByUserId).HasMaxLength(450);

            entity.Property(g => g.RejectionReason).HasMaxLength(500);

            entity.HasIndex(g => new { g.ApiKeyId, g.UserId });

            entity.HasOne(g => g.ApiKey)

                  .WithMany(k => k.Grants)

                  .HasForeignKey(g => g.ApiKeyId)

                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(g => g.User)

                  .WithMany()

                  .HasForeignKey(g => g.UserId)

                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(g => g.DecidedByUser)

                  .WithMany()

                  .HasForeignKey(g => g.DecidedByUserId)

                  .OnDelete(DeleteBehavior.Restrict);

        });

        // Notification configuration

        builder.Entity<Notification>(entity =>

        {

            entity.HasKey(n => n.NotificationId);

            entity.Property(n => n.Type).HasColumnType("tinyint");

            entity.Property(n => n.Message).HasMaxLength(500).IsRequired();

            entity.Property(n => n.TargetRole).HasMaxLength(30).IsRequired();

            entity.Property(n => n.CreatedAt).HasColumnType("datetime2");

            entity.Property(n => n.ReadAt).HasColumnType("datetime2");

            entity.HasIndex(n => n.CreatedAt);

            entity.HasOne(n => n.Job)

                  .WithMany()

                  .HasForeignKey(n => n.JobId)

                  .OnDelete(DeleteBehavior.SetNull);

        });

        // Device configuration

        builder.Entity<Device>(entity =>

        {

            entity.HasKey(d => d.DeviceId);

            entity.Property(d => d.TransmitterId).HasMaxLength(50).IsRequired();

            entity.HasIndex(d => d.TransmitterId).IsUnique();

            entity.Property(d => d.Name).HasMaxLength(100).IsRequired();

            entity.Property(d => d.InstrumentType).HasColumnType("tinyint");

            entity.Property(d => d.Location).HasMaxLength(100);

            entity.Property(d => d.FirmwareVersion).HasMaxLength(30);

            entity.Property(d => d.ApiKeyHash).HasMaxLength(200).IsRequired();

            entity.Property(d => d.CalibrationDue).HasColumnType("date");

            entity.Property(d => d.CreatedAt).HasColumnType("datetime2");

            entity.Property(d => d.UpdatedAt).HasColumnType("datetime2");

            entity.Property(d => d.LastSeenAt).HasColumnType("datetime2");

            entity.Property(d => d.LastSeenSeq).HasDefaultValue(0);

            entity.Property(d => d.IsActive).HasDefaultValue(true);

        });

        // Session configuration

        builder.Entity<Session>(entity =>

        {

            entity.HasKey(s => s.SessionId);

            entity.Property(s => s.WorkerId).HasMaxLength(450).IsRequired();

            entity.Property(s => s.Status).HasColumnType("tinyint");

            entity.Property(s => s.FinalResult).HasColumnType("tinyint");

            entity.Property(s => s.Remarks).HasMaxLength(500);

            entity.Property(s => s.StartTime).HasColumnType("datetime2");

            entity.Property(s => s.EndTime).HasColumnType("datetime2");

            entity.Property(s => s.UnitNo).HasDefaultValue(1);

            entity.Property(s => s.ComponentInstanceNo).HasDefaultValue(1);

            entity.Property(s => s.CurrentStepSequence).HasDefaultValue(1);

            // One InProgress session per (JobId, ProductComponentId, UnitNo, ComponentInstanceNo)

            entity.HasIndex(s => new { s.JobId, s.ProductComponentId, s.UnitNo, s.ComponentInstanceNo })

                  .IsUnique()

                  .HasFilter("[Status] = 1");

            // Recommended queries

            entity.HasIndex(s => new { s.TorqueDeviceId, s.StartTime });

            entity.HasIndex(s => new { s.VernierDeviceId, s.StartTime });

            entity.HasIndex(s => new { s.WorkerId, s.StartTime });

            entity.HasIndex(s => s.StartTime);

            entity.HasIndex(s => new { s.Status, s.StartTime });

            entity.HasIndex(s => new { s.JobId, s.Status });

            entity.HasOne(s => s.Job)

                  .WithMany()

                  .HasForeignKey(s => s.JobId)

                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.ProductComponent)

                  .WithMany()

                  .HasForeignKey(s => s.ProductComponentId)

                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Worker)

                  .WithMany()

                  .HasForeignKey(s => s.WorkerId)

                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.TorqueDevice)

                  .WithMany(d => d.TorqueSessions)

                  .HasForeignKey(s => s.TorqueDeviceId)

                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.VernierDevice)

                  .WithMany(d => d.VernierSessions)

                  .HasForeignKey(s => s.VernierDeviceId)

                  .OnDelete(DeleteBehavior.Restrict);

        });

        // SessionResult configuration (immutable, append-only)

        builder.Entity<Core.Entities.SessionResult>(entity =>

        {

            entity.HasKey(r => r.ResultId);

            entity.Property(r => r.InstrumentType).HasColumnType("tinyint");

            entity.Property(r => r.TargetValue).HasColumnType("decimal(10,3)");

            entity.Property(r => r.TolerancePlus).HasColumnType("decimal(10,3)");

            entity.Property(r => r.ToleranceMinus).HasColumnType("decimal(10,3)");

            entity.Property(r => r.ToleranceMode).HasColumnType("tinyint");

            entity.Property(r => r.LowerLimit).HasColumnType("decimal(10,3)");

            entity.Property(r => r.UpperLimit).HasColumnType("decimal(10,3)");

            entity.Property(r => r.ActualValue).HasColumnType("decimal(10,3)");

            entity.Property(r => r.Result).HasColumnType("tinyint");

            entity.Property(r => r.ManualApprovedBy).HasMaxLength(450);

            entity.Property(r => r.ReasonCode).HasMaxLength(30);

            entity.Property(r => r.CapturedAt).HasColumnType("datetime2");

            entity.Property(r => r.ReceivedAt).HasColumnType("datetime2");

            // Deduplication index on (DeviceId, DeviceSeq)

            entity.HasIndex(r => new { r.DeviceId, r.DeviceSeq })

                  .IsUnique()

                  .HasFilter("[DeviceSeq] IS NOT NULL");

            // Query by session step and attempt

            entity.HasIndex(r => new { r.SessionId, r.StepId, r.AttemptNo });

            entity.HasOne(r => r.Session)

                  .WithMany(s => s.Results)

                  .HasForeignKey(r => r.SessionId)

                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Step)

                  .WithMany()

                  .HasForeignKey(r => r.StepId)

                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Device)

                  .WithMany(d => d.SessionResults)

                  .HasForeignKey(r => r.DeviceId)

                  .OnDelete(DeleteBehavior.SetNull);

        });

        // Company configuration

        builder.Entity<Company>(entity =>

        {

            entity.HasKey(c => c.CompanyId);

            entity.Property(c => c.Name).HasMaxLength(150).IsRequired();

            entity.Property(c => c.Code).HasMaxLength(50).IsRequired();

            entity.HasIndex(c => c.Code).IsUnique();

            entity.Property(c => c.IsActive).HasDefaultValue(true);

            entity.Property(c => c.CreatedAt).HasColumnType("datetime2");

        });

        // Plant configuration

        builder.Entity<Plant>(entity =>

        {

            entity.HasKey(p => p.PlantId);

            entity.Property(p => p.Name).HasMaxLength(150).IsRequired();

            entity.Property(p => p.Code).HasMaxLength(50).IsRequired();

            entity.Property(p => p.Location).HasMaxLength(250);

            entity.Property(p => p.IsActive).HasDefaultValue(true);

            entity.Property(p => p.CreatedAt).HasColumnType("datetime2");

            entity.HasIndex(p => new { p.CompanyId, p.Code }).IsUnique();

            entity.HasOne(p => p.Company)

                  .WithMany(c => c.Plants)

                  .HasForeignKey(p => p.CompanyId)

                  .OnDelete(DeleteBehavior.Cascade);

        });

        // Department configuration

        builder.Entity<Department>(entity =>

        {

            entity.HasKey(d => d.DepartmentId);

            entity.Property(d => d.Name).HasMaxLength(150).IsRequired();

            entity.Property(d => d.Code).HasMaxLength(50).IsRequired();

            entity.Property(d => d.IsActive).HasDefaultValue(true);

            entity.Property(d => d.CreatedAt).HasColumnType("datetime2");

            entity.HasIndex(d => new { d.PlantId, d.Code }).IsUnique();

            entity.HasOne(d => d.Plant)

                  .WithMany(p => p.Departments)

                  .HasForeignKey(d => d.PlantId)

                  .OnDelete(DeleteBehavior.Cascade);

        });

        // WorkStation configuration

        builder.Entity<WorkStation>(entity =>

        {

            entity.HasKey(w => w.WorkStationId);

            entity.Property(w => w.StationType).HasColumnType("tinyint").HasDefaultValue(StationType.Station).HasSentinel((StationType)0);

            entity.Property(w => w.StationCode).HasMaxLength(50).IsRequired();

            entity.Property(w => w.StationName).HasMaxLength(150).IsRequired();

            entity.Property(w => w.Description).HasMaxLength(500);

            entity.Property(w => w.IsActive).HasDefaultValue(true);

            entity.Property(w => w.CreatedAt).HasColumnType("datetime2");

            entity.HasIndex(w => new { w.AssemblyLineId, w.StationCode }).IsUnique();

            entity.HasIndex(w => new { w.AssemblyLineId, w.IsActive });

            entity.HasOne(w => w.AssemblyLine)

                  .WithMany(a => a.WorkStations)

                  .HasForeignKey(w => w.AssemblyLineId)

                  .OnDelete(DeleteBehavior.Restrict);

        });

        // HierarchyNode configuration

        builder.Entity<HierarchyNode>(entity =>

        {

            entity.HasKey(n => n.NodeId);

            entity.Property(n => n.Code).HasMaxLength(50).IsRequired();

            entity.Property(n => n.Name).HasMaxLength(150).IsRequired();

            entity.Property(n => n.NodeType).HasColumnType("tinyint");

            entity.Property(n => n.Path).HasMaxLength(500).IsRequired();

            entity.Property(n => n.Depth).HasDefaultValue(0);

            entity.Property(n => n.SortOrder).HasDefaultValue(0);

            entity.Property(n => n.IsActive).HasDefaultValue(true);

            entity.Property(n => n.CreatedAt).HasColumnType("datetime2");

            entity.Property(n => n.UpdatedAt).HasColumnType("datetime2");

            entity.HasIndex(n => n.Code);

            entity.HasIndex(n => n.Path);

            entity.HasIndex(n => n.ParentNodeId);

            entity.HasIndex(n => n.AdminUserId);

            entity.HasOne(n => n.ParentNode)

                  .WithMany(p => p.Children)

                  .HasForeignKey(n => n.ParentNodeId)

                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(n => n.AdminUser)

                  .WithMany()

                  .HasForeignKey(n => n.AdminUserId)

                  .OnDelete(DeleteBehavior.SetNull);

        });

        // HierarchyNodeRequest configuration

        builder.Entity<HierarchyNodeRequest>(entity =>

        {

            entity.HasKey(r => r.RequestId);

            entity.Property(r => r.RequestedNodeName).HasMaxLength(150).IsRequired();

            entity.Property(r => r.RequestedNodeType).HasColumnType("tinyint");

            entity.Property(r => r.RequestStatus).HasColumnType("tinyint").HasDefaultValue(NodeRequestStatus.Pending).HasSentinel((NodeRequestStatus)0);

            entity.Property(r => r.RejectionReason).HasMaxLength(500);

            entity.Property(r => r.CreatedAt).HasColumnType("datetime2");

            entity.Property(r => r.ResolvedAt).HasColumnType("datetime2");

            entity.HasOne(r => r.ParentNode)

                  .WithMany(p => p.Requests)

                  .HasForeignKey(r => r.ParentNodeId)

                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.RequestedByUser)

                  .WithMany()

                  .HasForeignKey(r => r.RequestedByUserId)

                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(r => r.ApproverUser)

                  .WithMany()

                  .HasForeignKey(r => r.ApproverUserId)

                  .OnDelete(DeleteBehavior.NoAction);

        });

    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)

    {

        EnforceSessionResultImmutability();

        return base.SaveChanges(acceptAllChangesOnSuccess);

    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)

    {

        EnforceSessionResultImmutability();

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

    }

    private void EnforceSessionResultImmutability()

    {

        var invalidEntries = ChangeTracker.Entries<Core.Entities.SessionResult>()

            .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted)

            .ToList();

        if (invalidEntries.Count > 0)

        {

            throw new InvalidOperationException("Session results are immutable and cannot be updated or deleted (RPT-10).");

        }

    }

}