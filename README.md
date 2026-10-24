# SplitSettle

SplitSettle is a group expense-splitting application built with C# on .NET 8, ASP.NET Core, Entity Framework Core, PostgreSQL, and a React front end. It is a clean port and enhancement of the SplitLedger Django application.

## Architecture

```mermaid
graph TD
    Client["React Front End (Vite, SPA)"]
    Api["ASP.NET Core 8 Web API"]
    Core["SplitSettle.Core (Pure Domain & Money Logic)"]
    Data["SplitSettle.Data (EF Core, PostgreSQL)"]
    Db[("PostgreSQL 16 Database")]
    LLM["External LLM Provider (Gemini / Groq)"]

    Client -->|HTTP / JSON (snake_case)| Api
    Api --> Core
    Api --> Data
    Api -.->|AI Parse Sentence| LLM
    Data --> Db
```

## How to Run Everything Locally

### 1. Start the Database

Make sure Docker Desktop is running. Copy `.env.example` to `.env` if not already present, then start the PostgreSQL container:

```powershell
docker compose up -d
```

Confirm the database container is healthy:

```powershell
docker compose ps
```

### 2. Configure Database Connection

Store your local database connection string in user-secrets for `SplitSettle.Api`:

```powershell
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=splitsettle;Username=splitsettle;Password=your_password" --project src/SplitSettle.Api
```

Apply database migrations:

```powershell
dotnet ef database update --project src/SplitSettle.Data --startup-project src/SplitSettle.Api
```

### 3. Run the .NET API

```powershell
dotnet run --project src/SplitSettle.Api --launch-profile http
```

The API starts on `http://localhost:5055`. You can verify health at `http://localhost:5055/health`.

### 4. Run the React Client

In a separate terminal:

```powershell
cd client
npm install
npm run dev
```

Open your browser at `http://localhost:5173`.

### 5. Run the Tests

To run the complete unit, golden comparison, integration, and database rollback test suite:

```powershell
dotnet test
```

## Honest Limits

1. **Free hosting cold starts**: When deployed on free-tier container hosting such as Render, the service spins down after 15 minutes of inactivity. The first request after idle can take 50 to 90 seconds to wake up.
2. **Single currency only**: SplitSettle handles all monetary calculations in a single base currency formatted with two decimal places. Multi-currency conversions and exchange rate lookups are not supported.
3. **No authentication**: Groups and expenses are accessed directly by group identifier without user accounts or password authentication.
4. **AI feature accuracy**: Natural language expense parsing achieves a measured score of 17 / 20 (85.0%) on the evaluation dataset using Gemini (gemini-3.5-flash-lite), as documented in `docs/AI_EVAL.md`.
5. **Single-instance startup migrations**: Automatic schema migrations on application startup are designed for single-replica deployments. Concurrent replicas could encounter race conditions during migration execution.
