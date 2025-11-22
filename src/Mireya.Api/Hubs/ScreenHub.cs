using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Mireya.Api.Constants;
using Mireya.Api.Services.ScreenManagement;

namespace Mireya.Api.Hubs;

[Authorize(Roles = Roles.Screen)]
public class ScreenHub(ILogger<ScreenHub> logger) : Hub<IScreenClient>
{
    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Screen connected: UserId={UserId}, ConnectionId={ConnectionId}", 
            Context.UserIdentifier, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation(exception, "Screen disconnected: UserId={UserId}", Context.UserIdentifier);
        await base.OnDisconnectedAsync(exception);
    }
}

public interface IScreenClient
{
    Task ReceiveConfigurationUpdate(ScreenConfiguration configuration);
}
