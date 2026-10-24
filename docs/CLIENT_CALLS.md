# React Client API Calls Verification

This document lists every API call made by the React client (`client/src/`), its method, endpoint, payload, and the verified response shape from the .NET Web API.

## 1. Group List
- File: `client/src/pages/GroupList.jsx`
- Method: `GET`
- URL: `/api/groups/`
- Request Payload: None
- Response Shape (`200 OK`):
```json
[
  {
    "id": 1,
    "name": "Trip to Goa",
    "created_at": "2026-10-24T03:08:21Z",
    "member_count": 2
  }
]
```

## 2. Group Create
- File: `client/src/pages/GroupCreate.jsx`
- Method: `POST`
- URL: `/api/groups/`
- Request Payload:
```json
{
  "name": "Weekend Trip"
}
```
- Response Shape (`201 Created`):
```json
{
  "id": 2,
  "name": "Weekend Trip",
  "created_at": "2026-10-24T03:15:00Z",
  "member_count": 0
}
```

## 3. Member Create (via Group Create initial members or MemberCreate form)
- File: `client/src/pages/GroupCreate.jsx`, `client/src/pages/MemberCreate.jsx`
- Method: `POST`
- URL: `/api/groups/{id}/members/`
- Request Payload:
```json
{
  "name": "Aarav"
}
```
- Response Shape (`201 Created`):
```json
{
  "id": 1,
  "name": "Aarav",
  "group": 2
}
```

## 4. Group Detail
- File: `client/src/pages/GroupDetail.jsx`
- Method: `GET`
- URL: `/api/groups/{id}/`
- Request Payload: None
- Response Shape (`200 OK`):
```json
{
  "id": 2,
  "name": "Weekend Trip",
  "created_at": "2026-10-24T03:15:00Z",
  "member_count": 2,
  "members": [
    { "id": 1, "name": "Aarav", "group": 2 },
    { "id": 2, "name": "Bhavya", "group": 2 }
  ]
}
```

## 5. Group Members List (for forms and dropdowns)
- File: `client/src/pages/ExpenseCreate.jsx`, `client/src/pages/ExpenseEdit.jsx`
- Method: `GET`
- URL: `/api/groups/{id}/members/`
- Request Payload: None
- Response Shape (`200 OK`):
```json
[
  { "id": 1, "name": "Aarav", "group": 2 },
  { "id": 2, "name": "Bhavya", "group": 2 }
]
```

## 6. Group Expenses List
- File: `client/src/pages/GroupDetail.jsx`, `client/src/pages/ExpenseEdit.jsx`
- Method: `GET`
- URL: `/api/groups/{id}/expenses/`
- Request Payload: None
- Response Shape (`200 OK`):
```json
[
  {
    "id": 10,
    "group": 2,
    "description": "Dinner",
    "amount": "150.00",
    "paid_by": 1,
    "paid_by_name": "Aarav",
    "split_among": [1, 2],
    "created_at": "2026-10-24T03:18:00Z"
  }
]
```

## 7. Group Balances
- File: `client/src/pages/GroupDetail.jsx`
- Method: `GET`
- URL: `/api/groups/{id}/balances/`
- Request Payload: None
- Response Shape (`200 OK`):
```json
[
  { "member_id": 1, "member_name": "Aarav", "balance": "75.00" },
  { "member_id": 2, "member_name": "Bhavya", "balance": "-75.00" }
]
```

## 8. Group Settlements
- File: `client/src/pages/GroupDetail.jsx`
- Method: `GET`
- URL: `/api/groups/{id}/settlements/`
- Request Payload: None
- Response Shape (`200 OK`):
```json
[
  {
    "from_member_id": 2,
    "from_member_name": "Bhavya",
    "to_member_id": 1,
    "to_member_name": "Aarav",
    "amount": "75.00"
  }
]
```

## 9. Create Expense
- File: `client/src/pages/ExpenseCreate.jsx`
- Method: `POST`
- URL: `/api/groups/{id}/expenses/`
- Request Payload:
```json
{
  "description": "Dinner",
  "amount": "150.00",
  "paid_by": 1,
  "split_among": [1, 2]
}
```
- Response Shape (`201 Created`):
```json
{
  "id": 10,
  "group": 2,
  "description": "Dinner",
  "amount": "150.00",
  "paid_by": 1,
  "paid_by_name": "Aarav",
  "split_among": [1, 2],
  "created_at": "2026-10-24T03:18:00Z"
}
```

## 10. Update Expense
- File: `client/src/pages/ExpenseEdit.jsx`
- Method: `PUT`
- URL: `/api/groups/{id}/expenses/{expense_id}/`
- Request Payload:
```json
{
  "description": "Dinner buffet",
  "amount": "180.00",
  "paid_by": 1,
  "split_among": [1, 2]
}
```
- Response Shape (`200 OK`):
```json
{
  "id": 10,
  "group": 2,
  "description": "Dinner buffet",
  "amount": "180.00",
  "paid_by": 1,
  "paid_by_name": "Aarav",
  "split_among": [1, 2],
  "created_at": "2026-10-24T03:18:00Z"
}
```

## 11. Delete Expense
- File: `client/src/pages/GroupDetail.jsx`
- Method: `DELETE`
- URL: `/api/groups/{id}/expenses/{expense_id}/`
- Request Payload: None
- Response Shape (`204 No Content`): Empty body

## 12. Member Detail
- File: `client/src/pages/MemberDetail.jsx`
- Method: `GET`
- URL: `/api/groups/{id}/members/{member_id}/`
- Request Payload: None
- Response Shape (`200 OK`):
```json
{
  "member_id": 1,
  "member_name": "Aarav",
  "balance": "75.00",
  "paid_expenses": [
    {
      "id": 10,
      "group": 2,
      "description": "Dinner",
      "amount": "150.00",
      "paid_by": 1,
      "paid_by_name": "Aarav",
      "split_among": [1, 2],
      "created_at": "2026-10-24T03:18:00Z"
    }
  ],
  "owed_expenses": [
    {
      "expense_id": 10,
      "description": "Dinner",
      "share_amount": "75.00",
      "total_amount": "150.00",
      "date": "2026-10-24T03:18:00Z"
    }
  ]
}
```
