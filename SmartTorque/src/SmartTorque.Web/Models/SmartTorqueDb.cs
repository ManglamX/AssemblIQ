using System;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;

namespace SmartTorque.Web.Models;

public partial class SmartTorqueDb : DbContext
{
    public SmartTorqueDb(DbContextOptions<SmartTorqueDb> options)
        : base(options)
    {
    }

    public virtual DbSet<AspNetRole> AspNetRoles { get; set; } = null!;

    public virtual DbSet<AspNetRoleClaim> AspNetRoleClaims { get; set; } = null!;

    public virtual DbSet<AspNetUser> AspNetUsers { get; set; } = null!;

    public virtual DbSet<AspNetUserClaim> AspNetUserClaims { get; set; } = null!;

    public virtual DbSet<AspNetUserLogin> AspNetUserLogins { get; set; } = null!;

    public virtual DbSet<AspNetUserToken> AspNetUserTokens { get; set; } = null!;

    public virtual DbSet<AuditLog> AuditLogs { get; set; } = null!;

    public virtual DbSet<BmRow> BmRows { get; set; } = null!;

    public virtual DbSet<BmUpload> BmUploads { get; set; } = null!;

    public virtual DbSet<Job> Jobs { get; set; } = null!;

    public virtual DbSet<Notification> Notifications { get; set; } = null!;

    public virtual DbSet<PriorityColourMap> PriorityColourMaps { get; set; } = null!;

    public virtual DbSet<Product> Products { get; set; } = null!;

    public virtual DbSet<ProductComponent> ProductComponents { get; set; } = null!;

    public virtual DbSet<Setting> Settings { get; set; } = null!;

    public virtual DbSet<Template> Templates { get; set; } = null!;

    public virtual DbSet<TemplateStep> TemplateSteps { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AspNetRole>(entity =>
        {
            entity.HasIndex(e => e.NormalizedName, "RoleNameIndex")
                .IsUnique()
                .HasFilter("([NormalizedName] IS NOT NULL)");

            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.NormalizedName).HasMaxLength(256);
        });

        modelBuilder.Entity<AspNetRoleClaim>(entity =>
        {
            entity.HasIndex(e => e.RoleId, "IX_AspNetRoleClaims_RoleId");

            entity.HasOne(d => d.Role).WithMany(p => p.AspNetRoleClaims).HasForeignKey(d => d.RoleId);
        });

        modelBuilder.Entity<AspNetUser>(entity =>
        {
            entity.HasIndex(e => e.NormalizedEmail, "EmailIndex");

            entity.HasIndex(e => e.NormalizedUserName, "UserNameIndex")
                .IsUnique()
                .HasFilter("([NormalizedUserName] IS NOT NULL)");

            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.EmployeeNo).HasMaxLength(50);
            entity.Property(e => e.FullName).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.UserName).HasMaxLength(256);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "AspNetUserRole",
                    r => r.HasOne<AspNetRole>().WithMany().HasForeignKey("RoleId"),
                    l => l.HasOne<AspNetUser>().WithMany().HasForeignKey("UserId"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId");
                        j.ToTable("AspNetUserRoles");
                        j.HasIndex(new[] { "RoleId" }, "IX_AspNetUserRoles_RoleId");
                    });
        });

        modelBuilder.Entity<AspNetUserClaim>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_AspNetUserClaims_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserClaims).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserLogin>(entity =>
        {
            entity.HasKey(e => new { e.LoginProvider, e.ProviderKey });

            entity.HasIndex(e => e.UserId, "IX_AspNetUserLogins_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserLogins).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserToken>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserTokens).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(e => new { e.EntityName, e.EntityId }, "IX_AuditLogs_EntityName_EntityId");

            entity.HasIndex(e => e.OccurredAt, "IX_AuditLogs_OccurredAt");

            entity.Property(e => e.Action).HasMaxLength(100);
            entity.Property(e => e.EntityId).HasMaxLength(100);
            entity.Property(e => e.EntityName).HasMaxLength(100);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.UserName).HasMaxLength(256);
        });

        modelBuilder.Entity<BmRow>(entity =>
        {
            entity.HasKey(e => e.RowId);

            entity.HasIndex(e => new { e.ProductCode, e.DueDate }, "IX_BmRows_ProductCode_DueDate");

            entity.HasIndex(e => e.UploadId, "IX_BmRows_UploadId");

            entity.Property(e => e.ColourHex).HasMaxLength(9);
            entity.Property(e => e.ImportMessage).HasMaxLength(300);
            entity.Property(e => e.ProductCode).HasMaxLength(50);
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.Remarks).HasMaxLength(500);

            entity.HasOne(d => d.Upload).WithMany(p => p.BmRows).HasForeignKey(d => d.UploadId);
        });

        modelBuilder.Entity<BmUpload>(entity =>
        {
            entity.HasKey(e => e.UploadId);

            entity.HasIndex(e => e.UploadedAt, "IX_BmUploads_UploadedAt");

            entity.Property(e => e.FileName).HasMaxLength(260);
            entity.Property(e => e.StoredPath).HasMaxLength(300);
            entity.Property(e => e.UploadedBy).HasMaxLength(450);
        });

        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasIndex(e => new { e.AssignedWorkerId, e.Status }, "IX_Jobs_AssignedWorkerId_Status");

            entity.HasIndex(e => e.JobNo, "IX_Jobs_JobNo").IsUnique();

            entity.HasIndex(e => e.ProductId, "IX_Jobs_ProductId");

            entity.HasIndex(e => e.SourceBmRowId, "IX_Jobs_SourceBmRowId");

            entity.HasIndex(e => new { e.Status, e.Priority, e.DueDate }, "IX_Jobs_Status_Priority_DueDate");

            entity.Property(e => e.JobNo).HasMaxLength(30);
            entity.Property(e => e.ProductSerial).HasMaxLength(60);
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.AssignedWorker).WithMany(p => p.Jobs)
                .HasForeignKey(d => d.AssignedWorkerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.Product).WithMany(p => p.Jobs)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.SourceBmRow).WithMany(p => p.Jobs)
                .HasForeignKey(d => d.SourceBmRowId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(e => e.CreatedAt, "IX_Notifications_CreatedAt");

            entity.HasIndex(e => e.JobId, "IX_Notifications_JobId");

            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.TargetRole).HasMaxLength(30);

            entity.HasOne(d => d.Job).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.JobId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PriorityColourMap>(entity =>
        {
            entity.HasKey(e => e.MapId);

            entity.ToTable("PriorityColourMap");

            entity.HasIndex(e => e.Priority, "IX_PriorityColourMap_Priority");

            entity.Property(e => e.ColourHex).HasMaxLength(9);
            entity.Property(e => e.CreatedBy).HasMaxLength(450);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Label).HasMaxLength(30);
            entity.Property(e => e.MatchTolerance).HasDefaultValue(40);
            entity.Property(e => e.UpdatedBy).HasMaxLength(450);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(e => e.ProductCode, "IX_Products_ProductCode").IsUnique();

            entity.Property(e => e.CreatedBy).HasMaxLength(450);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ImagePath).HasMaxLength(300);
            entity.Property(e => e.ProductCode).HasMaxLength(50);
            entity.Property(e => e.ProductName).HasMaxLength(150);
            entity.Property(e => e.UpdatedBy).HasMaxLength(450);
        });

        modelBuilder.Entity<ProductComponent>(entity =>
        {
            entity.HasIndex(e => new { e.ProductId, e.AssemblyOrder }, "IX_ProductComponents_ProductId_AssemblyOrder");

            entity.HasIndex(e => e.TemplateId, "IX_ProductComponents_TemplateId");

            entity.Property(e => e.ComponentName).HasMaxLength(100);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductComponents).HasForeignKey(d => d.ProductId);

            entity.HasOne(d => d.Template).WithMany(p => p.ProductComponents)
                .HasForeignKey(d => d.TemplateId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasIndex(e => e.Key, "IX_Settings_Key").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Key).HasMaxLength(100);
            entity.Property(e => e.UpdatedBy).HasMaxLength(450);
            entity.Property(e => e.Value).HasMaxLength(500);
        });

        modelBuilder.Entity<Template>(entity =>
        {
            entity.HasIndex(e => e.IsLatest, "IX_Templates_IsLatest");

            entity.HasIndex(e => e.TemplateCode, "IX_Templates_TemplateCode");

            entity.HasIndex(e => new { e.TemplateCode, e.Version }, "IX_Templates_TemplateCode_Version").IsUnique();

            entity.Property(e => e.CreatedBy).HasMaxLength(450);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.ImagePath).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.TemplateCode).HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasMaxLength(450);
        });

        modelBuilder.Entity<TemplateStep>(entity =>
        {
            entity.HasIndex(e => new { e.TemplateId, e.SequenceNo }, "IX_TemplateSteps_TemplateId_SequenceNo").IsUnique();

            entity.Property(e => e.Label).HasMaxLength(200);
            entity.Property(e => e.MarkerX).HasColumnType("decimal(6, 3)");
            entity.Property(e => e.MarkerY).HasColumnType("decimal(6, 3)");
            entity.Property(e => e.Target).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.ToleranceMinus).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.TolerancePlus).HasColumnType("decimal(18, 4)");

            entity.HasOne(d => d.Template).WithMany(p => p.TemplateSteps).HasForeignKey(d => d.TemplateId);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}