namespace Mireya.Application.Services.Alerting;

/// <summary>
///     Configuration for screen health alerting. Bind from the "Alerting" configuration section.
///     Disabled by default; set <see cref="Enabled" /> and a <see cref="OfflineWebhookUrl" /> to
///     receive notifications when a screen goes offline (and recovers).
/// </summary>
public class AlertingOptions
{
    public const string SectionName = "Alerting";

    /// <summary>
    ///     Master switch. When false, no monitoring runs and no webhooks are sent.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     URL that offline/online alerts are POSTed to as JSON. Compatible with incoming-webhook
    ///     endpoints (Slack, Teams, Discord, n8n, Zapier) and custom receivers.
    /// </summary>
    public string? OfflineWebhookUrl { get; set; }

    /// <summary>
    ///     How long a screen must remain offline before an alert is raised. Avoids alerting on
    ///     brief reconnects. Minimum one minute.
    /// </summary>
    public int OfflineThresholdMinutes { get; set; } = 5;

    /// <summary>
    ///     How often the monitor checks screen health. Minimum 15 seconds.
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 60;
}
