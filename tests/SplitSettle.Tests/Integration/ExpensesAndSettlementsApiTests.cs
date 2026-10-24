namespace SplitSettle.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SplitSettle.Api.DTOs;
using Xunit;

[Collection("ApiTests")]
public class ExpensesAndSettlementsApiTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public ExpensesAndSettlementsApiTests(SplitSettleApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostExpense_WithValidData_CreatesExpenseAndSplits()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"G-Exp-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1Res = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Aarav"), JsonOptions);
        var m1 = await m1Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        var m2Res = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Bhavya"), JsonOptions);
        var m2 = await m2Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m1);
        Assert.NotNull(m2);

        var expReq = new CreateExpenseRequest(
            "Dinner",
            "100.00",
            m1.Id,
            new List<int> { m1.Id, m2.Id });

        var expRes = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/", expReq, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, expRes.StatusCode);

        var createdExp = await expRes.Content.ReadFromJsonAsync<ExpenseResponse>(JsonOptions);
        Assert.NotNull(createdExp);
        Assert.Equal("Dinner", createdExp.Description);
        Assert.Equal("100.00", createdExp.Amount);
        Assert.Equal(m1.Id, createdExp.PaidBy);
        Assert.Equal("Aarav", createdExp.PaidByName);
        Assert.Equal(2, createdExp.SplitAmong.Count);
    }

    [Fact]
    public async Task PostExpense_InvalidInputs_Returns400WithProblemDetails()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"G-Val-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1Res = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Aarav"), JsonOptions);
        var m1 = await m1Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m1);

        // Negative amount
        var negRes = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("Dinner", "-50.00", m1.Id, new List<int> { m1.Id }), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, negRes.StatusCode);
        var negDoc = JsonDocument.Parse(await negRes.Content.ReadAsStringAsync());
        Assert.True(negDoc.RootElement.TryGetProperty("amount", out _));

        // Three decimals
        var threeDecRes = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("Dinner", "10.005", m1.Id, new List<int> { m1.Id }), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, threeDecRes.StatusCode);

        // Empty description
        var emptyDescRes = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("", "50.00", m1.Id, new List<int> { m1.Id }), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, emptyDescRes.StatusCode);

        // Payer not in group
        var invalidPayerRes = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("Dinner", "50.00", 999999, new List<int> { m1.Id }), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPayerRes.StatusCode);

        // Empty split_among
        var emptySplitRes = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("Dinner", "50.00", m1.Id, new List<int>()), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, emptySplitRes.StatusCode);

        // Split member not in group
        var invalidSplitRes = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("Dinner", "50.00", m1.Id, new List<int> { m1.Id, 999999 }), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, invalidSplitRes.StatusCode);
    }

    [Fact]
    public async Task PutExpense_UpdatesExpenseAndRegeneratesSplits()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"G-Put-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1Res = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Aarav"), JsonOptions);
        var m1 = await m1Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        var m2Res = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Bhavya"), JsonOptions);
        var m2 = await m2Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m1);
        Assert.NotNull(m2);

        var expRes = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("Snacks", "40.00", m1.Id, new List<int> { m1.Id, m2.Id }), JsonOptions);
        var exp = await expRes.Content.ReadFromJsonAsync<ExpenseResponse>(JsonOptions);
        Assert.NotNull(exp);

        var updateReq = new UpdateExpenseRequest("Buffet Dinner", "120.00", m2.Id, new List<int> { m1.Id, m2.Id });
        var putRes = await _client.PutAsJsonAsync($"/api/groups/{group.Id}/expenses/{exp.Id}/", updateReq, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);

        var updated = await putRes.Content.ReadFromJsonAsync<ExpenseResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Buffet Dinner", updated.Description);
        Assert.Equal("120.00", updated.Amount);
        Assert.Equal(m2.Id, updated.PaidBy);
        Assert.Equal("Bhavya", updated.PaidByName);
    }

    [Fact]
    public async Task DeleteExpense_RemovesExpenseAndReturns204NoContent()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"G-Del-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1Res = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Aarav"), JsonOptions);
        var m1 = await m1Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m1);

        var expRes = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("Tea", "10.00", m1.Id, new List<int> { m1.Id }), JsonOptions);
        var exp = await expRes.Content.ReadFromJsonAsync<ExpenseResponse>(JsonOptions);
        Assert.NotNull(exp);

        var delRes = await _client.DeleteAsync($"/api/groups/{group.Id}/expenses/{exp.Id}/");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        var getRes = await _client.GetAsync($"/api/groups/{group.Id}/expenses/");
        var list = await getRes.Content.ReadFromJsonAsync<List<ExpenseResponse>>(JsonOptions);
        Assert.NotNull(list);
        Assert.DoesNotContain(list, e => e.Id == exp.Id);
    }

    [Fact]
    public async Task GetBalancesAndSettlements_CalculatesCorrectBalancesAndTransfers()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"G-Bal-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1Res = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Aarav"), JsonOptions);
        var m1 = await m1Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        var m2Res = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Bhavya"), JsonOptions);
        var m2 = await m2Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        var m3Res = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Chirag"), JsonOptions);
        var m3 = await m3Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m1);
        Assert.NotNull(m2);
        Assert.NotNull(m3);

        // Aarav pays 90 split among Aarav, Bhavya, Chirag (30 each)
        await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("Dinner", "90.00", m1.Id, new List<int> { m1.Id, m2.Id, m3.Id }), JsonOptions);

        var balRes = await _client.GetAsync($"/api/groups/{group.Id}/balances/");
        Assert.Equal(HttpStatusCode.OK, balRes.StatusCode);
        var balances = await balRes.Content.ReadFromJsonAsync<List<BalanceResponse>>(JsonOptions);
        Assert.NotNull(balances);

        var aaravBal = balances.First(b => b.MemberId == m1.Id).Balance;
        var bhavyaBal = balances.First(b => b.MemberId == m2.Id).Balance;
        var chiragBal = balances.First(b => b.MemberId == m3.Id).Balance;

        Assert.Equal("60.00", aaravBal);
        Assert.Equal("-30.00", bhavyaBal);
        Assert.Equal("-30.00", chiragBal);

        var setRes = await _client.GetAsync($"/api/groups/{group.Id}/settlements/");
        Assert.Equal(HttpStatusCode.OK, setRes.StatusCode);
        var settlements = await setRes.Content.ReadFromJsonAsync<List<SettlementResponse>>(JsonOptions);
        Assert.NotNull(settlements);
        Assert.Equal(2, settlements.Count);
        Assert.All(settlements, s => Assert.Equal("Aarav", s.ToMemberName));
        Assert.All(settlements, s => Assert.Equal("30.00", s.Amount));
    }

    [Fact]
    public async Task ExpenseEndpoints_NonExistentGroupOrExpense_Returns404NotFound()
    {
        // GET expenses for missing group
        var getExpRes = await _client.GetAsync("/api/groups/999999/expenses/");
        Assert.Equal(HttpStatusCode.NotFound, getExpRes.StatusCode);

        // POST expense for missing group
        var postExpRes = await _client.PostAsJsonAsync("/api/groups/999999/expenses/",
            new CreateExpenseRequest("Dinner", "50.00", 1, new List<int> { 1 }), JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, postExpRes.StatusCode);

        // PUT expense for missing group
        var putExpRes = await _client.PutAsJsonAsync("/api/groups/999999/expenses/1/",
            new UpdateExpenseRequest("Dinner", "50.00", 1, new List<int> { 1 }), JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, putExpRes.StatusCode);

        // DELETE expense for missing group
        var delExpRes = await _client.DeleteAsync("/api/groups/999999/expenses/1/");
        Assert.Equal(HttpStatusCode.NotFound, delExpRes.StatusCode);

        // GET balances for missing group
        var balRes = await _client.GetAsync("/api/groups/999999/balances/");
        Assert.Equal(HttpStatusCode.NotFound, balRes.StatusCode);

        // GET settlements for missing group
        var setRes = await _client.GetAsync("/api/groups/999999/settlements/");
        Assert.Equal(HttpStatusCode.NotFound, setRes.StatusCode);
    }

    [Fact]
    public async Task PutExpense_WithInvalidData_Returns400BadRequest()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"G-PutVal-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1 = await (await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("M1"), JsonOptions)).Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m1);

        var exp = await (await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("Dinner", "50.00", m1.Id, new List<int> { m1.Id }), JsonOptions)).Content.ReadFromJsonAsync<ExpenseResponse>(JsonOptions);
        Assert.NotNull(exp);

        // Invalid negative amount on update
        var putRes = await _client.PutAsJsonAsync($"/api/groups/{group.Id}/expenses/{exp.Id}/",
            new UpdateExpenseRequest("Dinner", "-10.00", m1.Id, new List<int> { m1.Id }), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, putRes.StatusCode);
    }

    [Fact]
    public async Task GetExpenses_ExistingGroupWithExpenses_ReturnsAllExpensesList()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"G-GetExps-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1Res = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Aarav"), JsonOptions);
        var m1 = await m1Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        var m2Res = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Bhavya"), JsonOptions);
        var m2 = await m2Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m1);
        Assert.NotNull(m2);

        await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("Lunch", "40.00", m1.Id, new List<int> { m1.Id, m2.Id }), JsonOptions);
        await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("Dinner", "80.00", m2.Id, new List<int> { m1.Id, m2.Id }), JsonOptions);

        var response = await _client.GetAsync($"/api/groups/{group.Id}/expenses/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var expenses = await response.Content.ReadFromJsonAsync<List<ExpenseResponse>>(JsonOptions);
        Assert.NotNull(expenses);
        Assert.Equal(2, expenses.Count);
        Assert.Contains(expenses, e => e.Description == "Lunch" && e.Amount == "40.00");
        Assert.Contains(expenses, e => e.Description == "Dinner" && e.Amount == "80.00");

        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var first = doc.RootElement[0];
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("group", out _));
        Assert.True(first.TryGetProperty("description", out _));
        Assert.True(first.TryGetProperty("amount", out _));
        Assert.True(first.TryGetProperty("paid_by", out _));
        Assert.True(first.TryGetProperty("paid_by_name", out _));
        Assert.True(first.TryGetProperty("split_among", out _));
        Assert.True(first.TryGetProperty("created_at", out _));
    }
}
