# AKENO Control Center

**Dashboard — monitor everything. Deck — control anything.**

AKENO Control Center is a Windows-first, LAN-accessible personal control surface for your PC, iPhone and second screens. Dashboard Mode and Deck Mode share the same component/state engine, so the same function can appear in multiple forms and stay synchronized.

## Current full-app MVP

### Real Windows integration

- Master output volume: read + write
- Master output mute
- Default microphone: read + mute/unmute
- Microphone input level
- CPU/GPU/RAM telemetry via LibreHardwareMonitor
- CPU/GPU temperatures where supported
- Network throughput where supported
- Display brightness control on compatible Windows displays
- Lock / sleep / restart / shutdown actions

Hardware sensor availability depends on the PC and permissions. Some LibreHardwareMonitor sensors require Administrator privileges.

### Dashboard Mode

A curated AKENO monitoring view for PC performance, audio, network and stream status.

### Deck Mode

- Multiple pages
- Drag-and-drop tile movement
- Resizeable tiles
- Widget gallery
- Range values as sliders or +/- buttons
- Toggles and action tiles
- Shared state with Dashboard
- SQLite-backed layout persistence
- Responsive iPhone layout
- PWA support

The architectural rule is: **function != widget**. `master.volume` is one function; slider, +/- buttons and compact value cards are different views of that same function.

## Run on Windows

Requirements: Windows 10/11 and .NET 8 SDK.

Fast start:

```powershell
./run-windows.ps1
```

Or:

```powershell
dotnet restore
dotnet run --project src/Akeno.Host
```

Desktop:

```text
http://localhost:5077
```

Phone or another screen on the same Wi-Fi/LAN:

```text
http://YOUR-PC-IP:5077
```

The server listens on `0.0.0.0:5077` by default.

## API surface (host backend)

- `GET /api/health`
- `GET /api/config`
- `GET /api/state`
- `GET /api/components`
- `GET /api/components/{id}`
- `POST /api/control/{id}`
- `POST /api/action/{id}`
- `GET /api/pages`
- `POST /api/pages`
- `PUT /api/pages/{id}`
- `DELETE /api/pages/{id}`
- `GET /api/layout`
- `PUT /api/layout`
- `GET /api/settings`
- `PUT /api/settings`
- `POST /api/pair`
- `GET /api/clients`

Live updates are available through SignalR (`/hubs/control`) and SSE (`/api/events`).

## Optional pairing protection

For easy development, LAN writes are open by default. To require a pairing token before control commands:

```powershell
$env:AKENO_REQUIRE_PAIRING="true"
./run-windows.ps1
```

The host prints a six-digit pairing code. The pairing API exchanges that code for a temporary token stored in SQLite with device metadata and expiry. Before exposing the PC host outside your LAN, pairing/authentication must be enabled and the host should be placed behind a secure VPN/tunnel rather than directly port-forwarded.

## Public web demo

GitHub Pages deploys the static interface from `src/Akeno.Host/wwwroot`. The public version demonstrates Dashboard and Deck UI but cannot directly control your private PC because GitHub Pages is static hosting.

## Project structure

```text
src/Akeno.Host/
  Program.cs
  Hubs/
    ControlHub.cs
  Models/
    ComponentModels.cs
  Services/
    ComponentEngineService.cs
    DeckLayoutService.cs
    SettingsService.cs
    AkenoDbService.cs
    StateBroadcastWorker.cs
    WindowsAudioService.cs
    HardwareMonitorService.cs
    WindowsControlService.cs
    PairingService.cs
  wwwroot/
    index.html
    styles.css
    app.js
    manifest.webmanifest
    sw.js
.github/workflows/
  ci.yml
  pages.yml
```

## Brand direction

**AKENO — Neo-Samurai Noir**

Black is the world. White is information. Crimson is interaction and AKENO. The UI uses dark glass, restrained crimson states, clean typography and subtle Japanese-night atmosphere rather than generic RGB gaming styling.

## Integration roadmap

Next native modules are OBS WebSocket, Twitch Helix, Discord controls, per-application Windows audio, media-session artwork/playback, page/profile automation, custom scripts/macros, plugin SDK, secure remote relay and layout cloud backup.

## Safety note

System power actions are real when running the Windows host. Keep the service private to trusted networks/devices and do not expose port 5077 directly to the public internet.
