# Mireya

> ⚠️ **Active development** — not production ready.

Mireya is an open, flexible digital-signage platform: a web admin backend to manage screens, assets, and campaigns, and
lightweight clients (Android TV, Avalonia desktop, Raspberry Pi planned) that auto-register, cache assets, and play
scheduled content.

[**Vision — detailed page**](https://mireya.moritzreis.dev/#/?id=mireya) · [**Technical documentation
**](https://mireya.moritzreis.dev/#/development)

---

## 🧠 Key Concepts

- **Backend (Mireya.Api)** — Admin UI + ASP.NET Core Web API: register/manage screens, upload assets, create/assign
  campaigns, monitor playback.
- **Client (Mireya.Client / Mireya.Tv)** — Display apps that register to the backend, receive campaigns, cache assets,
  and loop playback. Minimal setup: only the backend URL is required on first start.
- **Campaigns** — Ordered lists of assets (images, videos, URLs). Images/web pages use a configured display duration;
  videos use their own runtime. Assets loop; scheduling rules are planned.

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

- .NET 9 SDK
- Node.js 20+
- (Optional) PostgreSQL
- Git

---

## 🤝 Contributing

1. Fork the repository
2. Create a branch: git checkout -b feature/your-feature
3. Make changes and test
4. Submit a pull request with a clear description

Please ensure all tests pass and follow the existing code style.
