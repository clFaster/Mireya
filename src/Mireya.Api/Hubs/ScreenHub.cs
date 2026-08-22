using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Mireya.Application.Constants;
using Mireya.Application.Hubs;
using Mireya.Application.Services;
using Mireya.Application.Services.Reporting;
using Mireya.Application.Services.ScreenManagement;

namespace Mireya.Api.Hubs;

[Authorize(Roles = Roles.Screen)]
public class ScreenHub(
    ILogger<ScreenHub> logger,
    IScreenConnectionTracker connectionTracker,
    IScreenSynchronizationService screenSyncService,
    IScreenManagementService screenManagementService,
    IPlaybackReportingService playbackReporting
) : Hub<IScreenClient>
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        var connectionId = Context.ConnectionId;

        logger.LogInformation(
            "Screen connected: UserId={UserId}, ConnectionId={ConnectionId}",
            userId,
            connectionId
        );

        if (!string.IsNullOrEmpty(userId))
        {
            connectionTracker.AddConnection(userId, connectionId);
            logger.LogInformation(
                "Registered connection. Online screens: {Count}",
                connectionTracker.GetOnlineScreenCount()
            );

            await screenManagementService.SetScreenActiveAsync(userId, true);

            // Trigger sync when client connects/reconnects
            var bonjour = await screenManagementService.GetBonjourAsync(userId);
            logger.LogInformation(
                "Triggering sync for screen {ScreenIdentifier} on connect",
                bonjour.ScreenIdentifier
            );

            var displayId = await screenSyncService.GetDisplayIdByUserIdAsync(userId);
            if (displayId.HasValue)
                await screenSyncService.SyncScreenAsync(displayId.Value);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        var connectionId = Context.ConnectionId;

        logger.LogInformation(
            exception,
            "Screen disconnected: UserId={UserId}, ConnectionId={ConnectionId}",
            userId,
            connectionId
        );

        connectionTracker.RemoveConnection(connectionId);
        logger.LogInformation(
            "Removed connection. Online screens: {Count}",
            connectionTracker.GetOnlineScreenCount()
        );

        // Only set IsActive=false if this user has no more connections
        if (
            !string.IsNullOrEmpty(userId)
            && !connectionTracker.GetConnectedUserIds().Contains(userId)
        )
        {
            await screenManagementService.SetScreenActiveAsync(userId, false);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    ///     Called by screen clients to report which asset they are currently displaying.
    ///     This enables real-time "now playing" visibility in the admin UI.
    /// </summary>
    public Task ReportNowPlaying(Guid? assetId, string? assetName)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
            return Task.CompletedTask;

        logger.LogDebug(
            "Screen {UserId} now playing: {AssetName} ({AssetId})",
            userId,
            assetName,
            assetId
        );

        connectionTracker.UpdateNowPlaying(userId, assetId, assetName);

        // Persist a proof-of-play record (no-ops when no asset is supplied). Fire-and-forget
        // is avoided so EF's scoped DbContext stays valid for the duration of the write.
        return playbackReporting.RecordAsync(userId, assetId, assetName);
    }
}
