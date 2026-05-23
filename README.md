# 🏰 EmpireIdle

Browser-based idle/empire builder game built with **ASP.NET Core (.NET 10)**, **Clean Architecture**, **DDD**, and **React** (frontend in progress).

Build villages, construct buildings, collect resources, and upgrade your empire — even while you're offline.

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
└──────┬───┬───┘  └────────┬──────────┘
       │   │               │
       │   └───────┬───────┘
       │           │
┌──────┴───────────┴───────┐
│       Application        │
│  Use Cases / Services    │
└──────────┬───────────────┘
           │
┌──────────┴───────────────┐
│          Domain           │
│ Entities · Value Objects  │
│ Events · Game Config      │
└──────────────────────────┘
```

**Key patterns:** Clean Architecture, DDD (Aggregates, Value Objects, Domain Events), Repository + Unit of Work, CQRS-ready structure.

## 🛠️ Tech Stack

- **Backend:** ASP.NET Core / .NET 10, EF Core, PostgreSQL
- **Background Jobs:** Hangfire with PostgreSQL storage
- **Architecture:** Clean Architecture, DDD, Repository + UoW
- **API:** REST, Swagger/OpenAPI, ProblemDetails error handling
- **Frontend:** React (planned — Phase 4+)
- **Monetization:** Stripe (planned — Phase 7)
- **License:** AGPL-3.0

## 📦 Projects

| Project | Responsibility |
|---------|---------------|
| `EmpireIdle.Domain` | Entities, Value Objects, Domain Events, Game Config |
| `EmpireIdle.Application` | Use Cases (services), Repository interfaces |
| `EmpireIdle.Infrastructure` | EF Core, PostgreSQL, Repository implementations, DI |
| `EmpireIdle.API` | Controllers, DTOs, Hangfire config, Swagger, Middleware |

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL 17+](https://www.postgresql.org/download/)
- [EF Core CLI Tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) (`dotnet tool install --global dotnet-ef`)

### Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/YOUR_USERNAME/EmpireIdle.git
   cd EmpireIdle
   ```

2. **Create PostgreSQL database and user:**
   ```sql
   CREATE USER empireidle_user WITH PASSWORD 'your_password';
   CREATE DATABASE empireidle OWNER empireidle_user;
   ```

3. **Configure connection string via User Secrets:**
   ```bash
   cd src/EmpireIdle.API
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=empireidle;Username=empireidle_user;Password=your_password"
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

| Method | URL | Description |
|--------|-----|-------------|
| `POST` | `/api/player/register` | Register new player (creates village + wallet) |
| `GET` | `/api/village/{playerId}` | Get village state with buildings and resources |
| `POST` | `/api/village/{playerId}/buildings` | Build a new building |
| `POST` | `/api/village/{playerId}/buildings/upgrade` | Upgrade a building |

### Example: Register a player

```bash
curl -X POST http://localhost:5253/api/player/register \
  -H "Content-Type: application/json" \
  -d '{"username": "player1", "email": "player1@example.com"}'
```

Response:
```json
{
  "playerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

### Example: Get village state

```bash
curl http://localhost:5253/api/village/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

Response:
```json
{
  "id": "...",
  "name": "player1's Village",
  "lastTickAt": "2026-04-22T18:30:00Z",
  "buildings": [
    { "id": "...", "type": "farm", "level": 1, "lastCollectedAt": "..." }
  ],
  "resources": [
    { "resourceType": "gold", "amount": 150 },
    { "resourceType": "wood", "amount": 0 }
  ]
}
```

## 🎮 Game Mechanics

- **Resource Tick:** Hangfire runs every minute, collecting resources from all villages based on building levels
- **Building Upgrade:** Costs `BaseCost × current level` of the configured resource
- **Production Formula:** `Level × BaseProductionPerMinute × elapsed minutes`
- **Reskin Strategy:** Change `game-config.json` to create SpaceIdle, ZombieIdle, etc. — zero code changes

### game-config.json

```json
{
  "GameConfig": {
    "GameName": "EmpireIdle",
    "Buildings": [
      {
        "Key": "farm",
        "DisplayName": "Farm",
        "ProducesResource": "gold",
        "BaseProductionPerMinute": 10,
        "CostResource": "gold",
        "BaseCost": 100
      }
    ]
  }
}
```

## 🗺️ Roadmap

| Phase | Description | Status |
|-------|------------|--------|
| 1 | Clean Architecture + DDD Domain Model | ✅ |
| 2 | EF Core, Hangfire, Game Logic, Building Upgrades | ✅ |
| 3 | REST API Endpoints, Swagger, Error Handling | ✅ |
| 4 | React Frontend (SPA) | ⏳ |
| 5 | SignalR Real-time Updates | ⏳ |
| 6 | Authentication (ASP.NET Identity + JWT) | ⏳ |
| 7 | Stripe Monetization (Gems) | ⏳ |
| 8 | RabbitMQ Inter-service Messaging | ⏳ |
| 9 | Docker + CI/CD | ⏳ |

## 📄 License

This project is licensed under the [AGPL-3.0 License](LICENSE).
