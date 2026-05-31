# Mireya

> ⚠️ **Active development** — not production ready.

Mireya is an open, flexible digital-signage platform: a web admin backend to manage screens, assets, and campaigns, and
lightweight clients (Android TV, Avalonia desktop, Raspberry Pi planned) that auto-register, cache assets, and play
scheduled content.

[**Vision: detailed page**](https://mireya.moritzreis.dev/#/?id=mireya)

[**Technical documentation**](https://mireya.moritzreis.dev/#/development)

---

## 🧠 Key Concepts

- **Backend (Mireya.Api)** — Admin UI + ASP.NET Core Web API: register/manage screens, upload assets, create/assign
  campaigns, monitor playback. The admin interface uses a modern, responsive "Control Room" design system (custom
  Bootstrap theme with dedicated display, body and monospace typefaces) that adapts from desktop to mobile.
- **Client (Mireya.Client.Core + Mireya.Client.Desktop)** — Display apps that register to the backend, receive campaigns, cache assets,
  and loop playback. Minimal setup: only the backend URL is required on first start (it can also be preconfigured for
  unattended/kiosk deployments via the `MIREYA_BACKEND_URL` environment variable). The client reconnects automatically
  with exponential backoff and shows a colour-coded connection indicator on screen.
- **Campaigns** — Ordered lists of assets (images, videos, URLs). Assets can be reordered by drag-and-drop (or
  accessible up/down buttons) in the campaign editor, and bulk-uploaded several files at once.
  Images/web pages use a configured display duration;
  videos use their own runtime. Assets loop, and campaigns can be enabled/disabled and scheduled with optional
  start/end dates plus **weekly recurrence** (specific weekdays and/or a daily time window evaluated in a chosen
  time zone, with windows that may span midnight). A campaign can be marked as the **default (fallback) campaign**,
  which is shown automatically on any screen that has no other active campaign assigned. A background scheduler
  re-syncs screens automatically when a campaign's active window opens or closes, so time-based changes take effect
  without an edit. Individual screens can opt into **shuffled playback** to randomise their asset order.
- **Assets** — Images, videos and websites. Media can be added via a drag-and-drop upload area that previews each
  selected file (with image thumbnails and sizes) and lets you remove items before uploading. Videos get an
  automatically generated poster-frame thumbnail, and assets
  can be organised with **tags** and filtered via search in the admin. Images support a per-asset **fit mode**
  (contain, cover or fill) controlling how they scale to the screen, and transition with a smooth fade on the client.
- **Screens & Zones** — Each screen can be assigned campaigns directly, and can also belong to a **zone** (a named
  group of screens). A campaign assigned to a zone automatically plays on every member screen — including screens
  added to the zone later — so fleets can be managed together instead of one screen at a time. A screen's effective
  playlist is the union of its directly assigned campaigns and its zone's campaigns. Manage zones under **Zones**, and
  set a screen's zone from its edit page.
- **Audit log** — Administrative actions (creating, updating, deleting campaigns and assets; approving, rejecting and
  updating screens; sending remote commands) are recorded with the acting user and a timestamp, viewable in the admin
  under **Audit Log**.
- **Remote screen actions** — From a screen's detail page an administrator can push live commands to a connected
  screen over SignalR: **Restart** (replay the playlist from the start), **Reload** (re-render the current content) and
  **Identify** (briefly flash the screen and show its pairing code so it can be located within a fleet).
- **First-run pairing & approval** — On first launch a client registers itself and displays a large **pairing code**
  (its screen identifier) with a "waiting for approval" message until an administrator approves it under **Screens**.
  Once approved, content is pushed automatically — no client restart needed.
- **Proof of play** — Every time a screen starts showing an asset it is recorded, and the admin **Proof of Play**
  report aggregates plays by asset and by screen over a selectable time window (24 hours to 90 days) with a recent-plays
  log, so you can demonstrate exactly what played, where and when.
- **Offline alerting** — An optional background monitor raises a **webhook** alert when an approved screen has been
  offline beyond a configurable threshold, and a recovery alert when it reconnects. Each outage notifies once. Configure
  it under the `Alerting` section (see below); compatible with Slack/Teams/Discord/n8n/Zapier and custom endpoints.

---

## 🌟 Highlights / Values

- **Ease of Use** — One-step screen registration and automatic syncing
- **Flexibility** — Images, videos, web URLs, and multiple device targets
- **Scalability** — From single displays to large fleets
- **Open & Extensible** — Designed for community contributions

---

## 🗺️ Roadmap (Short)

- ✅ **Phase 1** — Core backend & client communication
- 📱 **Phase 2** — More client targets (Raspberry Pi, web players)
- 📊 **Phase 3** — Monitoring & analytics
- 🧩 **Phase 4** — Advanced scheduling & recurrence

---

## ⚙️ Quickstart (Developers)

**Requirements:**

- .NET 10 SDK
- SQLite (default, zero-setup) or PostgreSQL

**Run the backend locally:**

```bash
cd src/Mireya.Api
dotnet run
```

The admin UI is then available at `https://localhost:5001/login`.

**Or run the full stack with Docker (API + PostgreSQL):**

```bash
docker compose up --build   # API on http://localhost:8080
```

See the [technical documentation](https://mireya.moritzreis.dev/#/development) for database
configuration, migrations, tests, and operational endpoints (`/api/info`, `/alive`, `/health`).

**Offline screen alerting (optional):**

Set the `Alerting` section in `appsettings.json` (or the matching `Alerting__*` environment variables) to be
notified when a screen drops offline:

```json
"Alerting": {
  "Enabled": true,
  "OfflineWebhookUrl": "https://hooks.example.com/your-endpoint",
  "OfflineThresholdMinutes": 5,
  "PollIntervalSeconds": 60
}
```

When enabled, a JSON payload (`{ "event": "screen.offline" | "screen.online", "screenId", "screenName",
"location", "screenIdentifier", "lastSeenAtUtc", "timestampUtc", "message" }`) is POSTed to the webhook URL.

---

## 🤝 Contributing

1. Fork the repository
2. Create a branch: git checkout -b feature/your-feature
3. Make changes and test
4. Submit a pull request with a clear description

Please ensure all tests pass and follow the existing code style.
