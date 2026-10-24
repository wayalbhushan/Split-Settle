# Test Coverage Matrix

This matrix maps every REST API endpoint to the automated tests verifying each HTTP status code.

| Endpoint | Success | 400 Bad Request | 404 Not Found | 409 Conflict |
| :--- | :--- | :--- | :--- | :--- |
| `GET /api/groups/` | `GetGroups_ReturnsAllGroupsAndSupportsTrailingSlash` | N/A | N/A | N/A |
| `POST /api/groups/` | `PostGroup_WithValidName_Returns201CreatedAndCorrectSnakeCaseJson` | `PostGroup_WithEmptyOrWhitespaceName_Returns400BadRequestWithProblemDetails`, `PostGroup_WithNameExceeding255Chars_Returns400BadRequest` | N/A | N/A |
| `GET /api/groups/{id}/` | `GetGroupById_ExistingGroup_Returns200WithMembers` | N/A | `GetGroupById_NonExistentGroup_Returns404NotFoundWithProblemDetails` | N/A |
| `GET /api/groups/{groupId}/members/` | `GetGroupById_ExistingGroup_Returns200WithMembers` | N/A | `GetGroupById_NonExistentGroup_Returns404NotFoundWithProblemDetails` | N/A |
| `POST /api/groups/{groupId}/members/` | `PostMember_DuplicateNameInSameGroup_Returns409ConflictWithProblemDetails` | `PostMember_WithEmptyName_Returns400BadRequest` | `PostMember_ToNonExistentGroup_Returns404NotFound` | `PostMember_DuplicateNameInSameGroup_Returns409ConflictWithProblemDetails` |
| `GET /api/groups/{groupId}/members/{memberId}/` | `GetMemberDetail_ExistingMemberWithoutExpenses_ReturnsZeroBalanceAndEmptyExpenses`, `DjangoApiTest_CreateExpenseViaApi_CreatesExpenseAndSubsetsSplits` | N/A | `GetMemberDetail_NonExistentMemberOrWrongGroup_Returns404NotFound` | N/A |
| `GET /api/groups/{groupId}/expenses/` | `DeleteExpense_RemovesExpenseAndReturns204NoContent` | N/A | `ExpenseEndpoints_NonExistentGroupOrExpense_Returns404NotFound` | N/A |
| `POST /api/groups/{groupId}/expenses/` | `PostExpense_WithValidData_CreatesExpenseAndSplits`, `DjangoApiTest_CreateExpenseViaApi_CreatesExpenseAndSubsetsSplits` | `PostExpense_InvalidInputs_Returns400WithProblemDetails`, `DjangoApiTest_InvalidExpenseCreation_Returns400WithFieldErrors` | `ExpenseEndpoints_NonExistentGroupOrExpense_Returns404NotFound` | N/A |
| `PUT /api/groups/{groupId}/expenses/{expenseId}/` | `PutExpense_UpdatesExpenseAndRegeneratesSplits`, `PutExpense_RegeneratesSplitsCorrectlyInDatabase` | `PutExpense_WithInvalidData_Returns400BadRequest` | `ExpenseEndpoints_NonExistentGroupOrExpense_Returns404NotFound` | N/A |
| `DELETE /api/groups/{groupId}/expenses/{expenseId}/` | `DeleteExpense_RemovesExpenseAndReturns204NoContent`, `DeleteExpense_RemovesSplitsFromDatabase` | N/A | `ExpenseEndpoints_NonExistentGroupOrExpense_Returns404NotFound` | N/A |
| `GET /api/groups/{groupId}/balances/` | `GetBalancesAndSettlements_CalculatesCorrectBalancesAndTransfers`, `DjangoApiTest_GetBalancesApi_ReturnsCorrectStringBalances`, `GoldenDataset1_ThreeMembersWithRoundingAndMixedPayers_MatchesDjangoExactly` | N/A | `ExpenseEndpoints_NonExistentGroupOrExpense_Returns404NotFound` | N/A |
| `GET /api/groups/{groupId}/settlements/` | `GetBalancesAndSettlements_CalculatesCorrectBalancesAndTransfers`, `DjangoApiTest_GetSettlementsApi_ReturnsExpectedTransfers`, `GoldenDataset1_ThreeMembersWithRoundingAndMixedPayers_MatchesDjangoExactly` | N/A | `ExpenseEndpoints_NonExistentGroupOrExpense_Returns404NotFound` | N/A |

### Additional Database and Business Logic Guarantees
- No partial data on failed expense creation: `PostExpense_WhenValidationFails_LeavesNoPartialDataInDatabase`
- Split regeneration verified at database level: `PutExpense_RegeneratesSplitsCorrectlyInDatabase`
- Split cleanup on expense deletion verified at database level: `DeleteExpense_RemovesSplitsFromDatabase`
- Cascading deletion of group, members, expenses, and splits: `DeleteGroup_ViaDbContext_CascadesAndRemovesMembersExpensesAndSplits`
- Deletion of expense payer blocked by database foreign key: `DeletePayerMember_ViaDbContext_IsBlockedByDatabaseForeignKeyConstraint`
