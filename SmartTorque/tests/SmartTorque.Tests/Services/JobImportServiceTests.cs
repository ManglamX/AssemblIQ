using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

/// <summary>
/// Tests for turning a reviewed BM preview into Jobs (PRD BM-5, BM-10;
/// Architecture §7.3 step 5). Uploads are produced by the real preview pipeline so
/// these exercise the path an Admin actually walks.
/// </summary>
public class JobImportServiceTests : IDisposable
{
    private const string DefaultHeaders = "bm-default-headers.xlsx";
    private const string CustomHeaders = "bm-custom-headers.xlsx";

    private const string CustomHeaderMapJson =
        "{\"ProductCode\":\"Product Code\",\"Quantity\":\"Qty\",\"DueDate\":\"Due Date\",\"Remarks\":\"Remarks\"}";

    private readonly AppDbContext _db;
    private readonly string _webRoot;
    private readonly BmPreviewService _previews;
    private readonly JobImportService _service;

    public JobImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _webRoot = Path.Combine(Path.GetTempPath(), "bm-confirm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_webRoot);

        var files = new BmFileStore(_webRoot);
        var import = new BmImportService(_db);
        _previews = new BmPreviewService(_db, import, files);
        _service = new JobImportService(_db, new NotificationService(), new PriorityService());
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_webRoot, true); } catch (IOException) { }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task<BmUpload> UploadAsync(string fixture, string fileName = "bm.xlsx")
    {
        await using var stream = File.OpenRead(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
        return await _previews.UploadAsync(stream, fileName, stream.Length, "admin");
    }

    // Seeding is idempotent: the re-upload tests call both helpers a second time against
    // the same database, and ProductCode is a domain-wide unique key (ConfirmAsync builds
    // a ProductCode-keyed dictionary from Products), so a repeated seed must never insert
    // a duplicate row.
    private async Task SeedProductsAsync(params (string Code, bool Active)[] products)
    {
        var existing = (await _db.Products
                .Select(p => p.ProductCode)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (code, active) in products)
        {
            if (existing.Contains(code))
            {
                continue;
            }

            _db.Products.Add(new Product
            {
                ProductCode = code,
                ProductName = code,
                IsActive = active,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test"
            });
        }

        await _db.SaveChangesAsync();
    }

    private async Task SeedColourMapAsync(params (string Hex, PriorityLevel Priority)[] shades)
    {
        var existing = (await _db.PriorityColourMap
                .Select(m => m.ColourHex)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (hex, priority) in shades)
        {
            if (existing.Contains(hex))
            {
                continue;
            }

            _db.PriorityColourMap.Add(new PriorityColourMap
            {
                ColourHex = hex,
                Priority = priority,
                Label = priority.ToString(),
                MatchTolerance = 40,
                IsActive = true
            });
        }

        await _db.SaveChangesAsync();
    }

    private Task UseCustomHeaderMapAsync()
    {
        _db.Settings.Add(new Setting { Key = BmSettings.HeaderMapKey, Value = CustomHeaderMapJson });
        return _db.SaveChangesAsync();
    }

    /// <summary>Uploads the default workbook, confirms it, and returns its four jobs.</summary>
    private async Task<List<Job>> ConfirmDefaultJobsAsync()
    {
        var upload = await PrepareDefaultAsync();
        await _service.ConfirmAsync(upload.UploadId, "admin");

        _db.ChangeTracker.Clear();
        return await _db.Jobs.OrderBy(j => j.JobId).ToListAsync();
    }

    private async Task<BmUpload> PrepareDefaultAsync()
    {
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));
        return await UploadAsync(DefaultHeaders);
    }

    /// <summary>The custom sheet: rows 2 and 7 importable, rows 3-6 are errors, row 7 is Black.</summary>
    private async Task<BmUpload> PrepareCustomAsync()
    {
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#000000", PriorityLevel.Black), ("#FFFFFF", PriorityLevel.White));
        await UseCustomHeaderMapAsync();
        return await UploadAsync(CustomHeaders);
    }

    private static string TodayPrefix => $"JOB-{DateTime.UtcNow:yyyyMMdd}-";

    // =======================================================================
    // Job creation
    // =======================================================================

    [Fact]
    public async Task ConfirmAsync_CreatesOneJobPerImportableRow()
    {
        var upload = await PrepareDefaultAsync();

        var result = await _service.ConfirmAsync(upload.UploadId, "admin");

        result.JobsCreated.Should().Be(4);
        result.JobNumbers.Should().HaveCount(4);

        var jobs = await _db.Jobs.OrderBy(j => j.JobId).ToListAsync();
        jobs.Should().HaveCount(4);
        jobs.Should().OnlyContain(j => j.JobNo.StartsWith(TodayPrefix));
        jobs.Select(j => j.JobNo).Should().BeEquivalentTo(result.JobNumbers);

        var product = await _db.Products.SingleAsync(p => p.ProductCode == "LK-6P");

        foreach (var job in jobs)
        {
            var row = await _db.BmRows.SingleAsync(r => r.RowId == job.SourceBmRowId);
            job.ProductId.Should().Be(product.ProductId);
            job.Quantity.Should().Be(row.Quantity);
            job.Priority.Should().Be(row.Priority);
            job.DueDate.Should().Be(row.DueDate);
        }
    }

    [Fact]
    public async Task ConfirmAsync_AllocatesContiguousJobNumbersFromOne()
    {
        var upload = await PrepareDefaultAsync();

        var result = await _service.ConfirmAsync(upload.UploadId, "admin");

        result.JobNumbers.Should().Equal(
            $"{TodayPrefix}0001",
            $"{TodayPrefix}0002",
            $"{TodayPrefix}0003",
            $"{TodayPrefix}0004");
    }

    [Fact]
    public async Task ConfirmAsync_ContinuesTheDaysNumberingOnASecondUpload()
    {
        var first = await PrepareDefaultAsync();
        await _service.ConfirmAsync(first.UploadId, "admin");

        // Existing jobs are in progress, so BM-6 leaves them untouched and allocates new jobs
        foreach (var job in _db.Jobs)
        {
            job.Status = JobStatus.InProgress;
        }
        await _db.SaveChangesAsync();

        // A second workbook on the same day must not restart at 0001.
        _db.ChangeTracker.Clear();
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));
        var second = await UploadAsync(DefaultHeaders);

        var result = await _service.ConfirmAsync(second.UploadId, "admin");

        result.JobNumbers.Should().Equal(
            $"{TodayPrefix}0005",
            $"{TodayPrefix}0006",
            $"{TodayPrefix}0007",
            $"{TodayPrefix}0008");
    }

    [Fact]
    public async Task ConfirmAsync_SetsTheFieldsTheSpecCallsFor()
    {
        var upload = await PrepareDefaultAsync();

        await _service.ConfirmAsync(upload.UploadId, "admin");

        var job = await _db.Jobs.FirstAsync();

        job.Status.Should().Be(JobStatus.Pending);
        job.ProductSerial.Should().BeNull("serials wait on Q-07");
        job.AssignmentMode.Should().Be(AssignmentMode.Manual);
        job.ExecutionMode.Should().Be(ExecutionMode.Manual);
        job.Quantity.Should().BeGreaterThan(0);
        job.QuantityCompleted.Should().Be(0);
        job.AssignedWorkerId.Should().BeNull();
        job.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }

    // =======================================================================
    // Which rows become jobs
    // =======================================================================

    [Fact]
    public async Task ConfirmAsync_LeavesErrorAndSkippedRowsAlone()
    {
        var upload = await PrepareCustomAsync();

        // Rows 3-6 are errors (unknown product, bad quantity, bad date); row 2 is
        // importable but the Admin skips it, leaving only row 7 (Black).
        var skippable = upload.Rows.Single(r => r.ExcelRowNo == 2);
        await _previews.SetRowSkippedAsync(skippable.RowId, true);

        var result = await _service.ConfirmAsync(upload.UploadId, "admin");

        result.JobsCreated.Should().Be(1);

        var job = await _db.Jobs.SingleAsync();
        var importedRow = await _db.BmRows.SingleAsync(r => r.RowId == job.SourceBmRowId);
        importedRow.ExcelRowNo.Should().Be(7);
        importedRow.ImportStatus.Should().Be(BmRowImportStatus.Imported);

        (await _db.BmRows.SingleAsync(r => r.RowId == skippable.RowId)).ImportStatus
            .Should().Be(BmRowImportStatus.Skipped);

        (await _db.BmRows.CountAsync(r => r.ImportStatus == BmRowImportStatus.Error))
            .Should().Be(4, "the error rows are left for the Admin, not imported");
    }

    [Fact]
    public async Task ConfirmAsync_MarksTheRowsImportedAndTheUploadConfirmed()
    {
        var upload = await PrepareDefaultAsync();

        await _service.ConfirmAsync(upload.UploadId, "admin");

        var stored = await _db.BmUploads
            .Include(u => u.Rows)
            .SingleAsync(u => u.UploadId == upload.UploadId);

        stored.Status.Should().Be(BmUploadStatus.Confirmed);
        stored.Rows.Should().OnlyContain(r => r.ImportStatus == BmRowImportStatus.Imported);
    }

    // =======================================================================
    // Refusals — none of these write anything
    // =======================================================================

    [Fact]
    public async Task ConfirmAsync_RefusesAnAlreadyImportedUpload()
    {
        var upload = await PrepareDefaultAsync();
        await _service.ConfirmAsync(upload.UploadId, "admin");

        var act = () => _service.ConfirmAsync(upload.UploadId, "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already been imported*");
        (await _db.Jobs.CountAsync()).Should().Be(4, "the retry must not duplicate jobs");
    }

    [Fact]
    public async Task ConfirmAsync_RefusesWhenNothingIsImportable()
    {
        var upload = await PrepareDefaultAsync();
        foreach (var row in upload.Rows)
            await _previews.SetRowSkippedAsync(row.RowId, true);

        var act = () => _service.ConfirmAsync(upload.UploadId, "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nothing to import*");
        (await _db.Jobs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ConfirmAsync_RefusesAnUnknownUpload()
    {
        var act = () => _service.ConfirmAsync(9999, "admin");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ConfirmAsync_RefusesWhenTheProductWasDeactivatedAfterThePreview()
    {
        var upload = await PrepareDefaultAsync();

        var product = await _db.Products.SingleAsync(p => p.ProductCode == "LK-6P");
        product.IsActive = false;
        await _db.SaveChangesAsync();

        var act = () => _service.ConfirmAsync(upload.UploadId, "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*active product*");

        // Nothing happened: no jobs, no notification, preview untouched.
        (await _db.Jobs.CountAsync()).Should().Be(0);
        (await _db.Notifications.CountAsync()).Should().Be(0);
        (await _db.BmUploads.SingleAsync(u => u.UploadId == upload.UploadId)).Status
            .Should().Be(BmUploadStatus.Previewed);
        (await _db.BmRows.CountAsync(r => r.ImportStatus == BmRowImportStatus.Imported))
            .Should().Be(0);
    }

    // =======================================================================
    // Black-job notification (PRD BM-10)
    // =======================================================================

    [Fact]
    public async Task ConfirmAsync_RaisesOneNoticeNamingTheBlackJob()
    {
        var upload = await PrepareCustomAsync();

        var result = await _service.ConfirmAsync(upload.UploadId, "admin");

        result.BlackJobs.Should().Be(1);
        result.NotificationRaised.Should().BeTrue();

        var notice = await _db.Notifications.SingleAsync();
        notice.Type.Should().Be(NotificationType.BlackJob);
        notice.TargetRole.Should().Be("Worker");
        notice.Message.Should().Contain($"{TodayPrefix}0002", "row 7 is the second job");

        var blackJob = await _db.Jobs.SingleAsync(j => j.Priority == PriorityLevel.Black);
        notice.JobId.Should().Be(blackJob.JobId, "the notice links to the job it announces");
        notice.ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task ConfirmAsync_RaisesNoNoticeWhenNothingIsBlack()
    {
        var upload = await PrepareDefaultAsync();

        var result = await _service.ConfirmAsync(upload.UploadId, "admin");

        result.BlackJobs.Should().Be(0);
        result.NotificationRaised.Should().BeFalse();
        (await _db.Notifications.CountAsync()).Should().Be(0);
    }

    // =======================================================================
    // Audit and read-back
    // =======================================================================

    [Fact]
    public async Task ConfirmAsync_WritesAnAuditEntry()
    {
        var upload = await PrepareDefaultAsync();

        await _service.ConfirmAsync(upload.UploadId, "admin@smarttorque.local");

        var audit = await _db.AuditLogs.SingleAsync(a => a.Action == "Confirmed");
        audit.EntityName.Should().Be(nameof(BmUpload));
        audit.EntityId.Should().Be(upload.UploadId.ToString());
        audit.UserName.Should().Be("admin@smarttorque.local");
        audit.Changes.Should().Contain("JobsCreated");
    }

    [Fact]
    public async Task GetJobNumbersAsync_ReturnsOnlyThatUploadsJobs()
    {
        var first = await PrepareDefaultAsync();
        await _service.ConfirmAsync(first.UploadId, "admin");

        // Existing jobs are in progress, so BM-6 leaves them untouched and allocates new jobs for second
        foreach (var job in _db.Jobs)
        {
            job.Status = JobStatus.InProgress;
        }
        await _db.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));
        var second = await UploadAsync(DefaultHeaders);
        await _service.ConfirmAsync(second.UploadId, "admin");

        var firstNumbers = await _service.GetJobNumbersAsync(first.UploadId);
        var secondNumbers = await _service.GetJobNumbersAsync(second.UploadId);

        firstNumbers.Should().HaveCount(4);
        secondNumbers.Should().HaveCount(4);
        firstNumbers.Should().NotIntersectWith(secondNumbers);
    }

    [Fact]
    public async Task GetJobNumbersAsync_IsEmptyForAUploadThatWasNeverConfirmed()
    {
        var upload = await PrepareDefaultAsync();

        (await _service.GetJobNumbersAsync(upload.UploadId)).Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Workload board (PRD BM-9, Design §7.7)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetWorkloadAsync_OrdersBlackFirstThenByDueDate()
    {
        var jobs = await ConfirmDefaultJobsAsync();
        jobs[0].Priority = PriorityLevel.Black;
        jobs[0].DueDate = new DateOnly(2026, 10, 20);
        jobs[1].Priority = PriorityLevel.White;
        jobs[1].DueDate = new DateOnly(2026, 10, 25);
        jobs[2].Priority = PriorityLevel.Red;
        jobs[2].DueDate = null;
        jobs[3].Priority = PriorityLevel.White;
        jobs[3].DueDate = new DateOnly(2026, 11, 1);
        await _db.SaveChangesAsync();

        var result = await _service.GetWorkloadAsync(new WorkloadFilter(), 1, 50);

        result.Total.Should().Be(4);
        result.Jobs.Select(j => j.JobId).Should().ContainInOrder(
            jobs[0].JobId, jobs[2].JobId, jobs[1].JobId, jobs[3].JobId);
        result.Jobs.First().Priority.Should().Be(PriorityLevel.Black, "Black jobs are pinned to the top");
    }

    [Fact]
    public async Task GetWorkloadAsync_FiltersByTheTickedPriorities()
    {
        var jobs = await ConfirmDefaultJobsAsync();
        jobs[0].Priority = PriorityLevel.Black;
        jobs[1].Priority = PriorityLevel.White;
        jobs[2].Priority = PriorityLevel.Red;
        jobs[3].Priority = PriorityLevel.White;
        await _db.SaveChangesAsync();

        var blackAndRed = await _service.GetWorkloadAsync(
            new WorkloadFilter { Priorities = [PriorityLevel.Black, PriorityLevel.Red] }, 1, 50);

        blackAndRed.Total.Should().Be(2);
        blackAndRed.Jobs.Select(j => j.Priority).Should().BeEquivalentTo(
            new[] { PriorityLevel.Black, PriorityLevel.Red });

        var everything = await _service.GetWorkloadAsync(new WorkloadFilter(), 1, 50);
        everything.Total.Should().Be(4, "an empty priority selection means no priority filter");
    }

    [Fact]
    public async Task GetWorkloadAsync_DueRangeIsInclusiveAndSkipsJobsWithoutADueDate()
    {
        var jobs = await ConfirmDefaultJobsAsync();
        jobs[0].DueDate = new DateOnly(2026, 10, 20);
        jobs[1].DueDate = new DateOnly(2026, 10, 25);
        jobs[2].DueDate = null;
        jobs[3].DueDate = new DateOnly(2026, 11, 1);
        await _db.SaveChangesAsync();

        var window = await _service.GetWorkloadAsync(
            new WorkloadFilter
            {
                DueFrom = new DateOnly(2026, 10, 20),
                DueTo = new DateOnly(2026, 10, 25)
            },
            1,
            50);

        window.Total.Should().Be(2, "both endpoints are inclusive");
        window.Jobs.Select(j => j.JobId).Should().BeEquivalentTo(new[] { jobs[0].JobId, jobs[1].JobId });

        var fromNovember = await _service.GetWorkloadAsync(
            new WorkloadFilter { DueFrom = new DateOnly(2026, 11, 1) }, 1, 50);

        fromNovember.Total.Should().Be(1);
        fromNovember.Jobs.Single().JobId.Should().Be(jobs[3].JobId);
    }

    [Fact]
    public async Task GetWorkloadAsync_FiltersByStatus()
    {
        var jobs = await ConfirmDefaultJobsAsync();
        jobs[0].Status = JobStatus.OnHold;
        jobs[2].Status = JobStatus.OnHold;
        await _db.SaveChangesAsync();

        var onHold = await _service.GetWorkloadAsync(
            new WorkloadFilter { Status = JobStatus.OnHold }, 1, 50);

        onHold.Total.Should().Be(2);
        onHold.Jobs.Select(j => j.JobId).Should().BeEquivalentTo(new[] { jobs[0].JobId, jobs[2].JobId });

        var pending = await _service.GetWorkloadAsync(
            new WorkloadFilter { Status = JobStatus.Pending }, 1, 50);

        pending.Total.Should().Be(2, "the other two jobs are still Pending");
    }

    [Fact]
    public async Task GetWorkloadAsync_FiltersOpenJobsAndOneWorkersJobs()
    {
        var jobs = await ConfirmDefaultJobsAsync();
        jobs[0].AssignedWorkerId = "worker-a";
        jobs[1].AssignedWorkerId = "worker-a";
        await _db.SaveChangesAsync();

        var open = await _service.GetWorkloadAsync(new WorkloadFilter { UnassignedOnly = true }, 1, 50);
        open.Total.Should().Be(2);
        open.Jobs.Should().OnlyContain(j => j.AssignedWorkerId == null);

        var hers = await _service.GetWorkloadAsync(
            new WorkloadFilter { AssignedWorkerId = "worker-a" }, 1, 50);
        hers.Total.Should().Be(2);
        hers.Jobs.Should().OnlyContain(j => j.AssignedWorkerId == "worker-a");

        (await _service.GetWorkloadAsync(new WorkloadFilter(), 1, 50)).Total.Should().Be(4);
    }

    [Fact]
    public async Task GetWorkloadAsync_PaginatesAndKeepsTheTotalAcrossPages()
    {
        await ConfirmDefaultJobsAsync();

        var firstPage = await _service.GetWorkloadAsync(new WorkloadFilter(), 1, 2);
        var secondPage = await _service.GetWorkloadAsync(new WorkloadFilter(), 2, 2);
        var beyond = await _service.GetWorkloadAsync(new WorkloadFilter(), 9, 2);
        var pageZero = await _service.GetWorkloadAsync(new WorkloadFilter(), 0, 50);

        firstPage.Total.Should().Be(4);
        firstPage.Jobs.Should().HaveCount(2);
        secondPage.Total.Should().Be(4, "the total counts every match, not just the page");
        secondPage.Jobs.Should().HaveCount(2);
        secondPage.Jobs.Select(j => j.JobId).Should().NotIntersectWith(firstPage.Jobs.Select(j => j.JobId));
        beyond.Total.Should().Be(4);
        beyond.Jobs.Should().BeEmpty();
        pageZero.Jobs.Should().HaveCount(4, "page numbers below 1 clamp to the first page");
    }

    [Fact]
    public async Task GetWorkloadByItemAsync_GroupsJobsByItemCodeAndSumsQuantitiesAcrossBms()
    {
        await ConfirmDefaultJobsAsync();

        var summaries = await _service.GetWorkloadByItemAsync();

        summaries.Should().NotBeEmpty();
        foreach (var item in summaries)
        {
            item.ItemCode.Should().NotBeNullOrWhiteSpace();
            item.TotalQuantityRequired.Should().Be(item.Jobs.Sum(j => j.Quantity));
            item.TotalJobs.Should().Be(item.Jobs.Count);
            item.Jobs.Should().OnlyContain(j => !string.IsNullOrEmpty(j.JobNo));
        }

        // Test filtering by search
        var firstCode = summaries[0].ItemCode;
        var filtered = await _service.GetWorkloadByItemAsync(firstCode);
        filtered.Should().ContainSingle(i => i.ItemCode == firstCode);
    }

    [Fact]
    public async Task AssignJobAsync_AssignsWorkerAndSetsAssignedStatus_AndCanUnassign()
    {
        await ConfirmDefaultJobsAsync();

        var job = await _db.Jobs.FirstAsync();
        var worker = new ApplicationUser { Id = "worker-1", UserName = "jdoe", FullName = "John Doe", IsActive = true };
        _db.Users.Add(worker);
        await _db.SaveChangesAsync();

        // Assign worker
        var assigned = await _service.AssignJobAsync(job.JobId, "worker-1", PriorityLevel.Red, "admin");
        assigned.AssignedWorkerId.Should().Be("worker-1");
        assigned.Status.Should().Be(JobStatus.Assigned);
        assigned.Priority.Should().Be(PriorityLevel.Red);
        assigned.AssignmentMode.Should().Be(AssignmentMode.Manual);

        // Unassign worker back to Open
        var unassigned = await _service.AssignJobAsync(job.JobId, null, null, "admin");
        unassigned.AssignedWorkerId.Should().BeNull();
        unassigned.Status.Should().Be(JobStatus.Pending);
        unassigned.Priority.Should().Be(PriorityLevel.Red, "priority is preserved when not overridden");
    }

    [Fact]
    public async Task AutoAssignJobsAsync_DistributesJobsAcrossActiveWorkers()
    {
        await ConfirmDefaultJobsAsync();

        var workerRole = new Microsoft.AspNetCore.Identity.IdentityRole { Id = "role-worker", Name = "Worker", NormalizedName = "WORKER" };
        _db.Roles.Add(workerRole);

        var w1 = new ApplicationUser { Id = "auto-w1", UserName = "alice", FullName = "Alice", IsActive = true };
        var w2 = new ApplicationUser { Id = "auto-w2", UserName = "bob", FullName = "Bob", IsActive = true };
        _db.Users.AddRange(w1, w2);

        _db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { RoleId = "role-worker", UserId = "auto-w1" });
        _db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { RoleId = "role-worker", UserId = "auto-w2" });
        await _db.SaveChangesAsync();

        var count = await _service.AutoAssignJobsAsync("admin");
        count.Should().Be(4);

        var jobs = await _db.Jobs.ToListAsync();
        jobs.Should().OnlyContain(j => j.Status == JobStatus.Assigned);
        jobs.Should().OnlyContain(j => j.AssignmentMode == AssignmentMode.Auto);

        // Workload is balanced: 2 jobs to w1, 2 jobs to w2
        jobs.Count(j => j.AssignedWorkerId == "auto-w1").Should().Be(2);
        jobs.Count(j => j.AssignedWorkerId == "auto-w2").Should().Be(2);
    }

    [Fact]
    public async Task Hold_Resume_And_Cancel_TransitionsJobStatusCorrectly()
    {
        await ConfirmDefaultJobsAsync();

        var job = await _db.Jobs.FirstAsync();

        // Hold
        var held = await _service.HoldJobAsync(job.JobId, "Inspection pending", "admin");
        held.Status.Should().Be(JobStatus.OnHold);

        // Resume (unassigned -> Pending)
        var resumed = await _service.ResumeJobAsync(job.JobId, "admin");
        resumed.Status.Should().Be(JobStatus.Pending);

        // Cancel
        var cancelled = await _service.CancelJobAsync(job.JobId, "No longer needed", "admin");
        cancelled.Status.Should().Be(JobStatus.Cancelled);

        // Cannot hold or reassign a cancelled job
        var actHold = () => _service.HoldJobAsync(job.JobId, "try hold", "admin");
        await actHold.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot hold a job in status Cancelled*");
    }

    // =======================================================================
    // P4.10: Re-upload Refresh Rule (BM-6 / Architecture §7.3 step 6)
    // =======================================================================

    [Fact]
    public async Task ConfirmAsync_Reupload_RefreshesOpenJobs_PriorityAndQuantity()
    {
        var first = await PrepareDefaultAsync();
        var firstResult = await _service.ConfirmAsync(first.UploadId, "admin");
        firstResult.JobsCreated.Should().Be(4);

        var firstJob = await _db.Jobs.FirstAsync(j => j.JobNo == firstResult.JobNumbers[0]);
        var originalJobNo = firstJob.JobNo;
        var originalId = firstJob.JobId;

        // Second upload: same ProductCode and DueDates, but change row priorities and quantities
        _db.ChangeTracker.Clear();
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FF0000", PriorityLevel.Red), ("#FFFFFF", PriorityLevel.White));
        var second = await UploadAsync(DefaultHeaders);

        // Override row priority in preview before confirm
        var secondRows = await _db.BmRows.Where(r => r.UploadId == second.UploadId).ToListAsync();
        foreach (var r in secondRows)
        {
            r.Priority = PriorityLevel.Red;
            r.Quantity += 10;
        }
        await _db.SaveChangesAsync();

        var preview = await _service.GetRefreshPreviewAsync(second.UploadId);
        preview.MatchedRows.Should().HaveCount(4);
        preview.AbsentOpenJobs.Should().BeEmpty();

        var secondResult = await _service.ConfirmAsync(second.UploadId, "admin");

        secondResult.JobsRefreshed.Should().Be(4);
        secondResult.JobsCreated.Should().Be(0);
        secondResult.RefreshedJobNumbers.Should().Contain(originalJobNo);

        var refreshedJob = await _db.Jobs.FirstAsync(j => j.JobId == originalId);
        refreshedJob.JobNo.Should().Be(originalJobNo, "re-upload must not allocate new job numbers to refreshed jobs");
        refreshedJob.Priority.Should().Be(PriorityLevel.Red);
        refreshedJob.Quantity.Should().Be(15); // original was 5 + 10
        refreshedJob.UpdatedAt.Should().NotBeNull();
        refreshedJob.SourceBmRowId.Should().Be(secondRows.First(r => r.DueDate == refreshedJob.DueDate).RowId);
    }

    [Fact]
    public async Task ConfirmAsync_Reupload_LeavesInProgressJobsUntouched_AndCreatesNewJob()
    {
        var first = await PrepareDefaultAsync();
        var firstResult = await _service.ConfirmAsync(first.UploadId, "admin");

        // First job is taken by worker and marked InProgress
        var inProgressJob = await _db.Jobs.FirstAsync(j => j.JobNo == firstResult.JobNumbers[0]);
        inProgressJob.Status = JobStatus.InProgress;
        inProgressJob.Priority = PriorityLevel.White;
        inProgressJob.Quantity = 5;
        await _db.SaveChangesAsync();

        // Second upload has the same row
        _db.ChangeTracker.Clear();
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FF0000", PriorityLevel.Red), ("#FFFFFF", PriorityLevel.White));
        var second = await UploadAsync(DefaultHeaders);

        var secondRows = await _db.BmRows.Where(r => r.UploadId == second.UploadId).ToListAsync();
        foreach (var r in secondRows)
        {
            r.Priority = PriorityLevel.Red;
            r.Quantity = 20;
        }
        await _db.SaveChangesAsync();

        var result = await _service.ConfirmAsync(second.UploadId, "admin");

        // 3 open jobs were refreshed; the InProgress job was left untouched and resulted in 1 new job created
        result.JobsRefreshed.Should().Be(3);
        result.JobsCreated.Should().Be(1);

        // Verify in-progress job was untouched
        var unchanged = await _db.Jobs.FirstAsync(j => j.JobId == inProgressJob.JobId);
        unchanged.Status.Should().Be(JobStatus.InProgress);
        unchanged.Priority.Should().Be(PriorityLevel.White);
        unchanged.Quantity.Should().Be(5);
    }

    [Fact]
    public async Task ConfirmAsync_Reupload_LeavesCompletedAndCancelledJobsUntouched()
    {
        var first = await PrepareDefaultAsync();
        var firstResult = await _service.ConfirmAsync(first.UploadId, "admin");

        var jobs = await _db.Jobs.Take(2).ToListAsync();
        jobs[0].Status = JobStatus.Completed;
        jobs[1].Status = JobStatus.Cancelled;
        await _db.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));
        var second = await UploadAsync(DefaultHeaders);

        var result = await _service.ConfirmAsync(second.UploadId, "admin");

        // 2 open jobs refreshed, 2 new jobs created for the Completed and Cancelled rows
        result.JobsRefreshed.Should().Be(2);
        result.JobsCreated.Should().Be(2);
    }

    [Fact]
    public async Task ConfirmAsync_Reupload_EscalationToBlack_RaisesNotification()
    {
        var first = await PrepareDefaultAsync();
        await _service.ConfirmAsync(first.UploadId, "admin");

        _db.ChangeTracker.Clear();
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#000000", PriorityLevel.Black), ("#FFFFFF", PriorityLevel.White));
        var second = await UploadAsync(DefaultHeaders);

        // Escalate one row to Black
        var firstRow = await _db.BmRows.FirstAsync(r => r.UploadId == second.UploadId);
        firstRow.Priority = PriorityLevel.Black;
        await _db.SaveChangesAsync();

        var result = await _service.ConfirmAsync(second.UploadId, "admin");

        result.BlackJobs.Should().Be(1);
        result.NotificationRaised.Should().BeTrue();

        var notice = await _db.Notifications.FirstAsync(n => n.Type == NotificationType.BlackJob);
        notice.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAbsentJobsAsync_ReturnsOpenJobsAbsentFromUpload()
    {
        var first = await PrepareDefaultAsync();
        await _service.ConfirmAsync(first.UploadId, "admin");

        // Upload a second workbook with custom headers having different row dates/codes
        _db.ChangeTracker.Clear();
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#000000", PriorityLevel.Black), ("#FFFFFF", PriorityLevel.White));
        await UseCustomHeaderMapAsync();
        var second = await UploadAsync(CustomHeaders);

        var absentJobs = await _service.GetAbsentJobsAsync(second.UploadId);
        absentJobs.Should().NotBeEmpty();
        absentJobs.Should().OnlyContain(j => j.Status == JobStatus.Pending || j.Status == JobStatus.Assigned || j.Status == JobStatus.OnHold);
    }

    // =======================================================================
    // P4.11: Job Detail Queries & State Machine (Architecture §6.1)
    // =======================================================================

    [Fact]
    public async Task GetJobByIdAsync_LoadsJobWithFullHierarchy()
    {
        await ConfirmDefaultJobsAsync();
        var job = await _db.Jobs.FirstAsync();

        var loaded = await _service.GetJobByIdAsync(job.JobId);
        loaded.Should().NotBeNull();
        loaded!.Product.Should().NotBeNull();
        loaded.Product.ProductCode.Should().Be("LK-6P");
        loaded.SourceBmRow.Should().NotBeNull();
    }

    [Fact]
    public async Task ResumeJobAsync_RestoresToAssigned_WhenWorkerIsAssigned()
    {
        await ConfirmDefaultJobsAsync();
        var job = await _db.Jobs.FirstAsync();

        // Assign worker
        var worker = new ApplicationUser { Id = "w-1", UserName = "worker1", FullName = "Worker 1", IsActive = true };
        _db.Users.Add(worker);
        await _db.SaveChangesAsync();

        await _service.AssignJobAsync(job.JobId, "w-1", null, "admin");

        // Put on hold
        await _service.HoldJobAsync(job.JobId, "Hold for inspection", "admin");
        (await _db.Jobs.FindAsync(job.JobId))!.Status.Should().Be(JobStatus.OnHold);

        // Resume: worker is assigned, so status should become Assigned
        var resumed = await _service.ResumeJobAsync(job.JobId, "admin");
        resumed.Status.Should().Be(JobStatus.Assigned);
    }

    [Fact]
    public async Task JobStateMachine_Guards_PreventInvalidTransitions()
    {
        await ConfirmDefaultJobsAsync();
        var job = await _db.Jobs.FirstAsync();

        // Cannot resume a job that is not OnHold
        var actResume = () => _service.ResumeJobAsync(job.JobId, "admin");
        await actResume.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*is not on hold*");

        // Complete job
        job.Status = JobStatus.Completed;
        await _db.SaveChangesAsync();

        // Cannot cancel completed job
        var actCancel = () => _service.CancelJobAsync(job.JobId, "cancel", "admin");
        await actCancel.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot cancel a completed job*");

        // Cannot hold completed job
        var actHold = () => _service.HoldJobAsync(job.JobId, "hold", "admin");
        await actHold.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot hold a job in status Completed*");
    }

    // =======================================================================
    // Sales Order & PO No Order Bifurcation Tests
    // =======================================================================

    [Fact]
    public async Task ConfirmAsync_WithSalesOrderAndPONo_PersistsToCreatedJobs()
    {
        await SeedProductsAsync(("LK-6P", true));
        var upload = new BmUpload
        {
            FileName = "test-so.xlsx",
            StoredPath = "dummy.xlsx",
            UploadedBy = "admin",
            UploadedAt = DateTime.UtcNow,
            Status = BmUploadStatus.Previewed,
            RowCount = 1,
            ErrorCount = 0
        };
        upload.Rows.Add(new BmRow
        {
            ExcelRowNo = 2,
            ProductCode = "LK-6P",
            Quantity = 10,
            DueDate = new DateOnly(2026, 12, 1),
            SalesOrder = "1200|UOE004006|20",
            PONo = "S7A021822",
            Priority = PriorityLevel.Red,
            ImportStatus = BmRowImportStatus.Ok
        });
        _db.BmUploads.Add(upload);
        await _db.SaveChangesAsync();

        var result = await _service.ConfirmAsync(upload.UploadId, "admin");
        result.JobsCreated.Should().Be(1);

        var job = await _db.Jobs.FirstAsync(j => j.JobNo == result.JobNumbers[0]);
        job.SalesOrder.Should().Be("1200|UOE004006|20");
        job.PONo.Should().Be("S7A021822");
        job.Quantity.Should().Be(10);
    }

    [Fact]
    public async Task ConfirmAsync_SameItemCode_DifferentSalesOrders_CreatesDistinctJobs()
    {
        await SeedProductsAsync(("LK-6P", true));
        var upload = new BmUpload
        {
            FileName = "multi-orders.xlsx",
            StoredPath = "dummy2.xlsx",
            UploadedBy = "admin",
            UploadedAt = DateTime.UtcNow,
            Status = BmUploadStatus.Previewed,
            RowCount = 2,
            ErrorCount = 0
        };
        // Two rows with same product and same due date, but different Sales Orders and POs
        upload.Rows.Add(new BmRow
        {
            ExcelRowNo = 2,
            ProductCode = "LK-6P",
            Quantity = 8,
            DueDate = new DateOnly(2026, 12, 1),
            SalesOrder = "SO-001",
            PONo = "PO-001",
            Priority = PriorityLevel.Yellow,
            ImportStatus = BmRowImportStatus.Ok
        });
        upload.Rows.Add(new BmRow
        {
            ExcelRowNo = 3,
            ProductCode = "LK-6P",
            Quantity = 12,
            DueDate = new DateOnly(2026, 12, 1),
            SalesOrder = "SO-002",
            PONo = "PO-002",
            Priority = PriorityLevel.Red,
            ImportStatus = BmRowImportStatus.Ok
        });
        _db.BmUploads.Add(upload);
        await _db.SaveChangesAsync();

        var result = await _service.ConfirmAsync(upload.UploadId, "admin");
        result.JobsCreated.Should().Be(2);

        var jobs = await _db.Jobs.Where(j => result.JobNumbers.Contains(j.JobNo)).ToListAsync();
        jobs.Should().HaveCount(2);
        jobs.Should().ContainSingle(j => j.SalesOrder == "SO-001" && j.PONo == "PO-001" && j.Quantity == 8);
        jobs.Should().ContainSingle(j => j.SalesOrder == "SO-002" && j.PONo == "PO-002" && j.Quantity == 12);
    }

    [Fact]
    public async Task GetWorkloadByItemAsync_AggregatesTopLevel_AndCreatesOrderBifurcations()
    {
        await SeedProductsAsync(("46171506DG05623", true));
        var product = await _db.Products.FirstAsync(p => p.ProductCode == "46171506DG05623");

        // Job 1 for Sales Order 1
        _db.Jobs.Add(new Job
        {
            JobNo = "JOB-20261016-0001",
            ProductId = product.ProductId,
            SalesOrder = "1200|UOE004006|20",
            PONo = "S7A021822",
            Quantity = 10,
            QuantityCompleted = 4,
            Priority = PriorityLevel.Red,
            DueDate = new DateOnly(2026, 12, 1),
            Status = JobStatus.InProgress,
            CreatedAt = DateTime.UtcNow
        });

        // Job 2 for Sales Order 2
        _db.Jobs.Add(new Job
        {
            JobNo = "JOB-20261016-0002",
            ProductId = product.ProductId,
            SalesOrder = "1200|UOE004007|30",
            PONo = "S7A021899",
            Quantity = 15,
            QuantityCompleted = 0,
            Priority = PriorityLevel.Black,
            DueDate = new DateOnly(2026, 11, 15),
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var workload = await _service.GetWorkloadByItemAsync();
        workload.Should().HaveCount(1);

        var item = workload[0];
        item.ItemCode.Should().Be("46171506DG05623");
        // Top-level totals across all sales orders
        item.TotalQuantityRequired.Should().Be(25); // 10 + 15
        item.TotalQuantityCompleted.Should().Be(4);  // 4 + 0
        item.TotalQuantityRemaining.Should().Be(21); // 25 - 4
        item.TotalJobs.Should().Be(2);
        item.TotalOrders.Should().Be(2);
        item.HighestPriority.Should().Be(PriorityLevel.Black);

        // Order Bifurcations
        item.OrderBifurcations.Should().HaveCount(2);

        var order1 = item.OrderBifurcations.First(b => b.SalesOrder == "1200|UOE004006|20");
        order1.PONo.Should().Be("S7A021822");
        order1.QuantityRequired.Should().Be(10);
        order1.QuantityCompleted.Should().Be(4);
        order1.QuantityRemaining.Should().Be(6);
        order1.Jobs.Should().HaveCount(1);
        order1.Jobs[0].JobNo.Should().Be("JOB-20261016-0001");

        var order2 = item.OrderBifurcations.First(b => b.SalesOrder == "1200|UOE004007|30");
        order2.PONo.Should().Be("S7A021899");
        order2.QuantityRequired.Should().Be(15);
        order2.QuantityCompleted.Should().Be(0);
        order2.QuantityRemaining.Should().Be(15);
        order2.Jobs.Should().HaveCount(1);
        order2.Jobs[0].JobNo.Should().Be("JOB-20261016-0002");
    }
}