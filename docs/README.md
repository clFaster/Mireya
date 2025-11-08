# Mireya

> **Development status:** ⚠️ This project is currently in active development and is not in a usable or production-ready state. The design, features, and user experience may change frequently.

## The Vision

Mireya aims to be a modern, flexible, and open digital signage solution that makes it easy to manage and display dynamic content across multiple screens from Android TVs to Raspberry Pi devices. The goal is to provide a seamless end-to-end experience for administrators and operators, combining simplicity in setup with powerful campaign management.

## Core Concept

Mireya consists of two main components:

1. **Mireya Backend (Web Admin Panel)** – A centralized management platform where administrators can:
    - Register and manage display screens
    - Upload and organize assets (images, videos, or websites)
    - Create campaigns that define what content should be shown and for how long
    - Assign campaigns to specific screens or groups of screens
    - Monitor screen status and playback activity
2. **Mireya Client (Display App)** – A lightweight application designed to run on display devices such as Android TVs (and later Raspberry Pi or other embedded devices).
    - On first startup, the client only needs the Mireya backend URL
    - The screen automatically registers itself with the backend
    - After admin approval, the screen begins receiving its assigned campaigns and displaying assets according to schedule
    - Supports offline playback by caching assets locally

### Campaign System

A Campaign defines what is shown on a screen and in what order:
- Each campaign consists of multiple assets (images, videos, or web URLs)
- For static content (images, web pages), the admin sets a custom display duration
- Videos use their own runtime duration automatically
- Assets rotate in a loop, following the campaign configuration
- Future versions may include scheduling rules (e.g., time of day, weekdays, etc.)

## Planned Features & Roadmap

- ✅ **Phase 1** – Core backend & client communication (screen registration, asset management, campaign assignment)
- 📱 **Phase 2** – Extended client support (Raspberry Pi, Windows, web players)
- 📊 **Phase 3** – Monitoring & analytics (screen uptime, playback stats, asset performance)
- 🧩 **Phase 4** – Advanced scheduling (time-based rules, recurring campaigns)



## Key Values
- **Ease of Use** – One-step screen registration; automatic syncing with the backend
- **Flexibility** – Support for diverse asset types and dynamic scheduling
- **Scalability** – Manage from one to hundreds of displays
- **Open & Extensible** – Designed for future community contributions and integrations
