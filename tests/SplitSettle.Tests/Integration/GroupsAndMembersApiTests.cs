namespace SplitSettle.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SplitSettle.Api.DTOs;
using Xunit;

[Collection("ApiTests")]
public class GroupsAndMembersApiTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public GroupsAndMembersApiTests(SplitSettleApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostGroup_WithValidName_Returns201CreatedAndCorrectSnakeCaseJson()
    {
        var groupName = $"Trip to Paris {Guid.NewGuid():N}";
        var request = new CreateGroupRequest(groupName);

        var response = await _client.PostAsJsonAsync("/api/groups/", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal(groupName, created.Name);
        Assert.Equal(0, created.MemberCount);

        // Verify JSON keys in raw response
        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("id", out _));
        Assert.True(root.TryGetProperty("name", out _));
        Assert.True(root.TryGetProperty("created_at", out _));
        Assert.True(root.TryGetProperty("member_count", out _));
    }

    [Fact]
    public async Task PostGroup_WithEmptyOrWhitespaceName_Returns400BadRequestWithProblemDetails()
    {
        var request = new CreateGroupRequest("   ");

        var response = await _client.PostAsJsonAsync("/api/groups/", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("Bad Request", root.GetProperty("title").GetString());
        Assert.True(root.TryGetProperty("name", out var nameErrors));
        Assert.Contains("This field is required.", nameErrors.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task PostGroup_WithNameExceeding255Chars_Returns400BadRequest()
    {
        var longName = new string('A', 256);
        var request = new CreateGroupRequest(longName);

        var response = await _client.PostAsJsonAsync("/api/groups/", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("name", out var nameErrors));
        Assert.Contains("Ensure this field has no more than 255 characters.", nameErrors.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task GetGroups_ReturnsAllGroupsAndSupportsTrailingSlash()
    {
        var groupName = $"Group Slash Test {Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest(groupName), JsonOptions);

        var withSlash = await _client.GetAsync("/api/groups/");
        Assert.Equal(HttpStatusCode.OK, withSlash.StatusCode);

        var withoutSlash = await _client.GetAsync("/api/groups");
        Assert.Equal(HttpStatusCode.OK, withoutSlash.StatusCode);

        var groups = await withSlash.Content.ReadFromJsonAsync<List<GroupResponse>>(JsonOptions);
        Assert.NotNull(groups);
        Assert.Contains(groups, g => g.Name == groupName);
    }

    [Fact]
    public async Task GetGroupById_ExistingGroup_Returns200WithMembers()
    {
        var groupName = $"Group With Members {Guid.NewGuid():N}";
        var createGroupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest(groupName), JsonOptions);
        var createdGroup = await createGroupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(createdGroup);

        await _client.PostAsJsonAsync($"/api/groups/{createdGroup.Id}/members/", new CreateMemberRequest("Aarav"), JsonOptions);
        await _client.PostAsJsonAsync($"/api/groups/{createdGroup.Id}/members/", new CreateMemberRequest("Bhavya"), JsonOptions);

        var response = await _client.GetAsync($"/api/groups/{createdGroup.Id}/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<GroupDetailResponse>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(createdGroup.Id, detail.Id);
        Assert.Equal(groupName, detail.Name);
        Assert.Equal(2, detail.MemberCount);
        Assert.Equal(2, detail.Members.Count);
        Assert.Contains(detail.Members, m => m.Name == "Aarav" && m.Group == createdGroup.Id);
        Assert.Contains(detail.Members, m => m.Name == "Bhavya" && m.Group == createdGroup.Id);
    }

    [Fact]
    public async Task GetGroupById_NonExistentGroup_Returns404NotFoundWithProblemDetails()
    {
        var response = await _client.GetAsync("/api/groups/999999/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("Not Found", root.GetProperty("title").GetString());
    }

    [Fact]
    public async Task PostMember_ToNonExistentGroup_Returns404NotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/groups/999999/members/", new CreateMemberRequest("Solo"), JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;
        Assert.Equal(404, root.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task PostMember_WithEmptyName_Returns400BadRequest()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"G-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var response = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest(""), JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("name", out var errs));
        Assert.Contains("This field is required.", errs.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task PostMember_DuplicateNameInSameGroup_Returns409ConflictWithProblemDetails()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"G-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1 = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Chirag"), JsonOptions);
        Assert.Equal(HttpStatusCode.Created, m1.StatusCode);

        var duplicate = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("chirag"), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var rawJson = await duplicate.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;
        Assert.Equal(409, root.GetProperty("status").GetInt32());
        Assert.Equal("Conflict", root.GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetMemberDetail_ExistingMemberWithoutExpenses_ReturnsZeroBalanceAndEmptyExpenses()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"G-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var memberRes = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Divya"), JsonOptions);
        var member = await memberRes.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(member);

        var response = await _client.GetAsync($"/api/groups/{group.Id}/members/{member.Id}/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<MemberDetailResponse>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(member.Id, detail.MemberId);
        Assert.Equal("Divya", detail.MemberName);
        Assert.Equal("0.00", detail.Balance);
        Assert.Empty(detail.PaidExpenses);
        Assert.Empty(detail.OwedExpenses);

        // Verify JSON fields
        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("member_id", out _));
        Assert.True(root.TryGetProperty("member_name", out _));
        Assert.True(root.TryGetProperty("balance", out _));
        Assert.True(root.TryGetProperty("paid_expenses", out _));
        Assert.True(root.TryGetProperty("owed_expenses", out _));
    }

    [Fact]
    public async Task GetMemberDetail_NonExistentMemberOrWrongGroup_Returns404NotFound()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"G-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var response = await _client.GetAsync($"/api/groups/{group.Id}/members/999999/");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMembers_ExistingGroup_ReturnsAllMembers()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"G-Members-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Member1"), JsonOptions);
        await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Member2"), JsonOptions);

        var response = await _client.GetAsync($"/api/groups/{group.Id}/members/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var members = await response.Content.ReadFromJsonAsync<List<MemberResponse>>(JsonOptions);
        Assert.NotNull(members);
        Assert.Equal(2, members.Count);
        Assert.Contains(members, m => m.Name == "Member1" && m.Group == group.Id);
        Assert.Contains(members, m => m.Name == "Member2" && m.Group == group.Id);

        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var firstElem = doc.RootElement[0];
        Assert.True(firstElem.TryGetProperty("id", out _));
        Assert.True(firstElem.TryGetProperty("name", out _));
        Assert.True(firstElem.TryGetProperty("group", out _));
    }

    [Fact]
    public async Task GetMembers_NonExistentGroup_Returns404NotFound()
    {
        var response = await _client.GetAsync("/api/groups/999999/members/");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostMember_WithValidName_Returns201CreatedAndCorrectSnakeCaseJson()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"G-PostMem-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var response = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Ananya"), JsonOptions);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal("Ananya", created.Name);
        Assert.Equal(group.Id, created.Group);

        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("id", out _));
        Assert.True(root.TryGetProperty("name", out _));
        Assert.True(root.TryGetProperty("group", out _));
    }

    [Fact]
    public async Task GetMemberDetail_ExistingMemberWithExpenses_ReturnsCalculatedBalanceAndExpenses()
    {
        var groupRes = await _client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"G-DetailExp-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1Res = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Aarav"), JsonOptions);
        var m1 = await m1Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        var m2Res = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Bhavya"), JsonOptions);
        var m2 = await m2Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m1);
        Assert.NotNull(m2);

        var expReq = new CreateExpenseRequest("Dinner", "100.00", m1.Id, new List<int> { m1.Id, m2.Id });
        await _client.PostAsJsonAsync($"/api/groups/{group.Id}/expenses/", expReq, JsonOptions);

        var response = await _client.GetAsync($"/api/groups/{group.Id}/members/{m1.Id}/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<MemberDetailResponse>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(m1.Id, detail.MemberId);
        Assert.Equal("Aarav", detail.MemberName);
        Assert.Equal("50.00", detail.Balance);
        Assert.Single(detail.PaidExpenses);
        Assert.Single(detail.OwedExpenses);
        Assert.Equal("Dinner", detail.PaidExpenses[0].Description);
        Assert.Equal("100.00", detail.PaidExpenses[0].Amount);
        Assert.Equal("50.00", detail.OwedExpenses[0].ShareAmount);
    }
}
