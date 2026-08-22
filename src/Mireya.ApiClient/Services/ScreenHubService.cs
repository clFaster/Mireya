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
    event Action<string> OnCommandReceived;
    event Action OnReconnected;
    event Action OnReconnecting;
    event Action OnClosed;

    Task ConnectAsync();
    Task DisconnectAsync();

    /// <summary>
    ///     Report the currently displaying asset to the server for real-time admin visibility
    /// </summary>
    Task ReportNowPlayingAsync(Guid? assetId, string? assetName);
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
            .WithAutomaticReconnect(new BackoffRetryPolicy())
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

        _hubConnection.On<string>(
            "ExecuteCommand",
            command =>
            {
                _logger.LogInformation("Received remote command: {Command}", command);
                OnCommandReceived?.Invoke(command);
            }
        );

        _hubConnection.Closed += error =>
        {
            _logger.LogWarning(error, "SignalR connection closed");
            OnClosed?.Invoke();
            return Task.CompletedTask;
        };

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "SignalR reconnecting");
            OnReconnecting?.Invoke();
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
    public event Action<string>? OnCommandReceived;
    public event Action? OnReconnected;
    public event Action? OnReconnecting;
    public event Action? OnClosed;

    public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

    public async Task ConnectAsync()
    {
        if (_hubConnection.State != HubConnectionState.Disconnected)
            return;

        // The screen may boot before the server is reachable (e.g. a Raspberry Pi
        // powering on with the server), so retry the initial connection with a
        // capped exponential backoff before surfacing a failure to the UI.
        const int maxAttempts = 6;
        Exception lastError = new InvalidOperationException(
            "No SignalR connection attempt was made."
        );
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (attempt > 0)
            {
                var delay = BackoffRetryPolicy.GetDelay(attempt);
                _logger.LogWarning(
                    lastError,
                    "Initial SignalR connection attempt {Attempt} failed, retrying in {Delay}s",
                    attempt,
                    delay.TotalSeconds
                );
                await Task.Delay(delay);
            }

            try
            {
                _logger.LogInformation("Connecting to SignalR hub");
                await _hubConnection.StartAsync();
                _logger.LogInformation("Connected: {ConnectionId}", _hubConnection.ConnectionId);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        _logger.LogError(
            lastError,
            "Failed to connect to SignalR hub after {Attempts} attempts",
            maxAttempts
        );
        throw lastError;
    }

    public async Task DisconnectAsync()
    {
        if (_hubConnection.State != HubConnectionState.Disconnected)
        {
            _logger.LogInformation("Disconnecting from SignalR hub");
            await _hubConnection.StopAsync();
        }
    }

    public async Task ReportNowPlayingAsync(Guid? assetId, string? assetName)
    {
        if (_hubConnection.State != HubConnectionState.Connected)
            return;

        try
        {
            await _hubConnection.InvokeAsync("ReportNowPlaying", assetId, assetName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report now-playing to server");
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_hubConnection.State != HubConnectionState.Disconnected)
            {
                _logger.LogInformation("Stopping SignalR connection during dispose");
                await _hubConnection.StopAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping SignalR connection during dispose");
        }

        await _hubConnection.DisposeAsync();
    }

    // Simple logger provider to ensure we see SignalR internal logs in console
    private sealed class ConsoleLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new ConsoleLogger(categoryName);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    private sealed class ConsoleLogger(string categoryName) : ILogger
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

    /// <summary>
    ///     Reconnect policy with a capped exponential backoff that retries indefinitely, so an
    ///     unattended screen recovers on its own after an extended server outage instead of giving
    ///     up after the default 30 seconds.
    /// </summary>
    internal sealed class BackoffRetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            return GetDelay((int)retryContext.PreviousRetryCount + 1);
        }

        /// <summary>
        ///     Capped exponential backoff: ~2s, 4s, 8s, 16s, 32s, then 60s for all subsequent attempts.
        /// </summary>
        public static TimeSpan GetDelay(int attempt)
        {
            if (attempt < 1)
                return TimeSpan.Zero;

            const double maxSeconds = 60;
            var seconds = Math.Min(maxSeconds, Math.Pow(2, attempt));
            return TimeSpan.FromSeconds(seconds);
        }
    }
}
