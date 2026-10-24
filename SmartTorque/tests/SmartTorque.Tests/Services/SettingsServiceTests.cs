using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Interfaces;
using SmartTorque.Data;
using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

public class SettingsServiceTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedSettingsAsync(AppDbContext db, params (string Key, string Value)[] rows)
    {
        foreach (var (key, value) in rows)
        {
            db.Settings.Add(new Setting { Key = key, Value = value });
        }

        await db.SaveChangesAsync();
    }

    private const string DefaultHeaderMapJson =
        @"{ ""ProductCode"": ""Item Code"", ""Quantity"": ""Qty"", ""DueDate"": ""Due Date"", ""Remarks"": ""Remarks"", ""SalesOrder"": ""Sales Order"", ""PONo"": ""PO No"" }";

    // ================================================================
    // READS
    // ================================================================

    [Fact]
    public async Task GetAsync_ReturnsStoredValue_AndNullWhenMissing()
    {
        using var db = CreateInMemoryDbContext();
        await SeedSettingsAsync(db, ("DeviceOfflineSeconds", "45"));

        var service = new SettingsService(db);

        var value = await service.GetAsync("DeviceOfflineSeconds");
        var missing = await service.GetAsync("Nope");

        value.Should().Be("45");
        missing.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_Typed_ParsesAndReturnsNullWhenMissing()
    {
        using var db = CreateInMemoryDbContext();
        await SeedSettingsAsync(
            db,
            ("DeviceOfflineSeconds", "45"),
            ("RequireManualEntryApproval", "true"),
            ("StableHoldMs", "800"));

        var service = new SettingsService(db);

        var seconds = await service.GetAsync<int>("DeviceOfflineSeconds");
        var approval = await service.GetAsync<bool>("RequireManualEntryApproval");
        var holdMs = await service.GetAsync<decimal>("StableHoldMs");
        var missing = await service.GetAsync<int>("Nope");

        seconds.Should().Be(45);
        approval.Should().BeTrue();
        holdMs.Should().Be(800m);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_Typed_InvalidStoredValue_Throws()
    {
        using var db = CreateInMemoryDbContext();
        await SeedSettingsAsync(db, ("DeviceOfflineSeconds", "not-a-number"));

        var service = new SettingsService(db);

        var act = async () => await service.GetAsync<int>("DeviceOfflineSeconds");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*DeviceOfflineSeconds*not-a-number*");
    }

    [Fact]
    public async Task GetOverviewAsync_ReturnsOnlyRequestedRows_WithMeta()
    {
        using var db = CreateInMemoryDbContext();
        await SeedSettingsAsync(db, ("StableHoldMs", "800"), ("ImageMaxMb", "5"));

        var service = new SettingsService(db);

        var overview = await service.GetOverviewAsync(
            ["StableHoldMs", "ImageMaxMb", "SessionIdleTimeoutMin"]);

        overview.Should().HaveCount(2);
        var hold = overview.Single(o => o.Key == "StableHoldMs");
        hold.Value.Should().Be("800");
        overview.Should().NotContain(o => o.Key == "SessionIdleTimeoutMin");
    }

    // ================================================================
    // SAVES
    // ================================================================

    [Fact]
    public async Task SaveApplicationSettingsAsync_UpdatesChangedKeys_AndWritesOneAuditRow()
    {
        using var db = CreateInMemoryDbContext();
        await SeedSettingsAsync(
            db,
            ("DeviceOfflineSeconds", "30"),
            ("ImageMaxMb", "5"),
            ("MaxRetriesDefault", "3"));

        var service = new SettingsService(db);

        var updated = await service.SaveApplicationSettingsAsync(
            new ApplicationSettingsSaveRequest(45, 5, 3, DefaultHeaderMapJson),
            "admin@factory.com");

        updated.Should().Be(2); // device + header map; image/retries unchanged

        var device = await db.Settings.SingleAsync(s => s.Key == SettingsService.KeyDeviceOfflineSeconds);
        device.Value.Should().Be("45");
        device.UpdatedBy.Should().Be("admin@factory.com");
        device.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // Setting rows untouched by the save must keep their original UpdatedBy.
        var images = await db.Settings.SingleAsync(s => s.Key == SettingsService.KeyImageMaxMb);
        images.Value.Should().Be("5");
        images.UpdatedBy.Should().BeNull();

        var audit = db.AuditLogs.Should().ContainSingle().Which;
        audit.EntityName.Should().Be(nameof(Setting));
        audit.Action.Should().Be("Updated");
        audit.UserId.Should().Be("admin@factory.com");
        audit.Changes.Should().NotBeNull();
        audit.Changes.Should().Contain("DeviceOfflineSeconds").And.Contain("30").And.Contain("45");
        audit.Changes.Should().Contain("BmHeaderMap").And.Contain("(default)").And.Contain("Item Code");
    }

    [Fact]
    public async Task SaveApplicationSettingsAsync_WhenNothingChanged_WritesNothing()
    {
        using var db = CreateInMemoryDbContext();
        await SeedSettingsAsync(
            db,
            ("DeviceOfflineSeconds", "30"),
            ("ImageMaxMb", "5"),
            ("MaxRetriesDefault", "3"));

        var service = new SettingsService(db);

        var updated = await service.SaveApplicationSettingsAsync(
            new ApplicationSettingsSaveRequest(30, 5, 3, null),
            "admin@factory.com");

        updated.Should().Be(0);
        db.AuditLogs.Should().BeEmpty();
        // No header-map row existed and none was written
        db.Settings.Should().HaveCount(3);
    }

    [Fact]
    public async Task SaveApplicationSettingsAsync_InsertsMissingRows_WithDescriptions()
    {
        using var db = CreateInMemoryDbContext(); // no rows at all

        var service = new SettingsService(db);

        var updated = await service.SaveApplicationSettingsAsync(
            new ApplicationSettingsSaveRequest(60, 8, 4, DefaultHeaderMapJson),
            "admin@factory.com");

        updated.Should().Be(4);

        var device = await db.Settings.SingleAsync(s => s.Key == SettingsService.KeyDeviceOfflineSeconds);
        device.Value.Should().Be("60");
        device.Description.Should().NotBeNullOrWhiteSpace();

        var headerMap = await db.Settings.SingleAsync(s => s.Key == SettingsService.KeyBmHeaderMap);
        headerMap.Value.Should().Be(DefaultHeaderMapJson);

        var changeJson = (await db.AuditLogs.SingleAsync()).Changes;
        changeJson.Should().Contain("(default)"); // inserted rows record "(default)" → new value
    }

    [Theory]
    [InlineData(4, 5, 3)] // device below range
    [InlineData(700, 5, 3)] // device above range
    [InlineData(30, 0, 3)] // image limit below range
    [InlineData(30, 21, 3)] // image limit above range
    [InlineData(30, 5, 0)] // retries below range
    [InlineData(30, 5, 11)] // retries above range
    public async Task SaveApplicationSettingsAsync_RejectsOutOfRange_AndPersistsNothing(
        int deviceSeconds, int imageMaxMb, int maxRetries)
    {
        using var db = CreateInMemoryDbContext();
        await SeedSettingsAsync(db, ("DeviceOfflineSeconds", "30"));

        var service = new SettingsService(db);

        var act = async () => await service.SaveApplicationSettingsAsync(
            new ApplicationSettingsSaveRequest(deviceSeconds, imageMaxMb, maxRetries, null),
            "admin@factory.com");

        await act.Should().ThrowAsync<InvalidOperationException>();
        db.Settings.Should().ContainSingle(); // untouched
        db.AuditLogs.Should().BeEmpty();
    }

    [Theory]
    [InlineData("not json at all")] // invalid JSON
    [InlineData("[]")] // not an object
    [InlineData(@"{ ""ProductCode"": """", ""Quantity"": ""Qty"", ""DueDate"": ""Due Date"", ""Remarks"": ""R"", ""SalesOrder"": ""S"", ""PONo"": ""P"" }")] // blank key
    public async Task SaveApplicationSettingsAsync_RejectsInvalidHeaderMap_AndPersistsNothing(string json)
    {
        using var db = CreateInMemoryDbContext();
        await SeedSettingsAsync(db, ("DeviceOfflineSeconds", "30"));

        var service = new SettingsService(db);

        var act = async () => await service.SaveApplicationSettingsAsync(
            new ApplicationSettingsSaveRequest(30, 5, 3, json),
            "admin@factory.com");

        await act.Should().ThrowAsync<InvalidOperationException>();
        db.Settings.Should().ContainSingle();
        db.AuditLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveApplicationSettingsAsync_PartialHeaderMap_IsAccepted_DefaultsFillMissingKeys()
    {
        using var db = CreateInMemoryDbContext();
        await SeedSettingsAsync(
            db,
            ("DeviceOfflineSeconds", "30"),
            ("ImageMaxMb", "5"),
            ("MaxRetriesDefault", "3"));

        var service = new SettingsService(db);

        var updated = await service.SaveApplicationSettingsAsync(
            new ApplicationSettingsSaveRequest(30, 5, 3, @"{ ""ProductCode"": ""Item Code"" }"),
            "admin@factory.com");

        updated.Should().Be(1); // only the header map changes
        (await db.Settings.SingleAsync(s => s.Key == SettingsService.KeyBmHeaderMap))
            .Value.Should().Contain("Item Code");
    }

    [Fact]
    public async Task SaveApplicationSettingsAsync_EmptyHeaderMap_RemovesStoredRow()
    {
        using var db = CreateInMemoryDbContext();
        await SeedSettingsAsync(
            db,
            ("DeviceOfflineSeconds", "30"),
            ("ImageMaxMb", "5"),
            ("MaxRetriesDefault", "3"),
            ("BmHeaderMap", DefaultHeaderMapJson));

        var service = new SettingsService(db);

        var updated = await service.SaveApplicationSettingsAsync(
            new ApplicationSettingsSaveRequest(30, 5, 3, "   "),
            "admin@factory.com");

        updated.Should().Be(1); // only the removal
        db.Settings.Should().NotContain(s => s.Key == SettingsService.KeyBmHeaderMap);
        db.Settings.Should().HaveCount(3); // numeric rows unchanged by the blank header map
        var audit = db.AuditLogs.Should().ContainSingle().Which;
        audit.Changes.Should().Contain("(default)");
    }

    [Fact]
    public async Task SaveApplicationSettingsAsync_ReservedKeysOutsideRequest_AreNotTouched()
    {
        using var db = CreateInMemoryDbContext();
        await SeedSettingsAsync(
            db,
            ("DeviceOfflineSeconds", "30"),
            ("ImageMaxMb", "5"),
            ("MaxRetriesDefault", "3"),
            ("StableHoldMs", "800"),
            ("SessionIdleTimeoutMin", "60"),
            ("BmColourMatchTolerance", "40"),
            ("RequireManualEntryApproval", "true"));

        var service = new SettingsService(db);

        var updated = await service.SaveApplicationSettingsAsync(
            new ApplicationSettingsSaveRequest(45, 5, 3, null),
            "admin@factory.com");

        updated.Should().Be(1);
        (await db.Settings.SingleAsync(s => s.Key == "DeviceOfflineSeconds")).Value.Should().Be("45");
        (await db.Settings.SingleAsync(s => s.Key == "StableHoldMs")).Value.Should().Be("800");
        (await db.Settings.SingleAsync(s => s.Key == "RequireManualEntryApproval")).Value.Should().Be("true");
    }

    [Fact]
    public async Task SetAsync_CreatesOrUpdatesRows()
    {
        using var db = CreateInMemoryDbContext();

        var service = new SettingsService(db);

        await service.SetAsync("NewKey", "v1", "admin@factory.com");
        await service.SetAsync("NewKey", "v2", "admin@factory.com");
        await service.SetAsync("NewKey", "v2", "admin@factory.com"); // unchanged → no extra write

        var row = await db.Settings.SingleAsync(s => s.Key == "NewKey");
        row.Value.Should().Be("v2");
        row.UpdatedBy.Should().Be("admin@factory.com");
        db.Settings.Should().ContainSingle();
    }
}