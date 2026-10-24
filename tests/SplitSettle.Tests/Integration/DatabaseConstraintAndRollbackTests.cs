namespace SplitSettle.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitSettle.Api.DTOs;
using SplitSettle.Core.Entities;
using SplitSettle.Data;
using Xunit;

[Collection("ApiTests")]
public class DatabaseConstraintAndRollbackTests
{
    private readonly HttpClient _client;
    private readonly SplitSettleApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public DatabaseConstraintAndRollbackTests(SplitSettleApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostExpense_WhenValidationFails_LeavesNoPartialDataInDatabase()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"NoPartial-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1Res = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Aarav"), JsonOptions);
        var m1 = await m1Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m1);

        // Intentionally invalid: split member 999999 does not exist
        var expReq = new CreateExpenseRequest("Dinner", "100.00", m1.Id, new List<int> { m1.Id, 999999 });
        var response = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/", expReq, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify database directly
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var expenseCount = await context.Expenses.CountAsync(e => e.GroupId == group.Id);
        var splitCount = await context.ExpenseSplits.CountAsync(s => s.Expense.GroupId == group.Id);

        Assert.Equal(0, expenseCount);
        Assert.Equal(0, splitCount);
    }

    [Fact]
    public async Task PutExpense_RegeneratesSplitsCorrectlyInDatabase()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"Regen-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1 = await (await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("M1"), JsonOptions)).Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        var m2 = await (await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("M2"), JsonOptions)).Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        var m3 = await (await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("M3"), JsonOptions)).Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m1);
        Assert.NotNull(m2);
        Assert.NotNull(m3);

        // Initial expense: 100 split between M1 and M2 (50 each)
        var createRes = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("Taxi", "100.00", m1.Id, new List<int> { m1.Id, m2.Id }), JsonOptions);
        var exp = await createRes.Content.ReadFromJsonAsync<ExpenseResponse>(JsonOptions);
        Assert.NotNull(exp);

        // Update: 150 split among M1, M2, M3 (50 each)
        var putRes = await _client.PutAsJsonAsync($"/api/groups/{group.Id}/expenses/{exp.Id}/",
            new UpdateExpenseRequest("Van", "150.00", m2.Id, new List<int> { m1.Id, m2.Id, m3.Id }), JsonOptions);
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);

        // Verify in database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dbSplits = await context.ExpenseSplits
            .Where(s => s.ExpenseId == exp.Id)
            .OrderBy(s => s.MemberId)
            .ToListAsync();

        Assert.Equal(3, dbSplits.Count);
        Assert.Equal(new[] { m1.Id, m2.Id, m3.Id }, dbSplits.Select(s => s.MemberId).ToArray());
        Assert.All(dbSplits, s => Assert.Equal(50.00m, s.ShareAmount));
    }

    [Fact]
    public async Task DeleteExpense_RemovesSplitsFromDatabase()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"DelSplits-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1 = await (await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("M1"), JsonOptions)).Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m1);

        var createRes = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("Coffee", "15.00", m1.Id, new List<int> { m1.Id }), JsonOptions);
        var exp = await createRes.Content.ReadFromJsonAsync<ExpenseResponse>(JsonOptions);
        Assert.NotNull(exp);

        var delRes = await _client.DeleteAsync($"/api/groups/{group.Id}/expenses/{exp.Id}/");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var splitCount = await context.ExpenseSplits.CountAsync(s => s.ExpenseId == exp.Id);
        Assert.Equal(0, splitCount);
    }

    [Fact]
    public async Task DeleteGroup_ViaDbContext_CascadesAndRemovesMembersExpensesAndSplits()
    {
        int groupId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var group = new Group { Name = $"Cascade-{Guid.NewGuid():N}" };
            var m1 = new Member { Name = "C1", Group = group };
            var m2 = new Member { Name = "C2", Group = group };
            var exp = new Expense { Group = group, PaidByMember = m1, Description = "Lunch", Amount = 40.00m };
            exp.Splits.Add(new ExpenseSplit { Member = m1, ShareAmount = 20.00m });
            exp.Splits.Add(new ExpenseSplit { Member = m2, ShareAmount = 20.00m });

            group.Members.Add(m1);
            group.Members.Add(m2);
            group.Expenses.Add(exp);

            context.Groups.Add(group);
            await context.SaveChangesAsync();
            groupId = group.Id;
        }

        // Delete group
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var group = await context.Groups.FindAsync(groupId);
            Assert.NotNull(group);

            context.Groups.Remove(group);
            await context.SaveChangesAsync();
        }

        // Verify complete cascade deletion
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Null(await context.Groups.FindAsync(groupId));
            Assert.Empty(await context.Members.Where(m => m.GroupId == groupId).ToListAsync());
            Assert.Empty(await context.Expenses.Where(e => e.GroupId == groupId).ToListAsync());
        }
    }

    [Fact]
    public async Task DeletePayerMember_ViaDbContext_IsBlockedByDatabaseForeignKeyConstraint()
    {
        int memberId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var group = new Group { Name = $"BlockPayer-{Guid.NewGuid():N}" };
            var m1 = new Member { Name = "Payer1", Group = group };
            var m2 = new Member { Name = "Payer2", Group = group };
            var exp = new Expense { Group = group, PaidByMember = m1, Description = "Dinner", Amount = 50.00m };
            exp.Splits.Add(new ExpenseSplit { Member = m2, ShareAmount = 50.00m });

            group.Members.Add(m1);
            group.Members.Add(m2);
            group.Expenses.Add(exp);

            context.Groups.Add(group);
            await context.SaveChangesAsync();
            memberId = m1.Id;
        }

        // Attempting to delete the payer member alone must fail at database level
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var member = await context.Members.FindAsync(memberId);
            Assert.NotNull(member);

            context.Members.Remove(member);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }
}
