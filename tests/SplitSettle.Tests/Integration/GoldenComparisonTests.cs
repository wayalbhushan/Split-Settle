namespace SplitSettle.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SplitSettle.Api.DTOs;
using Xunit;

public class GoldenComparisonTests : IClassFixture<SplitSettleApiFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public GoldenComparisonTests(SplitSettleApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private class GoldenDataset
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Members { get; set; } = new();
        public List<GoldenExpense> Expenses { get; set; } = new();
        public List<GoldenBalance> ExpectedBalances { get; set; } = new();
        public List<GoldenSettlement> ExpectedSettlements { get; set; } = new();
    }

    private class GoldenExpense
    {
        public string Description { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string PaidByName { get; set; } = string.Empty;
        public List<string> SplitAmongNames { get; set; } = new();
    }

    private class GoldenBalance
    {
        public string MemberName { get; set; } = string.Empty;
        public string Balance { get; set; } = string.Empty;
    }

    private class GoldenSettlement
    {
        public string FromMemberName { get; set; } = string.Empty;
        public string ToMemberName { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
    }

    private static GoldenDataset LoadDataset(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Golden", fileName);
        if (!File.Exists(path))
        {
            // fallback to relative search
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "tests", "SplitSettle.Tests", "Golden", fileName);
                if (File.Exists(candidate))
                {
                    path = candidate;
                    break;
                }
                dir = dir.Parent;
            }
        }

        var json = File.ReadAllText(path);
        var dataset = JsonSerializer.Deserialize<GoldenDataset>(json, JsonOptions);
        Assert.NotNull(dataset);
        return dataset;
    }

    private async Task RunGoldenDatasetAsync(string fileName)
    {
        var dataset = LoadDataset(fileName);

        // 1. Create group
        var groupName = $"{dataset.Name} - {Guid.NewGuid():N}";
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest(groupName), JsonOptions);
        Assert.Equal(HttpStatusCode.Created, groupRes.StatusCode);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        // 2. Add members in order
        var memberMap = new Dictionary<string, int>();
        foreach (var memberName in dataset.Members)
        {
            var memRes = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest(memberName), JsonOptions);
            Assert.Equal(HttpStatusCode.Created, memRes.StatusCode);
            var mem = await memRes.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
            Assert.NotNull(mem);
            memberMap[memberName] = mem.Id;
        }

        // 3. Add expenses
        foreach (var exp in dataset.Expenses)
        {
            int paidById = memberMap[exp.PaidByName];
            var splitIds = exp.SplitAmongNames.Select(n => memberMap[n]).ToList();

            var expReq = new CreateExpenseRequest(exp.Description, exp.Amount, paidById, splitIds);
            var expRes = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/", expReq, JsonOptions);
            Assert.Equal(HttpStatusCode.Created, expRes.StatusCode);
        }

        // 4. Verify balances match Django exactly
        var balRes = await _client.GetAsync($"/api/groups/{group.Id}/balances/");
        Assert.Equal(HttpStatusCode.OK, balRes.StatusCode);
        var balances = await balRes.Content.ReadFromJsonAsync<List<BalanceResponse>>(JsonOptions);
        Assert.NotNull(balances);

        var balanceMap = balances.ToDictionary(b => b.MemberName, b => b.Balance);
        foreach (var expected in dataset.ExpectedBalances)
        {
            Assert.True(balanceMap.ContainsKey(expected.MemberName), $"Missing balance for {expected.MemberName}");
            Assert.Equal(expected.Balance, balanceMap[expected.MemberName]);
        }

        // 5. Verify settlements match Django exactly (same transfers, same amounts, same order)
        var setRes = await _client.GetAsync($"/api/groups/{group.Id}/settlements/");
        Assert.Equal(HttpStatusCode.OK, setRes.StatusCode);
        var settlements = await setRes.Content.ReadFromJsonAsync<List<SettlementResponse>>(JsonOptions);
        Assert.NotNull(settlements);

        Assert.Equal(dataset.ExpectedSettlements.Count, settlements.Count);
        for (int i = 0; i < dataset.ExpectedSettlements.Count; i++)
        {
            var expSettle = dataset.ExpectedSettlements[i];
            var actualSettle = settlements[i];

            Assert.Equal(expSettle.FromMemberName, actualSettle.FromMemberName);
            Assert.Equal(expSettle.ToMemberName, actualSettle.ToMemberName);
            Assert.Equal(expSettle.Amount, actualSettle.Amount);
        }
    }

    [Fact]
    public async Task GoldenDataset1_ThreeMembersWithRoundingAndMixedPayers_MatchesDjangoExactly()
    {
        await RunGoldenDatasetAsync("dataset1.json");
    }

    [Fact]
    public async Task GoldenDataset2_SixMembersWithSubsetSplits_MatchesDjangoExactly()
    {
        await RunGoldenDatasetAsync("dataset2.json");
    }

    [Fact]
    public async Task GoldenDataset3_TenMembersWithManyExpenses_MatchesDjangoExactly()
    {
        await RunGoldenDatasetAsync("dataset3.json");
    }
}
