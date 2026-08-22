using Mireya.Application.Services.Alerting;
using Mireya.Database.Models;
using NSubstitute;

namespace Mireya.Application.Tests;

public class ScreenOfflineMonitorTests
{
    private static Display AddDisplay(
        TestDatabase db,
        ApprovalStatus status,
        bool isActive,
        DateTime? lastSeenAt,
        DateTime? offlineAlertedAt = null
    )
    {
        var display = new Display
        {
            Name = "Screen",
            Location = "Lobby",
            ScreenIdentifier = Guid.NewGuid().ToString("N")[..10],
            ApprovalStatus = status,
            IsActive = isActive,
            LastSeenAt = lastSeenAt,
            OfflineAlertedAt = offlineAlertedAt,
        };
        db.Context.Displays.Add(display);
        db.Context.SaveChanges();
        return display;
    }

    [Fact]
    public async Task EvaluateOnceAsync_AlertsWhenOfflineBeyondThreshold()
    {
        using var db = new TestDatabase();
        var now = DateTime.UtcNow;
        var display = AddDisplay(
            db,
            ApprovalStatus.Approved,
            isActive: false,
            lastSeenAt: now.AddMinutes(-10)
        );
        var alerts = Substitute.For<IScreenAlertService>();

        var sent = await ScreenOfflineMonitorService.EvaluateOnceAsync(
            db.Context,
            alerts,
            thresholdMinutes: 5,
            now
        );

        Assert.Equal(1, sent);
        await alerts
            .Received(1)
            .SendAsync(
                ScreenAlertKind.Offline,
                Arg.Is<Display>(d => d.Id == display.Id),
                Arg.Any<CancellationToken>()
            );
        Assert.NotNull((await db.NewContext().Displays.FindAsync(display.Id))!.OfflineAlertedAt);
    }

    [Fact]
    public async Task EvaluateOnceAsync_DoesNotAlertBeforeThreshold()
    {
        using var db = new TestDatabase();
        var now = DateTime.UtcNow;
        AddDisplay(db, ApprovalStatus.Approved, isActive: false, lastSeenAt: now.AddMinutes(-2));
        var alerts = Substitute.For<IScreenAlertService>();

        var sent = await ScreenOfflineMonitorService.EvaluateOnceAsync(
            db.Context,
            alerts,
            thresholdMinutes: 5,
            now
        );

        Assert.Equal(0, sent);
        await alerts
            .DidNotReceive()
            .SendAsync(
                Arg.Any<ScreenAlertKind>(),
                Arg.Any<Display>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task EvaluateOnceAsync_DoesNotAlertTwiceForSameOutage()
    {
        using var db = new TestDatabase();
        var now = DateTime.UtcNow;
        AddDisplay(
            db,
            ApprovalStatus.Approved,
            isActive: false,
            lastSeenAt: now.AddMinutes(-10),
            offlineAlertedAt: now.AddMinutes(-3)
        );
        var alerts = Substitute.For<IScreenAlertService>();

        var sent = await ScreenOfflineMonitorService.EvaluateOnceAsync(
            db.Context,
            alerts,
            thresholdMinutes: 5,
            now
        );

        Assert.Equal(0, sent);
        await alerts
            .DidNotReceive()
            .SendAsync(
                Arg.Any<ScreenAlertKind>(),
                Arg.Any<Display>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task EvaluateOnceAsync_SendsRecoveryAndClearsFlagWhenBackOnline()
    {
        using var db = new TestDatabase();
        var now = DateTime.UtcNow;
        var display = AddDisplay(
            db,
            ApprovalStatus.Approved,
            isActive: true,
            lastSeenAt: now,
            offlineAlertedAt: now.AddMinutes(-20)
        );
        var alerts = Substitute.For<IScreenAlertService>();

        var sent = await ScreenOfflineMonitorService.EvaluateOnceAsync(
            db.Context,
            alerts,
            thresholdMinutes: 5,
            now
        );

        Assert.Equal(1, sent);
        await alerts
            .Received(1)
            .SendAsync(
                ScreenAlertKind.Online,
                Arg.Is<Display>(d => d.Id == display.Id),
                Arg.Any<CancellationToken>()
            );
        Assert.Null((await db.NewContext().Displays.FindAsync(display.Id))!.OfflineAlertedAt);
    }

    [Fact]
    public async Task EvaluateOnceAsync_IgnoresUnapprovedScreens()
    {
        using var db = new TestDatabase();
        var now = DateTime.UtcNow;
        AddDisplay(db, ApprovalStatus.Pending, isActive: false, lastSeenAt: now.AddMinutes(-30));
        var alerts = Substitute.For<IScreenAlertService>();

        var sent = await ScreenOfflineMonitorService.EvaluateOnceAsync(
            db.Context,
            alerts,
            thresholdMinutes: 5,
            now
        );

        Assert.Equal(0, sent);
    }
}
