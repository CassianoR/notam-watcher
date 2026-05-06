# NOTAM Watcher

[![CI](https://github.com/YOUR_USERNAME/notam-watcher/actions/workflows/ci.yml/badge.svg)](https://github.com/YOUR_USERNAME/notam-watcher/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-7%2B-512BD4)](https://dotnet.microsoft.com)
[![Angular](https://img.shields.io/badge/Angular-17-DD0031)](https://angular.io)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> Real-time aviation NOTAM monitoring: define a route, watch the feed update live.

<!-- TODO: replace with animated GIF of the live feed -->
<!-- ![NOTAM Watcher demo](docs/demo.gif) -->

---

## Why this exists

NOTAMs (Notices to Air Missions) are time-critical safety notices — runway closures, airspace restrictions, navigation aid outages — that pilots and dispatchers must check before every flight. The FAA publishes hundreds per day across thousands of airports, but consuming them in real time requires polling an API, parsing a notoriously inconsistent free-text format, and surfacing only what's relevant to a specific route. This project demonstrates those exact challenges: resilient API polling, structured text parsing, and live push updates to a browser client.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Angular 17 (Tailwind, standalone components)               │
│  • Route input (multi-select ICAO codes)                    │
│  • Live NOTAM feed (SignalR subscriber)                     │
│  • Severity color coding, collapsible raw text              │
└──────────────────┬──────────────────────────────────────────┘
                   │ @microsoft/signalr  (WebSocket / SSE)
┌──────────────────▼──────────────────────────────────────────┐
│  .NET 7 Minimal API  (NotamWatcher.Api)                     │
│  • POST /routes      — register a watched route             │
│  • GET  /routes      — list active routes                   │
│  • SignalR /hubs/notams — push new/updated NOTAMs           │
│  • IHostedService background fetcher (configurable interval)│
└──────┬─────────────────────┬───────────────────────────────┘
       │                     │
┌──────▼──────┐   ┌──────────▼────────────────────────────────┐
│ NotamWatcher│   │  NotamWatcher.Infrastructure               │
│ .Parsing    │   │  • FaaNotamClient  (HttpClient + Polly)    │
│ (standalone │   │  • AppDbContext     (EF Core + SQLite)      │
│  lib)       │   │  • NotamRepository (AsNoTracking reads)    │
└─────────────┘   └───────────────────────────────────────────┘
                               │
                    ┌──────────▼──────────┐
                    │  SQLite (notams.db)  │
                    └─────────────────────┘
```

### Project layout

```
/
├── src/
│   ├── NotamWatcher.Api/          .NET 7 minimal API, SignalR hub, hosted service
│   ├── NotamWatcher.Parsing/      NOTAM text parser (no dependencies on infra)
│   ├── NotamWatcher.Domain/       Entities, value objects, enums
│   ├── NotamWatcher.Infrastructure/ EF Core, FAA HTTP client, Polly pipeline
│   └── NotamWatcher.Web/          Angular 17 app
├── tests/
│   ├── NotamWatcher.Parsing.Tests/ 15+ unit tests, 5 real NOTAM fixtures
│   └── NotamWatcher.Api.Tests/     WebApplicationFactory integration tests
├── tests/fixtures/                 Real NOTAM text samples
├── docker-compose.yml
├── .github/workflows/ci.yml
└── README.md
```

---

## Tech stack

| Layer | Technology |
|---|---|
| API | .NET 7 Minimal API, ASP.NET Core SignalR |
| Persistence | EF Core 7, SQLite, EF Migrations |
| Resilience | Polly v8 (retry + circuit breaker + timeout) |
| Logging | Serilog (console dev, rolling file prod) |
| Parser | Pure C# (no external parser libs), xUnit tests |
| Frontend | Angular 17 standalone, Tailwind CSS |
| Runtime | Docker Compose (API + Angular + optional Seq) |
| CI | GitHub Actions |

---

## Quickstart

### Prerequisites

- Docker + Docker Compose ≥ v2
- A free FAA API key (see below)

### Get a FAA API key (free, instant)

1. Go to <https://api.faa.gov> and click **Sign Up**
2. Create an account — no credit card required
3. Copy your API key from the dashboard

### Run with Docker Compose

```bash
git clone https://github.com/YOUR_USERNAME/notam-watcher.git
cd notam-watcher

# Set your API key
echo "FAA_API_KEY=your_key_here" > .env

# Start everything
docker-compose up --build
```

- Angular app: <http://localhost:4200>
- API swagger: <http://localhost:5000/swagger>
- Seq logs (optional): <http://localhost:5341>

### Run without Docker

```bash
# API (from repo root)
cd src/NotamWatcher.Api
dotnet user-secrets set "FaaApi:ApiKey" "your_key_here"
dotnet run

# Angular (separate terminal)
cd src/NotamWatcher.Web
npm install
npx ng serve
```

---

## Configuration

All settings live in `appsettings.json` and are overridable via environment variables:

```json
{
  "FaaApi": {
    "ApiKey": "",
    "BaseUrl": "https://api.faa.gov/notamapi/v1",
    "FetchIntervalSeconds": 60,
    "PageSize": 100
  },
  "Database": {
    "Path": "notams.db"
  },
  "Serilog": {
    "MinimumLevel": "Information"
  }
}
```

---

## Notable engineering decisions

### 1. Polly resilience pipeline on the FAA client

The FAA NOTAM API is a public endpoint with no SLA guarantees. The `FaaNotamClient` wraps its `HttpClient` with a three-layer Polly pipeline:

1. **Timeout policy (5 s)** — innermost; kills hung requests fast
2. **Retry with exponential backoff** — 3 attempts, delays of 1 s / 2 s / 4 s, jitter applied so burst retries don't self-DDoS the endpoint. Retries only on `HttpRequestException` and 5xx/429 responses.
3. **Circuit breaker** — opens after 5 consecutive failures, stays open for 30 s. When open, the background fetcher logs a warning and skips the cycle rather than queue-flooding the downstream.

This is registered as a typed `HttpClient` via `IHttpClientFactory`, so each fetch cycle gets a fresh handler while the circuit state is shared.

### 2. Parser design (`NotamWatcher.Parsing`)

NOTAM text has a semi-structured format defined by ICAO Annex 15, but real-world samples from the FAA deviate constantly — missing fields, non-standard date formats, multi-line free text. Rather than a monolithic regex, the parser uses a **pipeline of specialized extractors** (one per field group: location, Q-code, effective window, body). Each extractor returns a `Result<T>` discriminated union so parse failures are recoverable and unit-testable in isolation. The library has zero dependencies on infrastructure — it takes a `string`, returns a `ParsedNotam`.

### 3. SignalR group-per-route model

Clients subscribe to a hub group named after their route key (e.g., `KJFK-KLAX-KORD`). When the background fetcher finds new or updated NOTAMs for an airport, it resolves which route groups contain that airport and broadcasts only to those groups. This avoids fan-out to uninterested clients and keeps the message payload small (delta only, not full re-send). The hub requires an `X-Api-Key` header; the Angular client injects it via SignalR's `withUrl` options.

### 4. IHostedService, not a Timer

The background fetcher implements `IHostedService` via `BackgroundService`. The fetch loop uses `PeriodicTimer` (introduced in .NET 6) which avoids timer drift and supports clean cancellation on shutdown — no `Thread.Sleep` hacks, no fire-and-forget `Task.Run`.

### 5. EF Core read path

All read queries on `NotamRepository` use `.AsNoTracking()`. NOTAMs are append-heavy (new records far outnumber updates); write paths use explicit `SaveChangesAsync`. Migrations are code-first and committed to the repo — no scaffolded `DbContext` junk.

---

## Running tests

```bash
dotnet test
```

Parser tests are fully offline (no network, no DB). Integration tests use `WebApplicationFactory` with an in-memory SQLite DB.

---

## Roadmap

- [ ] Angular route persistence (save watched routes to localStorage)
- [ ] NOTAM expiry pruning job (delete NOTAMs past `EndValidity`)
- [ ] Support NOTAM severity override rules (user-defined keyword → severity map)
- [ ] ANAC (Brazil) NOTAM source (different schema, different API)
- [ ] Auth beyond API key (JWT, OAuth2 with FAA SSO)
- [ ] Multi-user route subscriptions with a proper user table
- [ ] Export to PDF preflight briefing format
- [ ] Map view with affected airspace polygons

---

## License

MIT — see [LICENSE](LICENSE).
