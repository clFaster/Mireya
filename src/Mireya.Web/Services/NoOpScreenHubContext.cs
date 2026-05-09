using Mireya.Application.Hubs;
using Mireya.Application.Services.AssetSync;
using Mireya.Application.Services.ScreenManagement;

namespace Mireya.Web.Services;

/// <summary>
/// No-op hub context for Mireya.Web — screen sync happens via Mireya.Api's SignalR hub.
/// </summary>
public class NoOpScreenHubContext : IScreenHubContext
{
    public Task SendConfigurationUpdateAsync(string userId, ScreenConfiguration config) => Task.CompletedTask;
    public Task StartAssetSyncAsync(string userId, List<CampaignSyncInfo> campaigns) => Task.CompletedTask;
}
