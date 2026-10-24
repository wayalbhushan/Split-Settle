# Decisions

Each entry records a choice, the reason, and the date.

## 1. PostgreSQL with the Npgsql provider (2026-10-24)
The original SplitLedger already used PostgreSQL, so data and behaviour can be compared directly. It runs in Docker on any operating system, and free hosting services offer it. The same database is used in development and production, so migrations behave the same in both.

## 2. Controllers instead of minimal APIs (2026-10-24)
This API will have many endpoints grouped by resource, with validation and consistent error responses. Controllers with attribute routing keep each resource in one class and map closely to Spring Boot controllers and Django REST Framework views, which I already know.

## 3. Four projects: Core, Data, Api, Tests (2026-10-24)
Core holds the entities and the money logic (split, balance, settlement) and has no database dependency, so it can be unit tested without a database. Data holds the DbContext and migrations. Api holds controllers and wiring. Tests references Core and Data.

## 4. numeric(18,2) for money (2026-10-24)
Money is stored as decimal in C# and numeric(18,2) in PostgreSQL. Double and float cannot represent amounts like 0.10 exactly, and split totals must add up to the cent.

## 5. UTC for all times (2026-10-24)
All timestamps are stored in UTC in timestamptz columns. Npgsql rejects DateTime values that are not UTC for timestamptz, so the code uses DateTime.UtcNow.

## 6. No Azure for this project (2026-10-24)
The internship post lists Azure, but this project is deployed on a plain Docker host to keep the scope small and the focus on C# and .NET. The project will not be described as an Azure deployment.

## 7. NO ACTION with DEFERRABLE INITIALLY DEFERRED for member foreign keys (2026-10-24)
Deleting a member who is an expense payer or has a split must be prohibited to prevent dangling financial records. If ON DELETE RESTRICT is used, PostgreSQL immediately blocks cascading deletion of an entire group because members are deleted while expenses still reference them during the cascade. Using NO ACTION with DEFERRABLE INITIALLY DEFERRED defers constraint evaluation to the end of the statement: individual member deletion is blocked, while deleting an entire group succeeds because both members and expenses are cleanly removed in the same operation.

## 8. Preserving SplitLedger route hierarchy, trailing slashes, and deterministic tie-breaking (2026-10-24)
The React client relies on nested resource routes with trailing slashes (such as `/api/groups/{id}/expenses/`). To keep the React client completely unmodified, ASP.NET Core controllers will adopt these nested routes and trailing slashes. In addition, the settlement algorithm enforces deterministic tie-breaking by member ID to match the original heap behavior.

## 9. Leftover cents distributed in ascending member ID order (2026-10-24)
When an amount cannot be divided evenly into whole cents, leftover cents are distributed one by one to members in ascending order of their IDs. In the original SplitLedger Django codebase, members were queried using group.members.filter(id__in=split_among_ids) without an explicit order_by clause; while PostgreSQL typically returned rows by ascending primary key in simple queries, it was not strictly guaranteed. Explicitly sorting by member ID in SplitSettle ensures rounding distribution is completely deterministic and reproducible across all platforms.

## 10. Integration test isolation with dedicated database and unique group names (2026-10-24)
Integration tests use a separate database named splitsettle_test on the same PostgreSQL container. SplitSettleApiFactory inherits WebApplicationFactory<Program> and applies migrations on startup via Database.MigrateAsync(), creating the test database if it does not already exist. To ensure test isolation and allow concurrent or out-of-order test execution without state pollution, tests never clear or delete other tests' rows. Instead, each test creates its own group with a globally unique name using Guid.NewGuid().

## 11. Optional startup migrations via Database:MigrateOnStartup (2026-10-24)
Applying migrations at application startup when Database:MigrateOnStartup is enabled provides a simple, self-contained deployment model for single-container hosting environments like Render or local Docker Compose. The trade-off is that startup migrations are not safe when running multiple replicas concurrently, because multiple instances might attempt schema modifications simultaneously. For SplitSettle's single-instance deployment, this trade-off is acceptable and eliminates the need for separate migration release pipelines.

