using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Mireya.Api.Constants;
using Mireya.Api.Services;
using Mireya.Api.Services.ScreenManagement;
using Mireya.Database;

namespace Mireya.Api.Hubs;

[Authorize(Roles = Roles.Screen)]
public class ScreenHub(
    ILogger<ScreenHub> logger,
    IScreenConnectionTracker connectionTracker,
    MireyaDbContext db) : Hub<IScreenClient>
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        var connectionId = Context.ConnectionId;
        
        logger.LogInformation("Screen connected: UserId={UserId}, ConnectionId={ConnectionId}", 
            userId, connectionId);
        
        if (!string.IsNullOrEmpty(userId))
        {
            connectionTracker.AddConnection(userId, connectionId);
            logger.LogInformation("Registered connection. Online screens: {Count}", 
                connectionTracker.GetOnlineScreenCount());
            
            // Update IsActive and LastSeenAt in database
            var display = await db.Displays.FirstOrDefaultAsync(d => d.UserId == userId);
            if (display != null)
            {
                display.IsActive = true;
                display.LastSeenAt = DateTime.UtcNow;
                display.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                logger.LogInformation("Updated IsActive=true for screen {DisplayId}", display.Id);
            }
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        var connectionId = Context.ConnectionId;
        
        logger.LogInformation(exception, "Screen disconnected: UserId={UserId}, ConnectionId={ConnectionId}", 
            userId, connectionId);
        
        connectionTracker.RemoveConnection(connectionId);
        logger.LogInformation("Removed connection. Online screens: {Count}", 
            connectionTracker.GetOnlineScreenCount());
        
        // Only set IsActive=false if this user has no more connections
        if (!string.IsNullOrEmpty(userId) && !connectionTracker.GetConnectedUserIds().Contains(userId))
        {
            var display = await db.Displays.FirstOrDefaultAsync(d => d.UserId == userId);
            if (display != null)
            {
                display.IsActive = false;
                display.LastSeenAt = DateTime.UtcNow;
                display.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                logger.LogInformation("Updated IsActive=false for screen {DisplayId}", display.Id);
            }
        }
        
        await base.OnDisconnectedAsync(exception);
    }
}

public interface IScreenClient
{
    Task ReceiveConfigurationUpdate(ScreenConfiguration configuration);
}
