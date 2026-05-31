namespace Mireya.Application.Services;

/// <summary>
///     Represents the real-time state of a connected screen
/// </summary>
public record ScreenState(
    string UserId,
    bool IsOnline,
    Guid? CurrentAssetId = null,
    string? CurrentAssetName = null,
    DateTime? ConnectedAt = null
);

/// <summary>
///     Event raised when any screen's real-time state changes
/// </summary>
public record ScreenStateChangedEvent(string UserId, ScreenState State);

/// <summary>
///     Service to track which screens are currently connected via SignalR
///     and their real-time display state (now-playing)
/// </summary>
public interface IScreenConnectionTracker
{
    /// <summary>
    ///     Register a screen as connected
    /// </summary>
    void AddConnection(string userId, string connectionId);

    /// <summary>
    ///     Remove a screen connection
    /// </summary>
    void RemoveConnection(string connectionId);

    /// <summary>
    ///     Get the count of currently online screens
    /// </summary>
    int GetOnlineScreenCount();

    /// <summary>
    ///     Get all currently connected user IDs
    /// </summary>
    IEnumerable<string> GetConnectedUserIds();

    /// <summary>
    ///     Update the currently playing asset for a screen
    /// </summary>
    void UpdateNowPlaying(string userId, Guid? assetId, string? assetName);

    /// <summary>
    ///     Get the real-time state of all connected screens
    /// </summary>
    IReadOnlyList<ScreenState> GetAllScreenStates();

    /// <summary>
    ///     Get the real-time state of a specific screen by user ID
    /// </summary>
    ScreenState? GetScreenState(string userId);

    /// <summary>
    ///     Event raised whenever a screen's state changes (online/offline, now-playing)
    ///     Subscribers (e.g. Blazor components) must handle thread safety themselves.
    /// </summary>
    event Action<ScreenStateChangedEvent>? OnScreenStateChanged;
}

public class ScreenConnectionTracker(ILogger<ScreenConnectionTracker> logger) : IScreenConnectionTracker
{
    // Maps ConnectionId -> UserId
    private readonly Dictionary<string, string> _connectionToUser = new();

    private readonly Lock _lock = new();

    // Maps UserId -> HashSet of ConnectionIds (a user/screen might have multiple connections)
    private readonly Dictionary<string, HashSet<string>> _userToConnections = new();

    // Maps UserId -> current screen state (now-playing info)
    private readonly Dictionary<string, ScreenState> _screenStates = new();

    public event Action<ScreenStateChangedEvent>? OnScreenStateChanged;

    public void AddConnection(string userId, string connectionId)
    {
        ScreenState? newState;

        lock (_lock)
        {
            _connectionToUser[connectionId] = userId;

            if (!_userToConnections.TryGetValue(userId, out var connections))
            {
                connections = [];
                _userToConnections[userId] = connections;
            }

            connections.Add(connectionId);

            // Create or update screen state
            if (_screenStates.TryGetValue(userId, out var existing))
            {
                // Preserve now-playing info, mark as online
                newState = existing with { IsOnline = true };
            }
            else
            {
                newState = new ScreenState(userId, IsOnline: true, ConnectedAt: DateTime.UtcNow);
            }

            _screenStates[userId] = newState;
        }

        // Raise event outside lock to avoid deadlocks
        RaiseStateChanged(userId, newState);
    }

    public void RemoveConnection(string connectionId)
    {
        ScreenState? newState = null;
        string? userId = null;

        lock (_lock)
        {
            if (_connectionToUser.Remove(connectionId, out userId) && _userToConnections.TryGetValue(userId, out var connections))
            {
                connections.Remove(connectionId);

                if (connections.Count == 0)
                {
                    _userToConnections.Remove(userId);

                    // Mark screen as offline, clear now-playing
                    newState = new ScreenState(userId, IsOnline: false);
                    _screenStates[userId] = newState;
                }
            }
        }

        // Only raise event if the screen went fully offline
        if (userId != null && newState != null)
        {
            RaiseStateChanged(userId, newState);
        }
    }

    public int GetOnlineScreenCount()
    {
        lock (_lock)
        {
            return _userToConnections.Count;
        }
    }

    public IEnumerable<string> GetConnectedUserIds()
    {
        lock (_lock)
        {
            return _userToConnections.Keys.ToList();
        }
    }

    public void UpdateNowPlaying(string userId, Guid? assetId, string? assetName)
    {
        ScreenState? newState;

        lock (_lock)
        {
            if (!_screenStates.TryGetValue(userId, out var existing))
            {
                // Screen not tracked (shouldn't happen), create a state
                existing = new ScreenState(userId, IsOnline: true, ConnectedAt: DateTime.UtcNow);
            }

            newState = existing with { CurrentAssetId = assetId, CurrentAssetName = assetName };
            _screenStates[userId] = newState;
        }

        RaiseStateChanged(userId, newState);
    }

    public IReadOnlyList<ScreenState> GetAllScreenStates()
    {
        lock (_lock)
        {
            return _screenStates.Values
                .Where(s => s.IsOnline)
                .ToList();
        }
    }

    public ScreenState? GetScreenState(string userId)
    {
        lock (_lock)
        {
            return _screenStates.GetValueOrDefault(userId);
        }
    }

    private void RaiseStateChanged(string userId, ScreenState state)
    {
        try
        {
            OnScreenStateChanged?.Invoke(new ScreenStateChangedEvent(userId, state));
        }
        catch (Exception ex)
        {
            // Swallow subscriber exceptions to prevent hub disruption, but log them
            logger.LogError(ex,
                "Subscriber threw while handling screen state change for user {UserId}", userId);
        }
    }
}
