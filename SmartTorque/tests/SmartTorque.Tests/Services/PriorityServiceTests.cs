using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

/// <summary>Tests for <see cref="PriorityService"/> (P4.7 — reusable ordering for the workload board).</summary>
public class PriorityServiceTests
{
    private readonly AppDbContext _db;
    private readonly PriorityService _service;

    public PriorityServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _service = new PriorityService();
    }

    // DbContext disposal is handled by the test framework; no explicit Dispose needed.

    [Fact]
    public void OrderForWorkboard_OrdersByPriorityThenDueDateThenUpload()
    {
        // Arrange: 4 jobs with different priorities/dates
        _db.Jobs.AddRange(
            new Job { Priority = PriorityLevel.Red, DueDate = new DateOnly(2026, 11, 10), SourceBmRowId = 10 },
            new Job { Priority = PriorityLevel.Red, DueDate = new DateOnly(2026, 11, 5), SourceBmRowId = 5 },
            new Job { Priority = PriorityLevel.Green, DueDate = new DateOnly(2026, 11, 5), SourceBmRowId = 3 },
            new Job { Priority = PriorityLevel.Black, DueDate = new DateOnly(2026, 11, 5), SourceBmRowId = 1 }
        );
        _db.SaveChanges();

        // Act
        var ordered = _service.OrderForWorkboard(_db.Jobs.AsNoTracking()).ToList();

        // Assert: Black first, then Red (earlier due date), then Green
        ordered[0].Priority.Should().Be(PriorityLevel.Black);   // #1
        ordered[1].Priority.Should().Be(PriorityLevel.Red);     // #2 (Nov 5)
        ordered[2].Priority.Should().Be(PriorityLevel.Red);     // #3 (Nov 10)
        ordered[3].Priority.Should().Be(PriorityLevel.Green);   // #4

        // Within Red, sorted by DueDate (5th before 10th)
        ordered[1].DueDate.Should().Be(new DateOnly(2026, 11, 5));
        ordered[2].DueDate.Should().Be(new DateOnly(2026, 11, 10));
    }

    [Fact]
    public void OrderForWorkboard_StableOrdering_WhenPrioritiesAndDatesMatch()
    {
        // Arrange: same priority and same due date → order by SourceBmRowId
        _db.Jobs.AddRange(
            new Job { Priority = PriorityLevel.Red, DueDate = new DateOnly(2026, 11, 5), SourceBmRowId = 20 },
            new Job { Priority = PriorityLevel.Red, DueDate = new DateOnly(2026, 11, 5), SourceBmRowId = 10 },
            new Job { Priority = PriorityLevel.Red, DueDate = new DateOnly(2026, 11, 5), SourceBmRowId = 30 }
        );
        _db.SaveChanges();

        // Act
        var ordered = _service.OrderForWorkboard(_db.Jobs.AsNoTracking()).ToList();

        // Assert: ordered by SourceBmRowId (ascending)
        ordered[0].SourceBmRowId.Should().Be(10);
        ordered[1].SourceBmRowId.Should().Be(20);
        ordered[2].SourceBmRowId.Should().Be(30);
    }

    [Fact]
    public void OrderForWorkboard_NullsLastForDueDate()
    {
        // Arrange: one job with null DueDate
        _db.Jobs.AddRange(
            new Job { Priority = PriorityLevel.Red, DueDate = null, SourceBmRowId = 1 },
            new Job { Priority = PriorityLevel.Red, DueDate = new DateOnly(2026, 11, 5), SourceBmRowId = 2 }
        );
        _db.SaveChanges();

        // Act
        var ordered = _service.OrderForWorkboard(_db.Jobs.AsNoTracking()).ToList();

        // Assert: job with null DueDate comes after the one with a date
        ordered[0].DueDate.Should().Be(new DateOnly(2026, 11, 5));
        ordered[1].DueDate.Should().BeNull();
    }

    [Fact]
    public void OrderForWorkboard_HandlesEmptyQuery()
    {
        // Act
        var ordered = _service.OrderForWorkboard(_db.Jobs.AsNoTracking()).ToList();

        // Assert
        ordered.Should().BeEmpty();
    }
}