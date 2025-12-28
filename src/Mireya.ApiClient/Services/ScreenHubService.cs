using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mireya.ApiClient.Models;
using Mireya.ApiClient.Options;

namespace Mireya.ApiClient.Services;

public interface IScreenHubService : IAsyncDisposable
{
    bool IsConnected { get; }
    event Action<ScreenConfiguration> OnConfigurationUpdateReceived;
    event Action<List<CampaignSyncInfo>> OnStartAssetSync;
    event Action OnReconnected;

    Task ConnectAsync();
    Task DisconnectAsync();
}

public class ScreenHubService : IScreenHubService
{
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly HubConnection _hubConnection;
    private readonly ILogger<ScreenHubService> _logger;

    public ScreenHubService(
        IOptions<MireyaApiClientOptions> options,
        IAccessTokenProvider accessTokenProvider,
        ILogger<ScreenHubService> logger
    )
    {
        _accessTokenProvider = accessTokenProvider;
        _logger = logger;

        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(
                $"{baseUrl}/hubs/screen",
                options =>
                {
                    options.AccessTokenProvider = () =>
                        Task.FromResult(_accessTokenProvider.GetAccessToken());
                }
            )
            .WithAutomaticReconnect()
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddProvider(new ConsoleLoggerProvider()); // Simple console logging for SignalR internals
            })
            .Build();

        _hubConnection.On<ScreenConfiguration>(
            "ReceiveConfigurationUpdate",
            config =>
            {
                _logger.LogInformation(
                    "Received config: {ScreenName} with {CampaignCount} campaigns",
                    config.ScreenName,
                    config.Campaigns.Count
                );
                OnConfigurationUpdateReceived?.Invoke(config);
            }
        );

        _hubConnection.On<List<CampaignSyncInfo>>(
            "StartAssetSync",
            campaigns =>
            {
                _logger.LogInformation(
                    "Received StartAssetSync for {CampaignCount} campaigns",
                    campaigns.Count
                );
                OnStartAssetSync?.Invoke(campaigns);
            }
        );

        _hubConnection.Closed += error =>
        {
            _logger.LogWarning(error, "SignalR connection closed");
            return Task.CompletedTask;
        };

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "SignalR reconnecting");
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);

            // Trigger sync check on reconnect
            OnReconnected?.Invoke();

            return Task.CompletedTask;
        };
    }

    public event Action<ScreenConfiguration>? OnConfigurationUpdateReceived;
    public event Action<List<CampaignSyncInfo>>? OnStartAssetSync;
    public event Action? OnReconnected;

    public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

    public async Task ConnectAsync()
    {
        if (_hubConnection.State == HubConnectionState.Disconnected)
        {
            _logger.LogInformation("Connecting to SignalR hub");
            await _hubConnection.StartAsync();
            _logger.LogInformation("Connected: {ConnectionId}", _hubConnection.ConnectionId);
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hubConnection.State != HubConnectionState.Disconnected)
        {
            _logger.LogInformation("Disconnecting from SignalR hub");
            await _hubConnection.StopAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _hubConnection.DisposeAsync();
    }

    // Simple logger provider to ensure we see SignalR internal logs in console
    private class ConsoleLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new ConsoleLogger(categoryName);
        }

        public void Dispose() { }
    }

    private class ConsoleLogger(string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            Console.WriteLine($"[{logLevel}] {categoryName}: {formatter(state, exception)}");
        }
    }
}
