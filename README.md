<p align="center">
  <h1 align="center">IIS Monitor</h1>
  <p align="center">
    <strong>Enterprise-Grade IIS Health Monitoring & Auto-Recovery</strong>
  </p>
  <p align="center">
    Lightweight · Real-Time · Zero Dependency on .NET 4.5+
  </p>
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-green.svg" alt="License"></a>
  <a href="#"><img src="https://img.shields.io/badge/.NET-4.0-blue.svg" alt=".NET 4.0"></a>
  <a href="#"><img src="https://img.shields.io/badge/Platform-Windows%20Server%202008%2B-lightgrey.svg" alt="Platform"></a>
  <a href="#"><img src="https://img.shields.io/badge/IIS-7.0+-0078D4.svg" alt="IIS"></a>
  <a href="#"><img src="https://img.shields.io/badge/Tests-26%20Passed-brightgreen.svg" alt="Tests"></a>
  <a href="https://github.com/xuu1998/IISMonitor/releases"><img src="https://img.shields.io/badge/Release-v1.0.0-blue.svg" alt="Release"></a>
</p>

---

## Table of Contents

- [Why IIS Monitor?](#why-iis-monitor)
- [Features](#features)
- [Architecture](#architecture)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Restart Strategies](#restart-strategies)
- [Performance Monitoring](#performance-monitoring)
- [Project Structure](#project-structure)
- [FAQ](#faq)
- [Contributing](#contributing)
- [License](#license)

---

## Why IIS Monitor?

Running IIS on legacy Windows Server environments shouldn't mean sacrificing observability. **IIS Monitor** fills the gap where modern monitoring tools can't reach — it targets **.NET Framework 4.0**, runs natively on **Windows Server 2008 R2 SP1**, and requires **no external dependencies** beyond the IIS management API that's already on your server.

| Problem | Solution |
|:--------|:---------|
| IIS app pools silently crash at 3 AM | Automatic detection & recovery with configurable thresholds |
| No visibility into worker process memory/connection leaks | Real-time app pool performance dashboard |
| Legacy servers can't run .NET 4.5+ monitoring tools | Built for .NET 4.0 — runs everywhere IIS runs |
| Recovery actions cause cascading failures | Smart backoff prevents restart storms |
| Need to check multiple servers manually | Centralized dashboard with tray icon health indicators |

---

## Features

### Core Monitoring Engine

- **App Pool Health Check** — Detects Stopped / Started / Unknown states via `Microsoft.Web.Administration`
- **HTTP/HTTPS Availability** — Deep health checks with keyword matching and configurable timeouts
- **9 Recovery Strategies** — From gentle app pool recycling to full IIS restart cascades
- **Failure Threshold** — Require N consecutive failures before triggering recovery (avoids false positives)
- **Recovery Backoff** — Max 3 recoveries per 5 minutes per target, preventing restart storms

### App Pool Performance Dashboard

Real-time metrics for every application pool on the server:

| Metric | Source | Description |
|:-------|:-------|:------------|
| Worker Process PID | `ServerManager.WorkerProcesses` | Active worker process IDs |
| Memory (MB) | `Process.WorkingSet64` | Total working set memory per pool |
| Active Requests | `W3SVC_W3WP` Counter | Current in-flight requests |
| Requests/sec | `W3SVC_W3WP` Counter | Request throughput |
| Queue Length | `ASP.NET` Counter | Pending request queue depth |

Auto-refreshes every 10 seconds. Manual refresh button available. Idle pools show "Idle (no requests)" instead of misleading zeros.

### Observability & Alerts

- **Live Health Chart** — Real-time line chart for CPU / Memory / Disk usage trends
- **SMTP Email Alerts** — Configurable SMTP server, recipients, and subject templates
- **Webhook Alerts** — POST JSON payloads to Slack, Teams, DingTalk, or custom endpoints
- **Alert Cooldown** — Prevents notification floods during sustained outages
- **CSV & HTML Reports** — Export historical health data for offline analysis

### User Experience

- **Dual-Tab Layout** — Switch between monitoring logs and app pool performance at a glance
- **Dark Mode** — Full dark theme with persistent preference
- **System Tray** — Green / Yellow / Red icon reflects overall health status
- **Auto-Minimize** — Minimize to tray on monitoring start
- **Quick Select** — Pick sites and app pools directly from local IIS configuration
- **Double-Click Test** — Instantly test any site or app pool by double-clicking its row

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                      MainForm (UI)                       │
│  ┌──────────┐  ┌──────────┐  ┌────────────────────────┐ │
│  │ Tab: Logs │  │Tab: Pools│  │   Health Chart (Live)  │ │
│  └──────────┘  └──────────┘  └────────────────────────┘ │
└─────────────┬───────────────────────────┬───────────────┘
              │ Events                    │ Events
┌─────────────▼───────────────────────────▼───────────────┐
│                   MonitorService                         │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────┐  │
│  │ HealthCheck  │  │ResourceMonitor│  │ MetricsTimer   │  │
│  │ (Timer)      │  │ (CPU/Mem/Disk)│  │ (10s interval) │  │
│  └──────┬──────┘  └──────────────┘  └───────┬────────┘  │
│         │                                    │           │
│  ┌──────▼────────────────────────────────────▼────────┐  │
│  │                   IISHelper                         │  │
│  │  • ServerManager API  • PerformanceCounters         │  │
│  │  • App Pool CRUD      • HTTP Health Checks          │  │
│  │  • Worker Process     • IIS Restart                 │  │
│  └─────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────┘
              │
┌─────────────▼──────────────────────────────────────────┐
│                   Infrastructure                         │
│  ┌──────────┐  ┌─────────────┐  ┌────────────────────┐ │
│  │  Logger   │  │ AlertService│  │  ReportExporter    │ │
│  │ (Async)   │  │ (SMTP/Hook) │  │  (CSV / HTML)     │ │
│  └──────────┘  └─────────────┘  └────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

**Key Design Decisions:**

- **Zero external process dependencies** — Uses `Microsoft.Web.Administration` API directly, no PowerShell or `appcmd.exe` calls
- **Async logging** — `BlockingCollection<T>` + dedicated writer thread, never blocks the monitoring loop
- **Thread-safe counters** — `ConcurrentDictionary` for failure tracking across timer callbacks
- **PerformanceCounter dual-sampling** — Rate counters (`NextValue()`) require two samples; handled automatically with configurable sleep intervals

---

## Quick Start

### Prerequisites

| Requirement | Version |
|:------------|:--------|
| Windows Server | 2008 R2 SP1 or later |
| .NET Framework | 4.0 Runtime |
| IIS | 7.0 or later |
| Permissions | **Administrator** (required for IIS management) |

### Build & Run

```bash
# Clone
git clone https://github.com/xuu1998/IISMonitor.git
cd IISMonitor

# Build (NuGet packages auto-restore)
dotnet build IISMonitor\IISMonitor.csproj -c Release

# Run as Administrator
IISMonitor\bin\Release\IISMonitor.exe
```

### First Run

1. Click **"从本机选站点"** / **"从本机选应用池"** to import IIS configuration
2. Set check interval (default: 5 seconds) and failure threshold (default: 2)
3. Choose a restart strategy from the dropdown
4. Click **"启动监控"**
5. (Optional) Enable **"启动时自动监控"** for hands-free operation

---

## Configuration

Configuration is auto-saved to `MonitorConfig.xml` on first run.

| File | Location | Purpose |
|:-----|:---------|:--------|
| `MonitorConfig.xml` | App directory | Runtime configuration |
| `logs\IISMonitor_YYYYMMDD.log` | `logs\` | Daily rotating logs (10MB max, 30-day retention) |
| `logs\health_results.jsonl` | `logs\` | Structured health check data (for report export) |

---

## Restart Strategies

Choose the recovery approach that matches your tolerance for downtime vs. impact:

| Strategy | Impact | When to Use |
|:---------|:-------|:------------|
| `AppPoolOnly` | Minimal — recycles single pool | Default for most scenarios |
| `AppPoolRecycleOnly` | Minimal — graceful recycle | When you want soft recycling only |
| `SiteRestartOnly` | Low — restarts single site | When pool recycling isn't enough |
| `AppPoolThenIIS` | Medium — escalates to full IIS | When pool issues persist |
| `AppPoolThenSite` | Low-Medium — tries pool first | Balanced escalation |
| `SiteThenAppPool` | Low-Medium — tries site first | When site config is the issue |
| `SiteThenIIS` | Medium — escalates to full IIS | When site restart isn't enough |
| `IISOnly` | **High** — affects ALL sites | Last resort, server-level issues |
| `All` | **High** — tries everything | Maximum recovery coverage |

> **Warning:** `IISOnly` and `All` strategies will briefly interrupt **all** websites on the server. Use with caution in production.

---

## Performance Monitoring

The **App Pool Performance** tab provides real-time visibility into worker process behavior:

```
┌─────────────────────────────────────────────────────────────┐
│  [Refresh]                                                    │
├──────────┬───────┬──────┬─────────┬──────────┬───────┬──────┤
│ App Pool │  PID  │ WPs  │ Mem(MB) │ Active   │ Req/s │Queue │
├──────────┼───────┼──────┼─────────┼──────────┼───────┼──────┤
│ Default  │ 1234  │  1   │   128.5 │     5    │  12.3 │   0  │
│ ApiPool  │ 5678  │  2   │   256.0 │    42    │  87.1 │   0  │
│ AppPool2 │   -   │  0   │     -   │     -    │   -   │   -  │
│ AppPool3 │ 9012  │  1   │    64.2 │     0    │   0.0 │   0  │
└──────────┴───────┴──────┴─────────┴──────────┴───────┴──────┘
```

- **Auto-refresh** every 10 seconds (independent of health check interval)
- **Manual refresh** via button for on-demand snapshots
- **"-"** indicates the pool is idle (no active worker process) or stopped
- **Performance counters** use dual-sampling to handle rate-based metrics correctly

---

## Project Structure

```
IISMonitor/
├── IISMonitor/
│   ├── Models/
│   │   ├── HealthRecord.cs           # Health check result model
│   │   └── AppPoolMetrics.cs         # App pool performance snapshot
│   ├── Services/
│   │   ├── ResourceMonitor.cs        # CPU/Memory/Disk monitoring
│   │   └── AlertService.cs           # SMTP & Webhook alerting
│   ├── Export/
│   │   └── ReportExporter.cs         # CSV & HTML report generation
│   ├── UI/
│   │   └── HealthChart.cs            # LiveCharts2 real-time chart
│   ├── Infrastructure/
│   │   ├── ThemeManager.cs           # Light/Dark theme engine
│   │   └── EnumExtensions.cs         # Enum Description attribute helpers
│   ├── libs/                         # Vendored DLLs (MWA, Newtonsoft.Json)
│   ├── MainForm.cs                   # Main window logic & event wiring
│   ├── MainForm.Designer.cs          # WinForms layout (auto-generated)
│   ├── MonitorService.cs             # Core monitoring loop & orchestration
│   ├── MonitorConfig.cs              # Configuration model & XML persistence
│   ├── IISHelper.cs                  # IIS API wrapper (ServerManager + Counters)
│   └── Logger.cs                     # Async file logger with rotation
├── IISMonitor.Tests/                 # Unit tests (26 tests)
├── LICENSE                           # MIT License
└── README.md
```

---

## FAQ

<details>
<summary><strong>Q: Why target .NET 4.0 instead of a newer framework?</strong></summary>

Many enterprise Windows Server 2008 R2 environments cannot upgrade to .NET 4.5+. By targeting .NET 4.0, IIS Monitor runs on the widest range of servers without requiring framework upgrades.
</details>

<details>
<summary><strong>Q: Why are performance counters showing zeros?</strong></summary>

Rate-based counters (like `Total Requests/Sec`) require two samples to calculate. IIS Monitor handles this with automatic dual-sampling. If values are still zero, the app pool may be idle (no active worker process) — check the "PID" column for "-".
</details>

<details>
<summary><strong>Q: Can I run this as a Windows Service?</strong></summary>

The current version is a WinForms application with system tray support. For unattended server operation, enable "启动时自动监控" + "启动时最小化" in settings, and configure a Windows Task Scheduler entry to run at logon.
</details>

<details>
<summary><strong>Q: Does it support remote IIS servers?</strong></summary>

Not yet. IIS Monitor uses `Microsoft.Web.Administration` which operates on the local IIS instance. For remote monitoring, deploy an instance on each server.
</details>

---

## Contributing

Contributions are welcome! Please open an issue first to discuss what you'd like to change.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

You are free to use, modify, and distribute this software for any purpose, including commercial use.
