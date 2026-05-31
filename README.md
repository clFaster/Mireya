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
  campaigns, monitor playback.
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
- **Assets** — Images, videos and websites. Videos get an automatically generated poster-frame thumbnail, and assets
  can be organised with **tags** and filtered via search in the admin. Images support a per-asset **fit mode**
  (contain, cover or fill) controlling how they scale to the screen, and transition with a smooth fade on the client.
- **Audit log** — Administrative actions (creating, updating, deleting campaigns and assets; approving, rejecting and
  updating screens; sending remote commands) are recorded with the acting user and a timestamp, viewable in the admin
  under **Audit Log**.
- **Remote screen actions** — From a screen's detail page an administrator can push live commands to a connected
  screen over SignalR: **Restart** (replay the playlist from the start) and **Reload** (re-render the current content).

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

---

## 🤝 Contributing

1. Fork the repository
2. Create a branch: git checkout -b feature/your-feature
3. Make changes and test
4. Submit a pull request with a clear description

Please ensure all tests pass and follow the existing code style.
