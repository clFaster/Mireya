using Microsoft.AspNetCore.SignalR;
using Mireya.Application.Hubs;
using Mireya.Application.Services.AssetSync;
using Mireya.Application.Services.ScreenManagement;

namespace Mireya.Api.Hubs;

public class ScreenHubContextAdapter(IHubContext<ScreenHub, IScreenClient> hubContext)
    : IScreenHubContext
{
    public async Task SendConfigurationUpdateAsync(string userId, ScreenConfiguration config) =>
        await hubContext.Clients.User(userId).ReceiveConfigurationUpdate(config);

    public async Task StartAssetSyncAsync(string userId, List<CampaignSyncInfo> campaigns) =>
        await hubContext.Clients.User(userId).StartAssetSync(campaigns);

    public async Task SendCommandAsync(string userId, string command) =>
        await hubContext.Clients.User(userId).ExecuteCommand(command);
}
