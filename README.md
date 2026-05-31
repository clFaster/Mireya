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
- **Campaigns** — Ordered lists of assets (images, videos, URLs). Images/web pages use a configured display duration;
  videos use their own runtime. Assets loop, and campaigns can be enabled/disabled and scheduled with optional
  start/end dates. A campaign can be marked as the **default (fallback) campaign**, which is shown automatically on
  any screen that has no other active campaign assigned.
- **Assets** — Images, videos and websites. Videos get an automatically generated poster-frame thumbnail, and assets
  can be organised with **tags** and filtered via search in the admin.

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
