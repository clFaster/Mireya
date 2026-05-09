using Mireya.Application.Services.AssetSync;
using Mireya.Application.Services.ScreenManagement;

namespace Mireya.Application.Hubs;

public interface IScreenHubContext
{
    Task SendConfigurationUpdateAsync(string userId, ScreenConfiguration config);
    Task StartAssetSyncAsync(string userId, List<CampaignSyncInfo> campaigns);
}
