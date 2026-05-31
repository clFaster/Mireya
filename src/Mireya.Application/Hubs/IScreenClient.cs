using Mireya.Application.Services.AssetSync;
using Mireya.Application.Services.ScreenManagement;

namespace Mireya.Application.Hubs;

public interface IScreenClient
{
    Task ReceiveConfigurationUpdate(ScreenConfiguration configuration);
    Task StartAssetSync(List<CampaignSyncInfo> campaigns);
    Task ExecuteCommand(string command);
}
