# 🏰 EmpireIdle

Browser-based idle/empire builder game built with **ASP.NET Core (.NET 10)**, **Clean Architecture**, **DDD**, and **React** (frontend planned).

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
│ Identity/JWT │  │ Auth, Middleware   │
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

**Key patterns:** Clean Architecture, DDD (Aggregates, Value Objects, Domain Events), Repository + Unit of Work, CQRS with MediatR.

## 🛠️ Tech Stack

- **Backend:** ASP.NET Core / .NET 10, EF Core, PostgreSQL
- **Auth:** ASP.NET Identity, JWT with refresh token rotation
- **CQRS:** MediatR (commands/queries, pipeline behaviors for logging + FluentValidation)
- **Realtime:** SignalR (per-player groups, JWT auth over WebSocket)
- **Background Jobs:** Hangfire with PostgreSQL storage
- **Architecture:** Clean Architecture, DDD, Repository + UoW
- **API:** REST, Swagger/OpenAPI, ProblemDetails error handling
- **Frontend:** React 19 + TypeScript + Vite + Tailwind (in progress — login page implemented, game UI pending)
- **Monetization:** Stripe (planned)
- **License:** AGPL-3.0

## 📦 Projects

| Project | Responsibility |
|---------|---------------|
| `EmpireIdle.Domain` | Entities, Value Objects, Domain Events, Game Config |
| `EmpireIdle.Application` | Use Cases (services), Repository interfaces |
| `EmpireIdle.Infrastructure` | EF Core, PostgreSQL, Identity/JWT, Repository implementations, DI |
| `EmpireIdle.API` | Controllers, DTOs, Auth, Hangfire config, Swagger, Middleware, SignalR Hub |
| `EmpireIdle.Web` | React + TypeScript frontend (login implemented; village dashboard, SignalR client pending) |

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
| `POST` | `/api/auth/register` | Register player (IdentityUser + Village + Wallet), returns JWT |
| `POST` | `/api/auth/login` | Authenticate, returns access + refresh tokens |
| `POST` | `/api/auth/refresh` | Rotate tokens (with reuse detection) |

### Game (requires JWT)

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/village/{playerId}` | Get village state with buildings and resources |
| `POST` | `/api/village/{playerId}/buildings` | Build a new building |
| `POST` | `/api/village/{playerId}/buildings/upgrade` | Upgrade a building |

### Example: Register

```bash
curl -X POST http://localhost:5253/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username": "player1", "email": "player1@example.com", "password": "Test12345"}'
```

Response:
```json
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "Xk7pQ9z...",
  "playerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
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

## 🔐 Authentication

JWT-based auth with refresh token rotation:
- Access tokens (short-lived, configurable lifetime)
- Refresh tokens stored in PostgreSQL, single-use with rotation
- **Reuse detection:** using a revoked refresh token revokes all user sessions (theft protection)
- Game endpoints protected with `[Authorize]`

## 🗺️ Roadmap

| Phase | Description | Status |
|-------|------------|--------|
| 1 | Clean Architecture + DDD Domain Model | ✅ |
| 2 | EF Core, Hangfire, Game Logic, Building Upgrades | ✅ |
| 3 | REST API Endpoints, Swagger, Error Handling | ✅ |
| 4 | Authentication (ASP.NET Identity + JWT) | ✅ |
| 5 | Realtime updates (SignalR) | ✅ |
| 6 | CQRS + MediatR | ✅ |
| 7 | Frontend (React + TypeScript) | 🔄 In progress |
| 8 | Gems Economy (Quests, Rewards, Events) | ⏳ |
| 9 | Monetization (Stripe) | ⏳ |
| 10 | Chat System (SignalR) | ⏳ |
| 11 | Player Trading (RabbitMQ) | ⏳ |
| 12 | Docker + CI/CD | ⏳ |

## 📄 License

This project is licensed under the [AGPL-3.0 License](LICENSE). 