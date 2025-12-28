using System.Collections.Concurrent;

namespace Mireya.Api.Services;

/// <summary>
///     Service to track which screens are currently connected via SignalR
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
}

public class ScreenConnectionTracker : IScreenConnectionTracker
{
    // Maps ConnectionId -> UserId
    private readonly ConcurrentDictionary<string, string> _connectionToUser = new();

    private readonly Lock _lock = new();

    // Maps UserId -> HashSet of ConnectionIds (a user/screen might have multiple connections)
    private readonly ConcurrentDictionary<string, HashSet<string>> _userToConnections = new();

    public void AddConnection(string userId, string connectionId)
    {
        lock (_lock)
        {
            _connectionToUser[connectionId] = userId;

            if (!_userToConnections.ContainsKey(userId))
                _userToConnections[userId] = new HashSet<string>();

            _userToConnections[userId].Add(connectionId);
        }
    }

    public void RemoveConnection(string connectionId)
    {
        lock (_lock)
        {
            if (_connectionToUser.TryRemove(connectionId, out var userId))
                if (_userToConnections.TryGetValue(userId, out var connections))
                {
                    connections.Remove(connectionId);

                    // If user has no more connections, remove the user entry
                    if (connections.Count == 0)
                        _userToConnections.TryRemove(userId, out _);
                }
        }
    }

    public int GetOnlineScreenCount()
    {
        // Count unique users (screens) that have at least one active connection
        return _userToConnections.Count;
    }

    public IEnumerable<string> GetConnectedUserIds()
    {
        return _userToConnections.Keys.ToList();
    }
}
