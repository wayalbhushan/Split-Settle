## 1. What we are building and why
SplitSettle is a group expense-splitting app. It is a deliberate port of my earlier project SplitLedger (Django, Django REST Framework, React, PostgreSQL) to C# on .NET 8, with the same React front end. I am applying for a software engineering internship that asks for .NET and C#, basic cloud development, AI tools and generative AI concepts, SQL, React and Azure. I know Java with Spring Boot, Python with Django, and React. I am new to C# and .NET.

The internship lists Azure, but I chose plain hosting to keep this project simple. I will not claim Azure experience from this project.

The goal is a real, working, deployed project that I can explain line by line in an interview. It is not a copy-paste exercise. Every phase must leave me understanding what was built.

## 2. The original system (SplitLedger), summarized
Read the original code in `reference/splitledger-django/` (a read-only copy that is git-ignored) before designing anything. Extract the exact API contract, field names, validation rules and test cases from it. What I remember of it:

- Models: Group (name, created_at), Member (name, group), Expense (group, paid_by, description, amount, created_at), ExpenseSplit (expense, member, share_amount). There is no login system. Members are just named people inside a group.
- Money is exact. The original used Django DecimalField everywhere, never floats.
- Splits can be equal across all members or across any chosen subset of members. Editing or deleting an expense regenerates its splits.
- A rounding-safe split function guarantees the shares always add up exactly to the original amount.
- A settlement algorithm uses a greedy max-heap match of the biggest debtor against the biggest creditor. It settles a group in at most N-1 payments for N members and runs in O(N log N). It does not claim to find the true minimum number of payments.
- Business logic sits in a service layer (`services.py`) that both the form views and the REST API call, with no duplicated logic.
- There were 9 automated tests covering balance computation, the settlement bound, rounding-safe splits and API validation.
- The React front end (Vite, plain JavaScript) has group, member and expense management, a per-member detail view, a group summary strip, and a settlement display written as "PersonA -> PersonB amount".

This is my memory of it. If the real code disagrees, the code wins.

## 3. Target system

### Technology decisions
| Area | Decision |
| --- | --- |
| Language and runtime | C# 12 on .NET 8 (LTS), nullable reference types on |
| API | ASP.NET Core Web API with controllers and attribute routing |
| Data access | Entity Framework Core 8 with the Npgsql (PostgreSQL) provider |
| Database | PostgreSQL 16 in Docker for development, a hosted PostgreSQL for production |
| Tests | xUnit, plus integration tests with WebApplicationFactory |
| API docs | Swagger through Swashbuckle |
| Errors | ProblemDetails (RFC 7807), built into ASP.NET Core |
| Front end | The existing React (Vite) app, kept as is and re-pointed to the new API |
| CI | GitHub Actions running `dotnet build` and `dotnet test` |
| Hosting | Plain host that runs a Docker container, chosen in Phase 8. Config through environment variables |

PostgreSQL was chosen because SplitLedger already used it, it runs in Docker on any operating system, and free hosts offer it. Record the reasoning in `docs/DECISIONS.md`.

### Solution layout
```
SplitSettle/
  SplitSettle.sln
  src/
    SplitSettle.Core/      entities, services (split, balance, settlement), interfaces, no EF dependency
    SplitSettle.Data/      DbContext, entity configuration, migrations
    SplitSettle.Api/       controllers, DTOs, Program.cs, middleware, health check, Swagger
  tests/
    SplitSettle.Tests/     xUnit unit tests and integration tests
  client/                  React (Vite) front end copied from SplitLedger
  docs/                    PROJECT_CONTEXT.md, DECISIONS.md, SPLITLEDGER_API_NOTES.md
  reference/               read-only copy of the Django project (git-ignored)
  docker-compose.yml       PostgreSQL for development
```

### Domain rules
- Money is `decimal`, stored as `numeric(18,2)`. Never `double` or `float`.
- All times are stored in UTC. Npgsql rejects non-UTC DateTime values for `timestamptz` columns.
- Rounding-safe split: for an amount split among k members, compute the base share in whole cents, give each member the base, then hand the leftover cents one at a time to members in a fixed order (by member id). The shares must sum exactly to the amount.
- Net balance per member = total paid minus total share owed.
- Settlement: put creditors (positive balance) in one max-heap and debtors (negative balance, as absolute values) in another. Repeatedly take the largest of each, transfer the smaller of the two amounts, and push the remainder back. Stop when both heaps are empty. .NET's `PriorityQueue` is a min-heap, so a max-heap needs the priority negated. The result has at most N-1 transfers.
- Deleting a member who paid for an expense or has a split is not allowed. Return 409.
- Deleting a group must still work, and removes its members, expenses and splits.

### API contract
Match the original SplitLedger contract wherever practical, using the reference code to get it exactly right. Configure JSON so field names stay snake_case if the React app expects that (`JsonNamingPolicy.SnakeCaseLower` in .NET 8). Record any difference in `docs/DECISIONS.md`. The draft endpoints are:
- `GET /api/groups`, `POST /api/groups`, `GET /api/groups/{id}`
- `POST /api/groups/{id}/members`, `DELETE /api/members/{id}`, `GET /api/members/{id}` (paid, owed, net, and their expenses)
- `GET /api/groups/{id}/expenses`, `POST /api/groups/{id}/expenses` (description, amount, paid-by member, and an optional list of members to split among)
- `PUT /api/expenses/{id}`, `DELETE /api/expenses/{id}` (both regenerate splits)
- `GET /api/groups/{id}/balances`, `GET /api/groups/{id}/settlements`
- `POST /api/groups/{id}/expenses/parse` (the AI feature, Phase 6)
- `GET /health`

Errors use ProblemDetails with these status codes: 400 for validation, 404 for missing records, 409 for conflicts.

### The generative AI feature (Phase 6)
A user types a sentence such as "Dinner 1200 paid by Rahul, split among Rahul, Priya and Amit". The server sends it to an LLM (Gemini or Groq) with the group's member names, asks for strict JSON (description, amount, paid-by name, split-among names), and validates the result: the amount is positive, every name matches a real member, and the response parses. The API returns a draft. The React UI shows the draft and the user confirms before anything is saved. The LLM sits behind an `ILlmClient` interface so tests use a fake. A small evaluation set of 15 to 20 sentences measures how many parse correctly. Only the measured score may be quoted anywhere.

### Non-goals
No login or user accounts, no multi-currency, no real-time sync, no payments integration, no mobile app, no Azure. Do not add libraries such as AutoMapper or MediatR, and no CQRS or repository-per-entity patterns. Keep the design plain enough for me to explain.

## 4. Quality bar
- Every phase ends with a green `dotnet build` and `dotnet test`.
- Unit tests for all money and settlement logic. Integration tests for the API.
- No secrets in the repo. Connection strings and API keys come from configuration.
- Small, meaningful commits, one per logical step, with clear messages.
- A README that explains what it is, how to run it, the architecture, the decisions and the honest limits.

## 5. Working agreement with the assistant
- Work one phase at a time, and stop at the end of each phase. Do not start the next phase.
- Do not widen scope. If something outside the phase looks necessary, say so and wait for my answer.
- Prefer simple, readable code. Use records for DTOs, async/await for I/O, dependency injection through constructors.
- After each phase, print this report:
  1. Files created or changed
  2. How to run it and how to run the tests
  3. Verification results, with the actual command output
  4. Concept notes: each new .NET idea explained in plain words, with the Spring Boot or Django equivalent (for example `DbContext` against a JPA `EntityManager`, `appsettings.json` against `application.properties`, EF migrations against Flyway)
  5. Open questions or decisions that need me
- If something fails, show the error and the fix. Do not hide it.

## 6. What I must be able to explain in an interview
`Program.cs` and the request pipeline, dependency injection lifetimes (singleton, scoped, transient), why `DbContext` is scoped, how EF Core migrations work, async/await, LINQ against raw SQL, the settlement algorithm and why it is at most N-1 and not the minimum, decimal against double for money, ProblemDetails, CORS, configuration and secrets, why the LLM client is behind an interface, how the GitHub Actions workflow runs, and how the Docker image is built and how the hosting is wired together.

## 7. Hosting plan
The API runs as a Docker container on a plain host chosen in Phase 8, after checking that host's current free tier and its limits. The database is a hosted PostgreSQL. The connection string and API keys come from environment variables on the host. EF Core migrations are applied as a deliberate deployment step. The React client stays on Vercel, with CORS set to its URL. The README states that free hosts can sleep, so the first request after idle may be slow.
