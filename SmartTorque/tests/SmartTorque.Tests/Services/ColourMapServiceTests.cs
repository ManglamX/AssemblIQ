using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

public class ColourMapServiceTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ColourMapRequest Shade(
        string hex = "#000000",
        PriorityLevel priority = PriorityLevel.Black,
        string label = "Black",
        int tolerance = 40)
        => new(hex, priority, label, tolerance);

    [Fact]
    public async Task CreateAsync_StoresHexUppercased_AndIsActive()
    {
        using var db = CreateInMemoryDbContext();
        var service = new ColourMapService(db);

        var entry = await service.CreateAsync(Shade(hex: "#abcdef", label: "Custom"), "admin");

        entry.MapId.Should().BeGreaterThan(0);
        entry.ColourHex.Should().Be("#ABCDEF");
        entry.Priority.Should().Be(PriorityLevel.Black);
        entry.MatchTolerance.Should().Be(40);
        entry.IsActive.Should().BeTrue();

        var saved = await db.PriorityColourMap.FirstAsync(m => m.MapId == entry.MapId);
        saved.ColourHex.Should().Be("#ABCDEF");
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateHex()
    {
        using var db = CreateInMemoryDbContext();
        var service = new ColourMapService(db);

        await service.CreateAsync(Shade("#123456", PriorityLevel.Red, "First"), "admin");

        var act = () => service.CreateAsync(Shade("#123456", PriorityLevel.Green, "Second"), "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("not-a-colour")]
    public async Task CreateAsync_RejectsMalformedHex(string hex)
    {
        using var db = CreateInMemoryDbContext();
        var service = new ColourMapService(db);

        var act = () => service.CreateAsync(Shade(hex), "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*hex value*");
    }

    [Fact]
    public async Task CreateAsync_RejectsOutOfRangePriority()
    {
        using var db = CreateInMemoryDbContext();
        var service = new ColourMapService(db);

        var act = () => service.CreateAsync(Shade(priority: (PriorityLevel)9), "admin");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_RejectsToleranceOutOfRange()
    {
        using var db = CreateInMemoryDbContext();
        var service = new ColourMapService(db);

        var low = () => service.CreateAsync(Shade(tolerance: 0), "admin");
        var high = () => service.CreateAsync(Shade(tolerance: 442), "admin");

        await low.Should().ThrowAsync<InvalidOperationException>();
        await high.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateAsync_ChangesFields_AndNormalisesHex()
    {
        using var db = CreateInMemoryDbContext();
        var service = new ColourMapService(db);

        var created = await service.CreateAsync(Shade("#000000", PriorityLevel.Black, "Black", 40), "admin");

        var updated = await service.UpdateAsync(created.MapId, Shade("#c0ffee", PriorityLevel.Red, "Custom", 12), "editor");

        updated.MapId.Should().Be(created.MapId);
        updated.ColourHex.Should().Be("#C0FFEE");
        updated.Priority.Should().Be(PriorityLevel.Red);
        updated.Label.Should().Be("Custom");
        updated.MatchTolerance.Should().Be(12);
        updated.UpdatedBy.Should().Be("editor");
        updated.CreatedBy.Should().Be("admin");
    }

    [Fact]
    public async Task UpdateAsync_RejectsHexUsedByAnotherShade()
    {
        using var db = CreateInMemoryDbContext();
        var service = new ColourMapService(db);

        await service.CreateAsync(Shade("#000000", PriorityLevel.Black, "Black"), "admin");
        var second = await service.CreateAsync(Shade("#FF0000", PriorityLevel.Red, "Red"), "admin");

        var act = () => service.UpdateAsync(second.MapId, Shade("#000000"), "editor");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateAsync_KeepsItsOwnHex_WhenSavingUnchanged()
    {
        using var db = CreateInMemoryDbContext();
        var service = new ColourMapService(db);

        var created = await service.CreateAsync(Shade("#000000", PriorityLevel.Black, "Black"), "admin");

        var act = () => service.UpdateAsync(created.MapId, Shade("#000000", PriorityLevel.Black, "Black Renamed"), "editor");

        await act.Should().NotThrowAsync();
        var refreshed = await db.PriorityColourMap.FirstAsync(m => m.MapId == created.MapId);
        refreshed.Label.Should().Be("Black Renamed");
    }

    [Fact]
    public async Task SetActiveAsync_Deactivates_AndRestores()
    {
        using var db = CreateInMemoryDbContext();
        var service = new ColourMapService(db);

        await service.CreateAsync(Shade("#000000", PriorityLevel.Black, "Black"), "admin");
        var second = await service.CreateAsync(Shade("#FF0000", PriorityLevel.Red, "Red"), "admin");

        await service.SetActiveAsync(second.MapId, false, "admin");
        (await db.PriorityColourMap.FirstAsync(m => m.MapId == second.MapId)).IsActive.Should().BeFalse();

        await service.SetActiveAsync(second.MapId, true, "admin");
        (await db.PriorityColourMap.FirstAsync(m => m.MapId == second.MapId)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SetActiveAsync_KeepsAtLeastOneActiveShade()
    {
        using var db = CreateInMemoryDbContext();
        var service = new ColourMapService(db);

        var only = await service.CreateAsync(Shade("#000000", PriorityLevel.Black, "Black"), "admin");

        var act = () => service.SetActiveAsync(only.MapId, false, "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*At least one active shade*");
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveShades_OrderedByPriority()
    {
        using var db = CreateInMemoryDbContext();
        var service = new ColourMapService(db);

        await service.CreateAsync(Shade("#FFFFFF", PriorityLevel.White, "White"), "admin");
        await service.CreateAsync(Shade("#000000", PriorityLevel.Black, "Black"), "admin");
        var red = await service.CreateAsync(Shade("#FF0000", PriorityLevel.Red, "Red"), "admin");
        await service.SetActiveAsync(red.MapId, false, "admin");

        var active = await service.GetActiveAsync();
        var all = await service.ListAsync(includeInactive: true);

        active.Select(m => m.Label).Should().Equal("Black", "White");
        all.Should().HaveCount(3);
        (await service.ListAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_ToleranceDefaultsToForty_AndSeedPaletteIsAccepted()
    {
        using var db = CreateInMemoryDbContext();
        var service = new ColourMapService(db);

        // The seeded palette (Architecture §5.3) must all be creatable.
        var palette = new (string Hex, PriorityLevel Priority, string Label)[]
        {
            ("#000000", PriorityLevel.Black, "Black"),
            ("#FF0000", PriorityLevel.Red, "Red"),
            ("#FFFF00", PriorityLevel.Yellow, "Yellow"),
            ("#00B050", PriorityLevel.Green, "Green"),
            ("#FFFFFF", PriorityLevel.White, "White"),
        };

        foreach (var (hex, priority, label) in palette)
            await service.CreateAsync(new ColourMapRequest(hex, priority, label, 40), "admin");

        (await service.GetActiveAsync()).Should().HaveCount(5);
    }
}