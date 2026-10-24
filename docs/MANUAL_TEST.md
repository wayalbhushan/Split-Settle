# Manual Testing Checklist

Follow these steps to manually verify the SplitSettle React client against the running .NET 8 Web API:

## 1. Start Services
1. Ensure the PostgreSQL container is running:
   ```powershell
   docker compose ps
   ```
2. Start the ASP.NET Core API (in terminal 1):
   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   dotnet run --project src/SplitSettle.Api --launch-profile http
   ```
   Verify it listens on `http://localhost:5055`.
3. Start the Vite React client (in terminal 2):
   ```powershell
   cd client
   npm run dev
   ```
   Verify it opens at `http://localhost:5173`.

## 2. Test User Flows
1. **Create Group**:
   - Click "Create New Group".
   - Enter Group Name: `Weekend Camping`.
   - Enter Initial Members: `Aarav, Bhavya, Chirag`.
   - Click "Save". Verify redirect to group detail view showing 3 members.
2. **Add Member**:
   - In group detail, click "+ Add Member".
   - Enter Name: `Divya`.
   - Click "Save". Verify Divya appears in the member list.
3. **Add Expense with Subset Split**:
   - Click "+ Add Expense".
   - Enter Description: `Campground Reservation`.
   - Enter Amount: `150.00`.
   - Select Paid By: `Aarav`.
   - Under "Split Among", select only `Aarav`, `Bhavya`, and `Chirag` (leave `Divya` unchecked).
   - Click "Save".
   - Verify expense appears in the list as `150.00 paid by Aarav`.
4. **View Balances**:
   - Verify Aarav has positive balance `+100.00`.
   - Verify Bhavya has negative balance `-50.00`.
   - Verify Chirag has negative balance `-50.00`.
   - Verify Divya has zero balance `0.00`.
5. **View Settlements**:
   - Verify settlements display:
     - `Bhavya -> Aarav ₹50.00`
     - `Chirag -> Aarav ₹50.00`
6. **Open Member Detail**:
   - Click on `Aarav`.
   - Verify balance shows `₹100.00`.
   - Verify `Campground Reservation` appears in "Expenses Paid".
   - Verify `Campground Reservation` appears in "Share Owed" with share `₹50.00`.
