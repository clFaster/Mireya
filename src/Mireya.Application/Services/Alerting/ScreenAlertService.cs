using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Mireya.Database.Models;

namespace Mireya.Application.Services.Alerting;

/// <summary>
///     The kind of screen health transition being reported.
/// </summary>
public enum ScreenAlertKind
{
    Offline,
    Online,
}

/// <summary>
///     JSON payload POSTed to the configured webhook.
/// </summary>
public record ScreenAlertPayload(
    string Event,
    Guid ScreenId,
    string ScreenName,
    string? Location,
    string ScreenIdentifier,
    DateTime? LastSeenAtUtc,
    DateTime TimestampUtc,
    string Message
);

public interface IScreenAlertService
{
    /// <summary>
    ///     Sends a screen health alert to the configured webhook. Never throws: delivery failures
    ///     are logged and swallowed. No-ops when alerting is disabled or no URL is configured.
    /// </summary>
    Task SendAsync(
        ScreenAlertKind kind,
        Screen screen,
        CancellationToken cancellationToken = default
    );
}

public class ScreenAlertService(
    IHttpClientFactory httpClientFactory,
    IOptions<AlertingOptions> options,
    ILogger<ScreenAlertService> logger
) : IScreenAlertService
{
    public async Task SendAsync(
        ScreenAlertKind kind,
        Screen screen,
        CancellationToken cancellationToken = default
    )
    {
        var settings = options.Value;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.OfflineWebhookUrl))
            return;

        var payload = BuildPayload(kind, screen);

        try
        {
            var client = httpClientFactory.CreateClient(nameof(ScreenAlertService));
            using var response = await client.PostAsJsonAsync(
                settings.OfflineWebhookUrl,
                payload,
                cancellationToken
            );
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Screen alert webhook for {ScreenName} returned {StatusCode}",
                    screen.Name,
                    (int)response.StatusCode
                );
            }
        }
        catch (Exception ex)
        {
            // Alerting must never break monitoring.
            logger.LogError(
                ex,
                "Failed to deliver screen {Kind} alert for {ScreenName}",
                kind,
                screen.Name
            );
        }
    }

    private static ScreenAlertPayload BuildPayload(ScreenAlertKind kind, Screen screen)
    {
        var (eventName, message) = kind switch
        {
            ScreenAlertKind.Offline => (
                "screen.offline",
                $"Screen '{screen.Name}' ({screen.Location}) is offline."
            ),
            ScreenAlertKind.Online => (
                "screen.online",
                $"Screen '{screen.Name}' ({screen.Location}) is back online."
            ),
            _ => ("screen.unknown", $"Screen '{screen.Name}' changed state."),
        };

        return new ScreenAlertPayload(
            eventName,
            screen.Id,
            screen.Name,
            screen.Location,
            screen.ScreenIdentifier,
            screen.LastSeenAt,
            DateTime.UtcNow,
            message
        );
    }
}
