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
    public DbSet<BmUpload> BmUploads => Set<BmUpload>();
    public DbSet<BmRow> BmRows => Set<BmRow>();
    public DbSet<PriorityColourMap> PriorityColourMap => Set<PriorityColourMap>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Core.Entities.SessionResult> SessionResults => Set<Core.Entities.SessionResult>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ApplicationUser configuration
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            entity.Property(u => u.EmployeeNo).HasMaxLength(50);
            entity.Property(u => u.IsActive).HasDefaultValue(true);
            entity.Property(u => u.CreatedAt).HasColumnType("datetime2");
            entity.Property(u => u.UpdatedAt).HasColumnType("datetime2");
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
            entity.Property(r => r.ImportStatus).HasColumnType("tinyint");
            entity.Property(r => r.ImportMessage).HasMaxLength(300);
            entity.HasIndex(r => r.UploadId);
            entity.HasIndex(r => new { r.ProductCode, r.DueDate });
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
            entity.Property(j => j.Quantity).HasDefaultValue(1);
            entity.Property(j => j.QuantityCompleted).HasDefaultValue(0);
            entity.Property(j => j.Priority).HasColumnType("tinyint");
            entity.Property(j => j.DueDate).HasColumnType("date");
            entity.Property(j => j.AssignmentMode).HasColumnType("tinyint");
            entity.Property(j => j.AssignedWorkerId).HasMaxLength(450);
            entity.Property(j => j.Status).HasColumnType("tinyint");
            entity.Property(j => j.ExecutionMode).HasColumnType("tinyint");
            entity.Property(j => j.CreatedAt).HasColumnType("datetime2");
            entity.Property(j => j.UpdatedAt).HasColumnType("datetime2");
            entity.Property(j => j.RowVersion).IsRowVersion();
            // Board ordering and "my jobs"
            entity.HasIndex(j => new { j.Status, j.Priority, j.DueDate });
            entity.HasIndex(j => new { j.AssignedWorkerId, j.Status });
            // A product is never deleted when jobs exist
            entity.HasOne(j => j.Product)
                  .WithMany()
                  .HasForeignKey(j => j.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(j => j.AssignedWorker)
                  .WithMany()
                  .HasForeignKey(j => j.AssignedWorkerId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(j => j.SourceBmRow)
                  .WithMany()
                  .HasForeignKey(j => j.SourceBmRowId)
                  .OnDelete(DeleteBehavior.SetNull);
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
    }
}