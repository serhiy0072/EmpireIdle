# 🏰 EmpireIdle

Browser-based idle empire builder: a village that produces while you're away, an army and heroes, a 500×500 procedurally generated world with monsters and PvP, clans, banners, and turn-based dungeons — built with **ASP.NET Core (.NET 10)**, **Clean Architecture**, **DDD**, **CQRS**, and a **React + TypeScript** client.

## 🏗️ Architecture

```
┌──────────────────────────────────────────┐
│               External                    │
│   PostgreSQL · Hangfire · Stripe          │
└───────────┬──────────┬───────────────────┘
            │          │
┌───────────┴──┐  ┌────┴──────────────────┐
│Infrastructure│  │          API           │
│ EF Core      │  │ Controllers, DTOs      │
│ Repositories │  │ SignalR hub, Hangfire  │
│ Identity/JWT │  │ ProblemDetails, config │
└──────┬───┬───┘  └────────┬──────────────┘
       │   │               │
       │   └───────┬───────┘
       │           │
┌──────┴───────────┴───────┐        ┌──────────────────────┐
│       Application        │        │   Web (React + TS)   │
│  CQRS · MediatR          │ ◄────► │ types from OpenAPI   │
│  pipeline behaviors      │  HTTP  │ React Query, SignalR │
└──────────┬───────────────┘        └──────────────────────┘
           │
┌──────────┴───────────────┐
│          Domain           │
│ Aggregates · Value Objects│
│ Events · Services · Config│
└──────────────────────────┘
```

**Key patterns:** Clean Architecture, DDD (aggregates, value objects, domain events), Repository + Unit of Work, CQRS with MediatR pipeline behaviors, Transactional Outbox, optimistic concurrency (`xmin`).

## 💡 Design Highlights

**Domain knows nothing about infrastructure.** Config and operations arrive as parameters, never through DI. Game rules — the tier gate on upgrades, the dungeon battle engine, casualty buckets — live in the domain and are tested without a database.

**Config-driven gameplay.** Resources, buildings, units, heroes, items, monsters, dungeons and combat modifiers live in JSON (`src/EmpireIdle.API/Config`) and are validated at startup. Change the config — change the game, no recompilation.

**Computed over stored.** Terrain isn't persisted — it's a deterministic function of `(serverId, x, y)` with a seed. Production is lazy: a building's buffer is a pure function of time, so there is no per-minute tick over every village.

**Domain events through a Transactional Outbox.** Events are written in the same transaction as the change and published afterwards — a SignalR push never announces something the database rolled back.

**Timers as recurring scans, not delayed jobs.** `CompletesAt` in the database is the single source of truth, so speeding up is a field update and the scanner self-heals after a restart.

**IDOR protection by construction.** A MediatR behavior checks that the `PlayerId` in every request matches the player in the JWT. New endpoints are protected automatically.

**Idempotent commands.** State-changing commands require an `Idempotency-Key`; a retry returns the stored response instead of charging twice. The client sends one key per action and retries a lost response with the same key. Dungeon turns are guarded by the turn number instead (`expectedTurn` → 409 `StaleTurn`).

**Refusals the player can read.** Every refusal a player can reach by fair play carries a stable reason key and arguments in `ProblemDetails`; the client owns the Ukrainian text. The keys are a committed contract (`refusals/reasons.json`), and the client won't typecheck while a key has no text.

**Server-authoritative turn-based dungeons.** The battle state lives in the database, so a run survives a page reload. One request plays exactly one turn, which is what lets autoplay and manual control switch mid-battle.

## 🛠️ Tech Stack

- **Backend:** ASP.NET Core / .NET 10, EF Core, PostgreSQL
- **Auth:** ASP.NET Identity, JWT with refresh-token rotation and reuse detection
- **CQRS:** MediatR with behaviors for logging, validation (FluentValidation), player scope and idempotency
- **Realtime:** SignalR (per-player groups, JWT over WebSocket)
- **Background jobs:** Hangfire with PostgreSQL storage
- **Payments:** Stripe Checkout + webhook (test mode)
- **Frontend:** React 19, TypeScript, Vite, Tailwind, React Query, React Router; API types generated from OpenAPI
- **Testing:** xUnit, NSubstitute, AwesomeAssertions, Testcontainers (PostgreSQL), NetArchTest architecture rules

## 📦 Projects

| Project | Responsibility |
|---------|---------------|
| `EmpireIdle.Domain` | Aggregates, value objects, domain events, domain services (terrain, combat, march timing, dungeon battle engine, progression curves), game config models and validation |
| `EmpireIdle.Application` | CQRS commands/queries, pipeline behaviors, repository interfaces |
| `EmpireIdle.Infrastructure` | EF Core, PostgreSQL, Identity/JWT, repositories, outbox, migrations |
| `EmpireIdle.API` | Controllers, DTOs, SignalR hub, Hangfire jobs, error handling, game config files |
| `EmpireIdle.Web` | React + TypeScript client — every main screen of the game |
| `tests/*` | Domain, application, API/integration and architecture tests; `EmpireIdle.TestKit` holds shared fixtures |

Committed contracts: `openapi/v1.json` (HTTP API), `realtime/events.json` (SignalR events), `refusals/reasons.json` (refusal reasons). Contract tests fail when the code and the committed file drift apart.

## 🎮 Game Systems

**Village.** 20 unique buildings with lazy production, storage caps and a three-rule tier gate: a server-level ceiling, nothing above the town hall, and the town hall can't cross a tier while open buildings lag behind.

**Army.** Unit training and levelling in batches, an army cap from the barracks, a hospital with wounded / recoverable / lost buckets.

**World.** A 500×500 map with concentric rings, fog that expands as the server evolves, monsters scaled by distance, marches led by heroes, teleports, a battle preview that matches the real battle.

**PvP.** Attacking villages, a protected stash and plunder, a newbie shield checked on departure and on arrival, clan reinforcements that share losses.

**Heroes and gear.** Common / rare / unique heroes with tiers, levels and constellations; weapons and artifacts with enhancement, breaking and repair; set bonuses; combat power and rating.

**Banners.** Typed pools, two pity counters carried across banners of one group, the 50/50 rule, a roll journal with seed and pity state.

**Dungeons.** Turn-based battles for a team of up to four heroes in front and back lines, two active abilities per hero, statuses, energy that refills over time, three difficulty levels, and five tiers of paired dungeons whose artifact sets grow stronger with the tier and differ by focus. Heroes wear four typed artifacts — necklace, crown, ring and belt.

**Player market.** Equipment, heroes and tradeable items for gold at a fixed price inside a corridor around a 48-hour trimmed median of gold per unit of Power, anchored to shop prices so wash trading can't drag it; a burned listing tax, escrow while listed, a resale cooldown, and a listing limit from the market building.

**Social and progression.** Clans with roles and permissions, applications and invites, help with timers, reinforcements; daily, chain and server quests; a guided onboarding.

**Chat and languages.** Server, clan and private chat over SignalR with anti-spam; clan recipients are read from the database at delivery, not from hub groups. Messages remember the sender's language and are translated once per language through a pluggable translator (none by default). Config names ship in Ukrainian with per-language overrides, and the catalog is served in the player's language.

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL 17+](https://www.postgresql.org/download/)
- [Node.js 22+](https://nodejs.org/) for the client
- [EF Core CLI Tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) (`dotnet tool install --global dotnet-ef`)
- Docker (only for the integration tests — they start PostgreSQL through Testcontainers)

### Setup

1. **Clone the repository:**
```bash
git clone https://github.com/serhiy0072/EmpireIdle.git
cd EmpireIdle
```

2. **Create a PostgreSQL database and user:**
```sql
CREATE USER empireidle_user WITH PASSWORD 'your_password';
CREATE DATABASE empireidle OWNER empireidle_user;
```

3. **Configure secrets via User Secrets:**
```bash
cd src/EmpireIdle.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=empireidle;Username=empireidle_user;Password=your_password"
dotnet user-secrets set "JwtSettings:Secret" "your-very-long-random-secret-at-least-32-chars"
dotnet user-secrets set "JwtSettings:Issuer" "EmpireIdle"
dotnet user-secrets set "JwtSettings:Audience" "EmpireIdle.Players"
```

4. **Apply migrations:**
```bash
cd ../..
dotnet ef database update --project src/EmpireIdle.Infrastructure --startup-project src/EmpireIdle.API
```

5. **Run the API:**
```bash
dotnet run --project src/EmpireIdle.API --launch-profile https
```

6. **Run the client** (in another terminal):
```bash
cd src/EmpireIdle.Web
npm install
npm run dev
```
   The client runs on `http://localhost:5173`; the API allows that origin by default.

7. **Useful pages:**
   - Swagger UI: `https://localhost:7031/swagger`
   - Hangfire Dashboard: `https://localhost:7031/hangfire`

### Tests

```bash
dotnet test EmpireIdle.slnx
cd src/EmpireIdle.Web && npm run typecheck && npm run lint
```

## 📡 API

The full HTTP contract is `openapi/v1.json` (also served by Swagger in development). Controllers are grouped by feature: auth, village, garrison, heroes, inventory, banners, shop, wallet, map, marches, battle reports, clans, quests, server quests, rating, power, dungeons, tutorial, payments and a public game catalog.

Errors are `ProblemDetails` with a stable `errorCode`; refusals a player can hit also carry `reason` and `args` from `refusals/reasons.json`.

## ⚙️ Background Jobs

| Job | Schedule | Purpose |
|-----|----------|---------|
| `timer-scan` | every minute | Complete due constructions, trainings, level-ups and marches |
| `server-quest-totals` | every minute | Aggregate contributions to server quests |
| `monster-spawn` | every 5 minutes | Keep the monster population across the map |
| `outbox-maintenance` | hourly | Clean up processed outbox messages |
| `rating-recalculation` | hourly | Recalculate player ratings |
| `daily-quest-reset` | daily | Reset daily quests |
| `server-evolution` | daily | Evolve servers and expand the fog |
| `clan-leadership` | daily | Hand clan leadership over from inactive leaders |
| `market-expiry` | every minute | Close expired market listings and return the goods |
| `market-prices` | hourly | Recalculate the market median snapshots |
| `chat-retention` | daily | Delete chat history older than the retention window |

## 🗺️ Roadmap

- [x] Clean Architecture + DDD domain model, CQRS with MediatR
- [x] Authentication (Identity + JWT with refresh rotation), SignalR
- [x] Village, lazy production, tier gate
- [x] Units, garrison, hospital, world map, monsters, marches, combat
- [x] Monetization basics (gems, speed-ups, Stripe Checkout in test mode)
- [x] Transactional Outbox, idempotency, optimistic concurrency, multi-server filters
- [x] Quests, server quests, rating, power and battle preview
- [x] Clans, PvP and plunder
- [x] Heroes, equipment, banners
- [x] Game UI (React) for every main screen, onboarding
- [x] Turn-based dungeons
- [x] Player market with a price corridor
- [x] Chat, player language and localized catalog
- [ ] Balance pass (numbers in the configs are placeholders)
- [ ] Mailbox, auction, machine translation provider, UI string dictionaries
- [ ] Clan territory, city fall and shields
- [ ] Docker + deployment
