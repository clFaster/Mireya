using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mireya.ApiClient.Models;
using Mireya.ApiClient.Options;

namespace Mireya.ApiClient.Services;

public interface IScreenHubService : IAsyncDisposable
{
    event Action<ScreenConfiguration> OnConfigurationUpdateReceived;
    
    Task ConnectAsync();
    Task DisconnectAsync();
    bool IsConnected { get; }
}

public class ScreenHubService : IScreenHubService
{
    private readonly HubConnection _hubConnection;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly ILogger<ScreenHubService> _logger;

    public event Action<ScreenConfiguration>? OnConfigurationUpdateReceived;

    public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

    public ScreenHubService(
        IOptions<MireyaApiClientOptions> options, 
        IAccessTokenProvider accessTokenProvider,
        ILogger<ScreenHubService> logger)
    {
        _accessTokenProvider = accessTokenProvider;
        _logger = logger;
        
        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/screen", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_accessTokenProvider.GetAccessToken());
            })
            .WithAutomaticReconnect()
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddProvider(new ConsoleLoggerProvider()); // Simple console logging for SignalR internals
            })
            .Build();

        _hubConnection.On<ScreenConfiguration>("ReceiveConfigurationUpdate", config =>
        {
            _logger.LogInformation("Received config: {ScreenName} with {CampaignCount} campaigns", 
                config.ScreenName, config.Campaigns.Count);
            OnConfigurationUpdateReceived?.Invoke(config);
        });

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
            return Task.CompletedTask;
        };
    }

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
        public ILogger CreateLogger(string categoryName) => new ConsoleLogger(categoryName);
        public void Dispose() { }
    }
    
    private class ConsoleLogger(string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Console.WriteLine($"[{logLevel}] {categoryName}: {formatter(state, exception)}");
        }
    }
}
