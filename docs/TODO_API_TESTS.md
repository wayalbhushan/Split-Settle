# API-Level Tests to Re-implement in Phase 4

The following tests from `ledger/tests.py` in the original Django application are end-to-end API tests from `APILedgerTests`. During Phase 1, their core calculation assertions were ported as pure unit tests against the calculators. In Phase 4, these tests will be implemented as true HTTP integration tests using ASP.NET Core `WebApplicationFactory`.

## 1. test_create_expense_via_api
- Original test: `APILedgerTests.test_create_expense_via_api`
- Phase 1 scope: Unit tested subset split calculation in `PortedDjangoTests.Test6_CreateExpense_SubsetSplitsDivideEquallyAmongChosenMembers`.
- Phase 4 integration target: `POST /api/groups/{id}/expenses/` verifying HTTP 201 Created, database persistence of expense, and generated splits.

## 2. test_get_balances_api
- Original test: `APILedgerTests.test_get_balances_api`
- Phase 1 scope: Unit tested balance aggregation in `PortedDjangoTests.Test7_GetBalances_CalculatesNetBalancesCorrectlyAcrossAllMembers`.
- Phase 4 integration target: `GET /api/groups/{id}/balances/` verifying HTTP 200 OK and response JSON array with `member_id`, `member_name`, and string decimal balances.

## 3. test_get_settlements_api
- Original test: `APILedgerTests.test_get_settlements_api`
- Phase 1 scope: Unit tested settlement transfers in `PortedDjangoTests.Test8_GetSettlements_ProducesDeterministicTransfersToSingleCreditor`.
- Phase 4 integration target: `GET /api/groups/{id}/settlements/` verifying HTTP 200 OK and response JSON array with `from_member_name`, `to_member_name`, and `amount`.

## 4. test_invalid_expense_creation
- Original test: `APILedgerTests.test_invalid_expense_creation`
- Phase 1 scope: Unit tested argument validation in `PortedDjangoTests.Test9_InvalidExpense_ThrowsForNegativeAmountOrEmptySplitMembers`.
- Phase 4 integration target: `POST /api/groups/{id}/expenses/` verifying HTTP 400 Bad Request with ProblemDetails validation error dictionary for negative amounts and empty `split_among` lists.
