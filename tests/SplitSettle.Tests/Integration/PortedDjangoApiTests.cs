namespace SplitSettle.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SplitSettle.Api.DTOs;
using Xunit;

[Collection("ApiTests")]
public class PortedDjangoApiTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public PortedDjangoApiTests(SplitSettleApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // 1. Django: APILedgerTests.test_create_expense_via_api
    [Fact]
    public async Task DjangoApiTest_CreateExpenseViaApi_CreatesExpenseAndSubsetsSplits()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"API-Group-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1 = await (await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Aarav"), JsonOptions)).Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        var m2 = await (await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Bhavya"), JsonOptions)).Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        var m3 = await (await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Chirag"), JsonOptions)).Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m1);
        Assert.NotNull(m2);
        Assert.NotNull(m3);

        var data = new CreateExpenseRequest(
            "Dinner",
            "150.00",
            m1.Id,
            new List<int> { m1.Id, m2.Id });

        var response = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/", data, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var exp = await response.Content.ReadFromJsonAsync<ExpenseResponse>(JsonOptions);
        Assert.NotNull(exp);
        Assert.Equal("150.00", exp.Amount);
        Assert.Equal(2, exp.SplitAmong.Count);
        Assert.Contains(m1.Id, exp.SplitAmong);
        Assert.Contains(m2.Id, exp.SplitAmong);

        // Verify member details reflect split shares (150 / 2 = 75 each)
        var m1Detail = await (await _client.GetAsync($"/api/groups/{group.Id}/members/{m1.Id}/")).Content.ReadFromJsonAsync<MemberDetailResponse>(JsonOptions);
        Assert.NotNull(m1Detail);
        Assert.Equal("75.00", m1Detail.Balance); // paid 150, owes 75 -> +75

        var m2Detail = await (await _client.GetAsync($"/api/groups/{group.Id}/members/{m2.Id}/")).Content.ReadFromJsonAsync<MemberDetailResponse>(JsonOptions);
        Assert.NotNull(m2Detail);
        Assert.Equal("-75.00", m2Detail.Balance); // paid 0, owes 75 -> -75
    }

    // 2. Django: APILedgerTests.test_get_balances_api
    [Fact]
    public async Task DjangoApiTest_GetBalancesApi_ReturnsCorrectStringBalances()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"API-Bal-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1 = await (await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Aarav"), JsonOptions)).Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        var m2 = await (await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Bhavya"), JsonOptions)).Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        var m3 = await (await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Chirag"), JsonOptions)).Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m1);
        Assert.NotNull(m2);
        Assert.NotNull(m3);

        await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("E1", "90.00", m1.Id, new List<int> { m1.Id, m2.Id, m3.Id }), JsonOptions);

        var response = await _client.GetAsync($"/api/groups/{group.Id}/balances/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var balances = await response.Content.ReadFromJsonAsync<List<BalanceResponse>>(JsonOptions);
        Assert.NotNull(balances);

        var map = balances.ToDictionary(b => b.MemberName, b => b.Balance);
        Assert.Equal("60.00", map["Aarav"]);
        Assert.Equal("-30.00", map["Bhavya"]);
        Assert.Equal("-30.00", map["Chirag"]);
    }

    // 3. Django: APILedgerTests.test_get_settlements_api
    [Fact]
    public async Task DjangoApiTest_GetSettlementsApi_ReturnsExpectedTransfers()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"API-Set-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1 = await (await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Aarav"), JsonOptions)).Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        var m2 = await (await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Bhavya"), JsonOptions)).Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        var m3 = await (await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Chirag"), JsonOptions)).Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m1);
        Assert.NotNull(m2);
        Assert.NotNull(m3);

        await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("E1", "90.00", m1.Id, new List<int> { m1.Id, m2.Id, m3.Id }), JsonOptions);

        var response = await _client.GetAsync($"/api/groups/{group.Id}/settlements/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var settlements = await response.Content.ReadFromJsonAsync<List<SettlementResponse>>(JsonOptions);
        Assert.NotNull(settlements);
        Assert.Equal(2, settlements.Count);

        foreach (var s in settlements)
        {
            Assert.Equal("Aarav", s.ToMemberName);
            Assert.Equal("30.00", s.Amount);
            Assert.Contains(s.FromMemberName, new[] { "Bhavya", "Chirag" });
        }
    }

    // 4. Django: APILedgerTests.test_invalid_expense_creation
    [Fact]
    public async Task DjangoApiTest_InvalidExpenseCreation_Returns400WithFieldErrors()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"API-Inv-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1 = await (await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Aarav"), JsonOptions)).Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m1);

        // Negative amount
        var negRes = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("Dinner", "-50.00", m1.Id, new List<int> { m1.Id }), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, negRes.StatusCode);
        var negJson = await negRes.Content.ReadAsStringAsync();
        using var negDoc = JsonDocument.Parse(negJson);
        Assert.True(negDoc.RootElement.TryGetProperty("amount", out _));

        // Empty split_among
        var emptyRes = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/",
            new CreateExpenseRequest("Dinner", "50.00", m1.Id, new List<int>()), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, emptyRes.StatusCode);
        var emptyJson = await emptyRes.Content.ReadAsStringAsync();
        using var emptyDoc = JsonDocument.Parse(emptyJson);
        Assert.True(emptyDoc.RootElement.TryGetProperty("split_among", out _));
    }
}
