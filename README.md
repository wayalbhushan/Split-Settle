# SplitSettle

SplitSettle is a group expense-splitting app built with C# on .NET 8, ASP.NET Core, Entity Framework Core and PostgreSQL, with a React front end. It is a port of my earlier Django project, SplitLedger.

## Status

Phase 0 is done: repository, solution, domain entities, database and first migration, plus a health endpoint. Nothing else is built yet.

## Run the database

Copy `.env.example` to `.env`, set a password, then start PostgreSQL:

```
docker compose up -d
```

## More

The scope and decisions are in `docs/PROJECT_CONTEXT.md` and `docs/DECISIONS.md`.
