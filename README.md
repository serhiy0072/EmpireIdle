# 🏰 EmpireIdle

Browser-based idle empire builder with city management, world map exploration, and PvE combat — built with **ASP.NET Core (.NET 10)**, **Clean Architecture**, **DDD**, and **CQRS**.

Build your village across terrain zones, train armies, march across a 1000×1000 procedurally generated world, and fight monsters — even while you're offline.

## 🏗️ Architecture

```
┌──────────────────────────────────────┐
│            External                   │
│  PostgreSQL · Hangfire · Stripe (TBD)│
└───────────┬──────────┬───────────────┘
            │          │
┌───────────┴──┐  ┌────┴──────────────┐
│Infrastructure│  │       API          │
│ EF Core      │  │ Controllers, DTOs │
│ Repositories │  │ Hangfire, Swagger  │
│ Identity/JWT │  │ Auth, Middleware   │
└──────┬───┬───┘  └────────┬──────────┘
       │   │               │
       │   └───────┬───────┘
       │           │
┌──────┴───────────┴───────┐
│       Application        │
│  CQRS · MediatR          │
└──────────┬───────────────┘
           │
┌──────────┴───────────────┐
│          Domain           │
│ Entities · Value Objects  │
│ Events · Services · Config│
└──────────────────────────┘
```

**Key patterns:** Clean Architecture, DDD (Aggregates, Value Objects, Domain Events), Repository + Unit of Work, CQRS with MediatR.

## 💡 Design Highlights

**Domain knows nothing about infrastructure.** Config and operations arrive as parameters, never through DI. `SettlementPlacer` takes a `Func<int,int,Task<bool>> isOccupied` delegate instead of a repository — so it's tested with a lambda, no mocks, no database.

**Config-driven gameplay.** Resources, buildings, units, monsters, terrain and combat modifiers live in JSON. Change the config — change the game, no recompilation. The same backend could power a reskin (SpaceIdle, ZombieIdle).

**Computed over stored.** Terrain for the 1000×1000 world isn't persisted — it's a deterministic function of `(serverId, x, y)` with a seed. Only occupied cells hit the database. Monster power and rewards are derived from type + level the same way.

**Domain events via EF interceptor.** A `SaveChangesInterceptor` collects events from tracked aggregates *after* a successful commit and publishes them through MediatR — so a SignalR push never announces something the database rolled back.

**Timers as recurring scans, not delayed jobs.** `CompletesAt` in the database is the single source of truth, so speeding up or cancelling is a field update — no job rescheduling, and the scanner self-heals after a restart.

**IDOR protection by construction.** A MediatR behavior verifies that the `PlayerId` in any request matches the player in the JWT. New endpoints are protected automatically — you can't forget the check.

## 🛠️ Tech Stack

- **Backend:** ASP.NET Core / .NET 10, EF Core, PostgreSQL
- **Auth:** ASP.NET Identity, JWT with refresh token rotation
- **CQRS:** MediatR (commands/queries, pipeline behaviors for logging, validation and player scope)
- **Realtime:** SignalR (per-player groups, JWT auth over WebSocket)
- **Background Jobs:** Hangfire with PostgreSQL storage
- **Testing:** xUnit (domain logic, deterministic time and terrain)
- **API:** REST, Swagger/OpenAPI, ProblemDetails error handling
- **Frontend:** React 19 + TypeScript + Vite + Tailwind (in progress — login implemented, game UI pending)
- **Monetization:** Stripe (planned)

## 📦 Projects

| Project | Responsibility |
|---------|---------------|
| `EmpireIdle.Domain` | Entities, Value Objects, Domain Events, domain services (terrain generation, combat, march timing, settlement placement), Game Config |
| `EmpireIdle.Application` | CQRS commands/queries, MediatR pipeline behaviors, repository interfaces |
| `EmpireIdle.Infrastructure` | EF Core, PostgreSQL, Identity/JWT, repository implementations, domain event interceptor, DI |
| `EmpireIdle.API` | Controllers, DTOs, Auth, Hangfire jobs, Swagger, Middleware, SignalR Hub, game config files |
| `EmpireIdle.Web` | React + TypeScript frontend (login implemented; game UI pending) |

## 🎮 Game Systems

**City building.** Terrain zones (plain / forest / mountain / water) with limited slots, buildings with production buffers and geometric storage growth, real-time construction with builder limits, town hall level gating, multi-resource costs.

**Army.** Population from housing, batch training in barracks, garrison management, hospital with a three-bucket casualty system (wounded / instantly recoverable / permanently lost).

**World map.** A 1000×1000 world with procedural terrain, monsters scaled by distance from the center, marches timed by `distance ÷ slowest unit speed × terrain difficulty`.

**Combat.** Symmetric formula for PvE and future PvP, terrain modifiers per unit type (cavalry +25% on plains, archers +25% in mountains), normally distributed randomness, battle reports with per-unit casualty breakdown.

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL 17+](https://www.postgresql.org/download/)
- [EF Core CLI Tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) (`dotnet tool install --global dotnet-ef`)

### Setup

1. **Clone the repository:**
```bash
   git clone https://github.com/serhiy0072/EmpireIdle.git
   cd EmpireIdle
```

2. **Create PostgreSQL database and user:**
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
   dotnet run --project src/EmpireIdle.API
```

6. **Open in browser:**
   - Swagger UI: `http://localhost:5253/swagger`
   - Hangfire Dashboard: `http://localhost:5253/hangfire`

## 📡 API Endpoints

### Auth

| Method | URL | Description |
|--------|-----|-------------|
| `POST` | `/api/auth/register` | Register player (IdentityUser + Village + Garrison + Wallet), returns JWT |
| `POST` | `/api/auth/login` | Authenticate, returns access + refresh tokens |
| `POST` | `/api/auth/refresh` | Rotate tokens (with reuse detection) |

### Village (requires JWT)

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/village/{playerId}` | Village state: buildings, resources, construction timers |
| `POST` | `/api/village/{playerId}/buildings` | Construct a building (zone, slot and town hall checks) |
| `POST` | `/api/village/{playerId}/buildings/{buildingId}/upgrade` | Start an upgrade |
| `POST` | `/api/village/{playerId}/buildings/{buildingId}/collect` | Collect from a production buffer |

### Garrison (requires JWT)

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/garrisons/{playerId}` | Units, wounded, active training orders |
| `POST` | `/api/garrisons/{playerId}/units/train` | Queue a batch of 1–5 units |
| `POST` | `/api/garrisons/{playerId}/units/heal` | Heal wounded units for half their cost |

### World Map (requires JWT)

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/map?centerX&centerY&radius` | Terrain and occupants for a viewport (radius ≤ 25) |
| `GET` | `/api/map/cell/{x}/{y}` | Cell details, including monster composition |
| `POST` | `/api/marches/{playerId}` | Send an army to a target |

### Battle Reports (requires JWT)

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/reports/{playerId}` | Recent battle reports with per-unit casualty breakdown |
| `POST` | `/api/reports/{playerId}/{reportId}/read` | Mark a report as read |

## ⚙️ Background Jobs

| Job | Schedule | Purpose |
|-----|----------|---------|
| `resource-tick` | every minute | Accumulate production into building buffers |
| `timer-scan` | every minute | Complete due constructions, trainings and marches |
| `monster-spawn` | every 5 minutes | Maintain monster population across the map |

## 🗺️ Roadmap

- [x] Clean Architecture + DDD domain model
- [x] Game logic (resources, buildings, Hangfire ticks)
- [x] REST API, Swagger, ProblemDetails
- [x] Authentication (Identity + JWT with refresh rotation)
- [x] Realtime updates (SignalR)
- [x] CQRS with MediatR
- [x] Core hardening (buffers, storage caps, xUnit tests)
- [x] Timed construction with builder limits
- [x] Zones, building roster, town hall gating
- [x] Units, garrison, training queue
- [x] World map, monsters, marches
- [x] Combat, casualties, hospital, battle reports
- [ ] Production chains (ore → ingots → weapons)
- [ ] Monetization (Stripe, gems, speedups)
- [ ] Game UI (React)
- [ ] Docker + CI/CD