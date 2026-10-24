# SplitLedger API and Architecture Notes

This document provides a comprehensive reference of the original SplitLedger Django/DRF codebase located in `reference/splitledger-django/` and notes dependencies in `client/`.

---

## 1. Endpoints Specification

All API routes in the Django backend are registered under `api/` (via `splitledger/urls.py` including `ledger/urls.py`). In DRF and the React client, trailing slashes are consistently used.

### `GET /api/groups/`
- **Method**: `GET`
- **URL**: `/api/groups/`
- **Request Body**: None
- **Response**: `200 OK`
  ```json
  [
    {
      "id": 1,
      "name": "Trip to Goa",
      "created_at": "2026-08-25T10:00:00Z",
      "member_count": 3
    }
  ]
  ```
- **Ordering**: `-created_at` (newest first)
- **Status Codes**: `200 OK`

---

### `POST /api/groups/`
- **Method**: `POST`
- **URL**: `/api/groups/`
- **Request Body**:
  ```json
  {
    "name": "Trip to Goa"
  }
  ```
- **Validation**:
  - `name`: Required, string, max length 255.
- **Response**:
  - Success: `201 Created`
    ```json
    {
      "id": 1,
      "name": "Trip to Goa",
      "created_at": "2026-08-25T10:00:00Z",
      "member_count": 0
    }
    ```
  - Validation Error: `400 Bad Request`
    ```json
    {
      "name": ["This field is required."]
    }
    ```

---

### `GET /api/groups/<int:group_id>/`
- **Method**: `GET`
- **URL**: `/api/groups/<int:group_id>/`
- **Request Body**: None
- **Response**:
  - Success: `200 OK`
    ```json
    {
      "id": 1,
      "name": "Trip to Goa",
      "created_at": "2026-08-25T10:00:00Z",
      "member_count": 2,
      "members": [
        { "id": 1, "name": "Aarav", "group": 1 },
        { "id": 2, "name": "Bhavya", "group": 1 }
      ]
    }
    ```
  - Not Found: `404 Not Found`

---

### `GET /api/groups/<int:group_id>/members/`
- **Method**: `GET`
- **URL**: `/api/groups/<int:group_id>/members/`
- **Request Body**: None
- **Response**:
  - Success: `200 OK`
    ```json
    [
      { "id": 1, "name": "Aarav", "group": 1 },
      { "id": 2, "name": "Bhavya", "group": 1 }
    ]
    ```
  - Not Found: `404 Not Found` if group does not exist.

---

### `POST /api/groups/<int:group_id>/members/`
- **Method**: `POST`
- **URL**: `/api/groups/<int:group_id>/members/`
- **Request Body**:
  ```json
  {
    "name": "Chirag"
  }
  ```
- **Validation**:
  - `name`: Required, max length 255.
- **Response**:
  - Success: `201 Created`
    ```json
    {
      "id": 3,
      "name": "Chirag",
      "group": 1
    }
    ```
  - Validation Error: `400 Bad Request`
    ```json
    {
      "name": ["This field is required."]
    }
    ```
  - Not Found: `404 Not Found` if group does not exist.

---

### `GET /api/groups/<int:group_id>/members/<int:member_id>/`
- **Method**: `GET`
- **URL**: `/api/groups/<int:group_id>/members/<int:member_id>/`
- **Request Body**: None
- **Response**:
  - Success: `200 OK`
    ```json
    {
      "member_id": 1,
      "member_name": "Aarav",
      "balance": "50.00",
      "paid_expenses": [
        {
          "id": 10,
          "group": 1,
          "description": "Dinner",
          "amount": "100.00",
          "paid_by": 1,
          "paid_by_name": "Aarav",
          "split_among": [1, 2],
          "created_at": "2026-08-25T12:00:00Z"
        }
      ],
      "owed_expenses": [
        {
          "expense_id": 11,
          "description": "Lunch",
          "share_amount": "25.00",
          "total_amount": "50.00",
          "date": "2026-08-25T14:00:00Z"
        }
      ]
    }
    ```
  - Not Found: `404 Not Found` if group or member does not exist or member does not belong to group.

---

### `GET /api/groups/<int:group_id>/expenses/`
- **Method**: `GET`
- **URL**: `/api/groups/<int:group_id>/expenses/`
- **Request Body**: None
- **Ordering**: `-created_at` (newest first)
- **Response**:
  - Success: `200 OK`
    ```json
    [
      {
        "id": 10,
        "group": 1,
        "description": "Dinner",
        "amount": "150.00",
        "paid_by": 1,
        "paid_by_name": "Aarav",
        "split_among": [1, 2],
        "created_at": "2026-08-25T12:00:00Z"
      }
    ]
    ```
  - Not Found: `404 Not Found` if group does not exist.

---

### `POST /api/groups/<int:group_id>/expenses/`
- **Method**: `POST`
- **URL**: `/api/groups/<int:group_id>/expenses/`
- **Request Body**:
  ```json
  {
    "description": "Dinner",
    "amount": "150.00",
    "paid_by": 1,
    "split_among": [1, 2]
  }
  ```
- **Validation**:
  - `amount`: Required. Must parse to decimal >= 0.01.
    - Missing: `{"amount": ["This field is required."]}`
    - Invalid numeric: `{"amount": ["A valid number is required."]}`
    - Less than 0.01: `{"amount": ["Ensure this value is greater than or equal to 0.01."]}`
  - `split_among`: Required non-empty array of integer member IDs.
    - Missing / empty: `{"split_among": ["You must select at least one member to split the expense with."]}`
    - Any member ID not found in group: `{"split_among": ["One or more members selected are invalid or not in this group."]}`
  - `paid_by`: Required integer member ID belonging to group.
    - Missing or not in group: `{"paid_by": ["Valid group member ID is required."]}`
- **Side Effects**:
  - Creates `Expense` record.
  - Computes equal shares using `split_equally(amount, len(split_members))`.
  - Creates `ExpenseSplit` records for each member in `split_members` (ordered by member ID).
- **Response**:
  - Success: `201 Created` with serialized expense object (including `paid_by_name` and `split_among`).
  - Errors: `400 Bad Request` or `404 Not Found`.

---

### `PUT /api/groups/<int:group_id>/expenses/<int:expense_id>/`
- **Method**: `PUT`
- **URL**: `/api/groups/<int:group_id>/expenses/<int:expense_id>/`
- **Request Body**:
  ```json
  {
    "description": "Dinner buffet",
    "amount": "180.00",
    "paid_by": 1,
    "split_among": [1, 2, 3]
  }
  ```
- **Validation**: Identical to `POST /api/groups/<int:group_id>/expenses/`.
- **Side Effects**:
  - Updates expense fields (`description`, `amount`, `paid_by`).
  - Deletes all existing splits: `expense.splits.all().delete()`.
  - Re-calculates and re-creates `ExpenseSplit` records via `split_equally`.
- **Response**:
  - Success: `200 OK` with updated serialized expense object.
  - Errors: `400 Bad Request` or `404 Not Found`.

---

### `DELETE /api/groups/<int:group_id>/expenses/<int:expense_id>/`
- **Method**: `DELETE`
- **URL**: `/api/groups/<int:group_id>/expenses/<int:expense_id>/`
- **Request Body**: None
- **Side Effects**: Deletes expense and its splits (via cascade).
- **Response**:
  - Success: `204 No Content`
  - Not Found: `404 Not Found` if group or expense not found.

---

### `GET /api/groups/<int:group_id>/balances/`
- **Method**: `GET`
- **URL**: `/api/groups/<int:group_id>/balances/`
- **Request Body**: None
- **Response**:
  - Success: `200 OK`
    ```json
    [
      {
        "member_id": 1,
        "member_name": "Aarav",
        "balance": "60.00"
      },
      {
        "member_id": 2,
        "member_name": "Bhavya",
        "balance": "-30.00"
      },
      {
        "member_id": 3,
        "member_name": "Chirag",
        "balance": "-30.00"
      }
    ]
    ```
  - Note: `balance` is formatted as a decimal string (positive = owed money, negative = owes money).
  - Status Codes: `200 OK`, `404 Not Found`.

---

### `GET /api/groups/<int:group_id>/settlements/`
- **Method**: `GET`
- **URL**: `/api/groups/<int:group_id>/settlements/`
- **Request Body**: None
- **Response**:
  - Success: `200 OK`
    ```json
    [
      {
        "from_member_id": 2,
        "from_member_name": "Bhavya",
        "to_member_id": 1,
        "to_member_name": "Aarav",
        "amount": "30.00"
      },
      {
        "from_member_id": 3,
        "from_member_name": "Chirag",
        "to_member_id": 1,
        "to_member_name": "Aarav",
        "amount": "30.00"
      }
    ]
    ```
  - Note: `amount` is formatted as a positive decimal string.
  - Status Codes: `200 OK`, `404 Not Found`.

---

## 2. Field Names and JSON Casing

- **Casing Convention**: Strict `snake_case` across all requests and responses.
- **Key Field Names**:
  - Group: `id`, `name`, `created_at`, `member_count`, `members`
  - Member: `id`, `name`, `group`
  - Expense: `id`, `group`, `description`, `amount`, `paid_by`, `paid_by_name`, `split_among`, `created_at`
  - Balance: `member_id`, `member_name`, `balance`
  - Settlement: `from_member_id`, `from_member_name`, `to_member_id`, `to_member_name`, `amount`
  - Member Detail: `member_id`, `member_name`, `balance`, `paid_expenses`, `owed_expenses`
  - Owed Expense Item: `expense_id`, `description`, `share_amount`, `total_amount`, `date` (Note: `date` corresponds to `split.expense.created_at`).

---

## 3. Money Fields and Rounding Rules

1. **Storage**:
   - `Expense.amount`: Django `DecimalField(max_digits=10, decimal_places=2)`.
   - `ExpenseSplit.share_amount`: Django `DecimalField(max_digits=10, decimal_places=2)`.
2. **String Serialization**:
   - In DRF serializers and endpoints, money values are serialized as fixed-point 2-decimal strings (e.g. `"150.00"`, `"-30.00"`).
3. **Rounding in `split_equally`**:
   - Amount is converted to integer cents: `cents = int(amount * 100)`.
   - Base cents per member: `base_share_cents = cents // member_count`.
   - Leftover cents: `remainder = cents % member_count`.
   - For member index $i \in [0, 	ext{member\_count}-1]$:
     $$	ext{share\_cents} = 	ext{base\_share\_cents} + (1 	ext{ if } i < 	ext{remainder} 	ext{ else } 0)$$
     $$	ext{share} = rac{	ext{Decimal(share\_cents)}}{100}$$
   - When splitting among members, the order of iteration is by Member ID ascending. Thus, the first `remainder` members in ID order receive $+0.01$.
   - Sum of shares is guaranteed to equal the original amount exactly ($\sum 	ext{shares} = 	ext{amount}$).
4. **Threshold / Epsilon**:
   - `EPSILON = Decimal('0.001')` is used in settlement computation to filter out settled zero-balances.

---

## 4. The Split and Settlement Functions

### Split Function: `split_equally`
- **File**: `ledger/services.py` (lines 6-23)
- **Signature**: `split_equally(amount: Decimal, member_count: int) -> List[Decimal]`
- **Description**:
  Splits an exact currency amount into `member_count` shares such that every share is a valid cent amount and the sum of all shares equals the original amount exactly. It converts the amount to whole cents, performs integer division to find the base share for each member, and distributes any leftover cents ($0 \le 	ext{remainder} < 	ext{member\_count}$) one cent at a time to the first members in the list.

### Settlement Function: `compute_settlements`
- **File**: `ledger/services.py` (lines 41-80)
- **Signature**: `compute_settlements(balances: Dict[Member, Decimal]) -> List[Tuple[Member, Member, Decimal]]`
- **Description**:
  Calculates a sequence of peer-to-peer payments to settle all debts in at most $N-1$ transactions for $N$ members with non-zero balances. It places all members who owe money into a debtor max-heap (implemented as a Python min-heap storing negative debts) and all members who are owed money into a creditor max-heap (min-heap storing negated credits), using member ID as a tie-breaker. It repeatedly pops the largest debtor and largest creditor, creates a transaction for $\min(	ext{debt}, 	ext{credit})$, and pushes any remaining balance back onto the respective heap until both are cleared. Runs in $O(N \log N)$ time.

---

## 5. Summary of the 9 Tests in `ledger/tests.py`

1. `test_simple_case_two_members`: Verifies net balance calculation (50.00 and -50.00) and that a single 50.00 settlement transfer resolves a 2-member equal split of 100.00.
2. `test_multi_member_case`: Verifies a 4-member group with multiple overlapping expenses, checking that balances sum to zero, settlements do not exceed $N-1$ transactions, and applying settlements brings all balances to zero within 0.001 epsilon.
3. `test_rounding_case_split_equally`: Verifies that splitting 100.00 among 3 members yields exactly `[33.34, 33.33, 33.33]`, summing exactly to 100.00.
4. `test_already_settled_group`: Verifies that equal offsetting reciprocal debts result in all zero balances and produce 0 settlement transactions.
5. `test_single_member_group`: Verifies that a solo group member who pays for their own 10.00 expense has a 0.00 balance and produces 0 settlement transactions.
6. `test_create_expense_via_api`: Verifies POST `/api/groups/<id>/expenses/` succeeds (201 Created), creates the expense record, and creates equal 75.00 splits for the chosen subset of 2 members out of 3.
7. `test_get_balances_api`: Verifies GET `/api/groups/<id>/balances/` returns HTTP 200 with correct string balance values for all members (e.g. `"60.00"`, `"-30.00"`).
8. `test_get_settlements_api`: Verifies GET `/api/groups/<id>/settlements/` returns HTTP 200 with 2 settlement transfers matching the expected debtor, creditor, and amount strings.
9. `test_invalid_expense_creation`: Verifies POST `/api/groups/<id>/expenses/` returns 400 Bad Request with field errors when amount is negative and when `split_among` list is empty.

---

## 6. React Client Dependencies on API Response Shapes

| Frontend File | Endpoint Called | Expected Fields / Data Structure | Notes / Error Handling |
| :--- | :--- | :--- | :--- |
| `frontend/src/api.js` | All | Returns `response.json()`. On error, attaches `response.json()` to `err.data`. | Key helper for all API requests. |
| `frontend/src/pages/GroupList.jsx` | `GET /groups/` | Array of `{ id, name, member_count }` | Renders list with member count. |
| `frontend/src/pages/GroupDetail.jsx` | `GET /groups/${id}/` | `{ name, members: [{ id, name, group }] }` | Displays group name and member count. |
| `frontend/src/pages/GroupDetail.jsx` | `GET /groups/${id}/expenses/` | Array of `{ id, created_at, description, paid_by_name, amount }` | Uses `parseFloat(e.amount)` to compute total spent; formats `created_at`. |
| `frontend/src/pages/GroupDetail.jsx` | `GET /groups/${id}/balances/` | Array of `{ member_id, member_name, balance }` | Uses `parseFloat(b.balance)` to style positive/negative balances. |
| `frontend/src/pages/GroupDetail.jsx` | `GET /groups/${id}/settlements/` | Array of `{ from_member_name, to_member_name, amount }` | Formats settlement text `from -> to ₹amount`. |
| `frontend/src/pages/GroupDetail.jsx` | `DELETE /groups/${id}/expenses/${id}/` | `204 No Content` | Deletes expense and triggers page refresh. |
| `frontend/src/pages/MemberDetail.jsx` | `GET /groups/${id}/members/${member_id}/` | `{ member_name, balance, paid_expenses, owed_expenses }` | Uses `paid_expenses[].created_at` and `owed_expenses[].date`. |
| `frontend/src/pages/ExpenseCreate.jsx` | `GET /groups/${id}/members/` | Array of `{ id, name }` | Populates dropdown and checkboxes. |
| `frontend/src/pages/ExpenseCreate.jsx` | `POST /groups/${id}/expenses/` | Submits `{ description, amount, paid_by, split_among }` | Binds validation errors to `fieldErrors.description`, `fieldErrors.amount`, `fieldErrors.paid_by`, `fieldErrors.split_among` using `.join(', ')`. |
| `frontend/src/pages/ExpenseEdit.jsx` | `GET /groups/${id}/expenses/` | Array of `{ id, description, amount, paid_by, split_among }` | Finds expense by `e.id.toString() === expense_id`. Expects `paid_by` as ID and `split_among` as array of IDs. |
| `frontend/src/pages/ExpenseEdit.jsx` | `PUT /groups/${id}/expenses/${id}/` | Submits `{ description, amount, paid_by, split_among }` | Binds validation errors to `fieldErrors` mapping. |
| `frontend/src/pages/GroupCreate.jsx` | `POST /groups/` | Submits `{ name }`, receives `{ id, ... }` | If `initialMembers` provided, fires subsequent `POST /groups/${id}/members/` calls. |
| `frontend/src/pages/MemberCreate.jsx` | `POST /groups/${id}/members/` | Submits `{ name }` | Binds validation errors to `fieldErrors.name`. |

---

## 7. Discrepancies: Real Code vs. `docs/PROJECT_CONTEXT.md` Section 2/3

Where the real code and the summary in Part A of `PROJECT_CONTEXT.md` disagree, **the real code wins**. The differences are:

1. **Expense Detail & Edit/Delete Routes**:
   - *PROJECT_CONTEXT.md*: Lists flat endpoints: `PUT /api/expenses/{id}` and `DELETE /api/expenses/{id}`.
   - *Real Code*: Uses nested endpoints: `PUT /api/groups/<group_id>/expenses/<expense_id>/` and `DELETE /api/groups/<group_id>/expenses/<expense_id>/`.
   - *Impact*: The React client (`ExpenseEdit.jsx`, `GroupDetail.jsx`) calls the nested endpoints with trailing slashes.
2. **Member Detail Route**:
   - *PROJECT_CONTEXT.md*: Lists `GET /api/members/{id}`.
   - *Real Code*: Uses nested endpoint: `GET /api/groups/<group_id>/members/<member_id>/`.
   - *Impact*: The React client (`MemberDetail.jsx`) calls `/groups/${id}/members/${member_id}/`.
3. **Member Delete Endpoint**:
   - *PROJECT_CONTEXT.md*: Lists `DELETE /api/members/{id}`.
   - *Real Code*: Does **not** implement a member delete endpoint in Django views or URLs. The React UI also does not have a member delete button.
4. **Group Delete Endpoint**:
   - *PROJECT_CONTEXT.md*: Mentions group deletion and cascade rules.
   - *Real Code*: Does **not** implement a group delete API endpoint.
5. **Error Format**:
   - *PROJECT_CONTEXT.md*: Specifies RFC 7807 ProblemDetails for ASP.NET Core errors.
   - *Real Code*: DRF returns dictionary with field names mapping to arrays of strings: `{"<field_name>": ["<error message>"]}`.
   - *Impact*: In Phase 2/5, we must ensure ASP.NET Core ProblemDetails `errors` dictionary matches this structure so React's `err.data[field].join(', ')` handles it without modification, or map it accordingly.
6. **Trailing Slashes**:
   - *PROJECT_CONTEXT.md*: Shows endpoints without trailing slashes.
   - *Real Code*: Django DRF expects trailing slashes (`/api/groups/`, etc.) and the React client sends them.
7. **Member Detail Response Shape**:
   - *PROJECT_CONTEXT.md*: States "paid, owed, net, and their expenses".
   - *Real Code*: Shape is explicitly `{ member_id, member_name, balance, paid_expenses: [...], owed_expenses: [...] }`. In `owed_expenses`, the date is named `date` (not `created_at`).
8. **Group Detail Response Shape**:
   - *PROJECT_CONTEXT.md*: Summary doesn't specify nested members.
   - *Real Code*: `GET /api/groups/<group_id>/` returns `{ id, name, created_at, member_count, members: [...] }`.
9. **Numeric Precision in Database**:
   - *PROJECT_CONTEXT.md*: Target is `numeric(18,2)`.
   - *Real Code*: Django models use `DecimalField(max_digits=10, decimal_places=2)`.
