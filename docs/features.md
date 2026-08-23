# Features

This page describes Mireya from an operator's point of view: what can be managed in the admin UI and what display clients do once connected.

## Admin backend

The backend is both a web admin interface and an API server. Administrators sign in through the Blazor Server admin UI and manage assets, campaigns, screens, reports, and audit history.

The main admin sections are:

- Dashboard: high-level system entry point.
- Assets: media and website content library.
- Campaigns: playlists, schedules, and assignments.
- Screens: display registration, approval, configuration, and remote commands.
- Proof of Play: playback reporting.
- Audit Log: administrative action history.

## Assets

Mireya supports three asset types:

- **Image**: uploaded media shown by the display client.
- **Video**: uploaded media played by the display client.
- **Website**: a URL rendered by the client.

Administrators can upload files, create website assets, preview media, tag assets, search/filter the library, and delete assets. Image assets support a fit mode:

- `Contain`: preserve aspect ratio and fit the whole image on screen.
- `Cover`: preserve aspect ratio and fill the screen, cropping if needed.
- `Fill`: stretch the image to the full screen.

Video assets can have generated poster-frame thumbnails and use their configured or detected runtime for playback duration.

## Campaigns

A campaign is an ordered playlist of assets. Campaigns can be enabled, disabled, prioritized, scheduled, and assigned directly to screens.

Campaign behavior includes:

- Ordered assets with per-item duration support.
- Image and website duration control.
- Video playback based on video duration metadata.
- Campaign priority, with higher priority campaigns played first.
- Start and end date bounds.
- Weekly recurrence through selected weekdays.
- Daily time windows.
- Time-zone-aware recurrence evaluation.
- Windows that span midnight.

When campaign schedules open or close, a background service resynchronizes affected screens so time-based changes can take effect without a manual edit.

## Screens

Display clients register themselves on first connection. A new screen appears as pending until an administrator approves or rejects it.

Screen management includes:

- Pairing code shown on the client and visible in the admin.
- Approval/rejection workflow.
- Screen name, description, and location.
- Online/offline status and last-seen tracking.
- Direct campaign assignment.
- Per-screen shuffle playback.
- Asset sync status.
- Now-playing updates.

Once approved, a screen authenticates with the backend, connects to the SignalR hub, receives configuration updates, and starts syncing assigned content.
Pending and rejected screens receive no campaign content. Rejecting a connected screen immediately clears its active playlist; an offline screen is revoked when it reconnects.

## Display clients

The display client uses shared Avalonia UI and platform-specific renderers.

Implemented platform heads:

- **Desktop**: Windows/Linux project using WebView2 for website assets and LibVLC for video.
- **Android TV**: Android project using native Android WebView and Jetpack Media3/ExoPlayer.

Client capabilities include:

- Backend URL selection and persisted backend list.
- Optional `MIREYA_BACKEND_URL` preconfiguration.
- Fullscreen and autostart settings.
- On-demand Screen Info page, opened by touch, Enter/Space, or a TV remote's OK button.
- First-run registration and approval waiting screen.
- SignalR reconnect with capped exponential backoff.
- Local asset database and media cache.
- Asset sync progress reporting.
- Image, video, and website playback.
- Smooth transitions between assets.
- Proof-of-play reporting when an asset starts.
- Remote command handling.

## Remote commands

Administrators can send commands to connected screens:

- **Restart**: restart playback from the first playlist item.
- **Reload**: reload the current asset.
- **Identify**: briefly flash the screen and show its pairing code.
- **Next**: advance to the next asset.
- **Previous**: return to the previous asset.

Commands are delivered over the SignalR screen hub.

## Proof of play

The client reports when it starts showing an asset. The backend stores immutable playback events with screen and asset names captured at play time.

The Proof of Play report aggregates playback by asset and by screen over a selected time window and shows recent play activity.

## Audit log

Mireya records mutating administrative actions such as creating, updating, deleting, approving, rejecting, and sending commands. Audit entries include timestamp, actor, entity type, entity id, action, and summary.

## Offline alerting

Offline alerting is optional. When enabled, a background monitor sends a webhook alert when an approved screen remains offline longer than the configured threshold. It sends one alert per outage and a recovery alert when the screen comes back online.
