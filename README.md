# AKENO Control Center

AKENO Control Center is a LAN-first personal control surface with two views over the same component engine:

- **Dashboard Mode** — curated PC information and performance monitoring.
- **Deck Mode** — customizable multi-page Stream Deck-style control surface with flexible widgets, actions, toggles, sliders, buttons and multiple views of the same function.

The visual direction follows **AKENO — Neo-Samurai Noir**: deep black, off-white information, crimson interaction states, glass surfaces, subtle Japanese night atmosphere.

## Quick start

Requirements: .NET 8 SDK and a modern browser.

```bash
dotnet run --project src/Akeno.Host
```

Open `http://localhost:5077`, or from another device on your LAN use `http://YOUR-PC-IP:5077`.

## Included

- Dashboard / Deck switching
- Shared component/state engine
- Multiple Deck pages
- Add, delete, resize and drag widgets
- Persistent layout in browser local storage
- Widget gallery
- Universal range controls with slider and +/- views
- Toggle functions and action buttons
- Mock PC telemetry API
- Shared state between Dashboard and Deck
- Live polling from the C# host
- iPhone-friendly responsive UI
- PWA manifest and service worker
- GitHub Pages workflow for the public static demo

## Core rule

A function is not a widget. A function defines data and actions; a widget is a visual representation. The same function can appear in Dashboard and Deck and remain synchronized.

## Internet demo vs PC control

GitHub Pages hosts the web UI publicly. Because Pages is static hosting, the public demo uses simulated telemetry when it cannot reach the local C# host. Actual PC control runs through the ASP.NET Core host on the PC/LAN.

**AKENO — Beyond the Dawn.**
