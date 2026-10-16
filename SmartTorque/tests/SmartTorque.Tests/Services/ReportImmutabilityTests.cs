using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;

using Xunit;

using SessionResultEntity = SmartTorque.Core.Entities.SessionResult;

namespace SmartTorque.Tests.Services;

public class ReportImmutabilityTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task SessionResult_ThrowsInvalidOperationException_WhenModified()
    {
        using var db = CreateInMemoryDbContext();

        var sessionResult = new SessionResultEntity
        {
            SessionId = 1,
            StepId = 1,
            InstrumentType = InstrumentType.Torque,
            AttemptNo = 1,
            TargetValue = 20.0m,
            TolerancePlus = 1.0m,
            ToleranceMinus = 1.0m,
            LowerLimit = 19.0m,
            UpperLimit = 21.0m,
            ActualValue = 20.5m,
            Result = StepResult.Pass,
            CapturedAt = DateTime.UtcNow
        };

        // Adding is allowed (append-only)
        db.SessionResults.Add(sessionResult);
        await db.SaveChangesAsync();

        // Attempting to modify an immutable measurement record must throw
        sessionResult.ActualValue = 25.0m;
        sessionResult.Result = StepResult.Fail;

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*immutable*");
    }

    [Fact]
    public async Task SessionResult_ThrowsInvalidOperationException_WhenDeleted()
    {
        using var db = CreateInMemoryDbContext();

        var sessionResult = new SessionResultEntity
        {
            SessionId = 1,
            StepId = 1,
            InstrumentType = InstrumentType.Torque,
            AttemptNo = 1,
            TargetValue = 20.0m,
            TolerancePlus = 1.0m,
            ToleranceMinus = 1.0m,
            LowerLimit = 19.0m,
            UpperLimit = 21.0m,
            ActualValue = 20.5m,
            Result = StepResult.Pass,
            CapturedAt = DateTime.UtcNow
        };

        db.SessionResults.Add(sessionResult);
        await db.SaveChangesAsync();

        // Attempting to delete an immutable measurement record must throw
        db.SessionResults.Remove(sessionResult);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*immutable*");
    }

    [Fact]
    public async Task SessionResult_AllowsAppendOnlyNewAttempts()
    {
        using var db = CreateInMemoryDbContext();

        var attempt1 = new SessionResultEntity
        {
            SessionId = 1,
            StepId = 1,
            InstrumentType = InstrumentType.Torque,
            AttemptNo = 1,
            TargetValue = 20.0m,
            ActualValue = 17.5m,
            Result = StepResult.Fail,
            CapturedAt = DateTime.UtcNow
        };

        db.SessionResults.Add(attempt1);
        await db.SaveChangesAsync();

        // Appending attempt 2 is allowed
        var attempt2 = new SessionResultEntity
        {
            SessionId = 1,
            StepId = 1,
            InstrumentType = InstrumentType.Torque,
            AttemptNo = 2,
            TargetValue = 20.0m,
            ActualValue = 20.2m,
            Result = StepResult.Pass,
            CapturedAt = DateTime.UtcNow
        };

        db.SessionResults.Add(attempt2);
        var saveResult = await db.SaveChangesAsync();

        saveResult.Should().Be(1);
        (await db.SessionResults.CountAsync()).Should().Be(2);
    }
}