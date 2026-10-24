namespace SplitSettle.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SplitSettle.Api.DTOs;
using SplitSettle.Api.Errors;
using SplitSettle.Core.Services;
using Xunit;

[Collection("ApiTests")]
public class ParseExpenseApiTests
{
    private readonly SplitSettleApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public ParseExpenseApiTests(SplitSettleApiFactory factory)
    {
        _factory = factory;
    }

    private class TestLlmClient : ILlmClient
    {
        public Func<string, Task<string>> Handler { get; set; } = _ => Task.FromResult("{}");

        public Task<string> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default)
        {
            return Handler(prompt);
        }
    }

    private HttpClient CreateClientWithLlm(Func<string, Task<string>> handler)
    {
        var testClient = new TestLlmClient { Handler = handler };
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ILlmClient));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<ILlmClient>(testClient);
            });
        }).CreateClient();
    }

    private async Task<(int groupId, int m1Id, int m2Id, int m3Id)> CreateTestGroupWithMembersAsync(HttpClient client)
    {
        var groupRes = await client.PostAsJsonAsync("/api/groups/", new CreateGroupRequest($"ParseTest-{Guid.NewGuid():N}"), JsonOptions);
        var group = await groupRes.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
        Assert.NotNull(group);

        var m1Res = await client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Rahul"), JsonOptions);
        var m1 = await m1Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m1);

        var m2Res = await client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Priya"), JsonOptions);
        var m2 = await m2Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m2);

        var m3Res = await client.PostAsJsonAsync($"/api/groups/{group.Id}/members/", new CreateMemberRequest("Amit"), JsonOptions);
        var m3 = await m3Res.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(m3);

        return (group.Id, m1.Id, m2.Id, m3.Id);
    }

    [Fact]
    public async Task Parse_ValidReply_ReturnsParsedDraft()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("""
        {
            "description": "Dinner",
            "amount": 1200.00,
            "paid_by": "Rahul",
            "split_among": ["Rahul", "Priya", "Amit"]
        }
        """));

        var (groupId, m1Id, m2Id, m3Id) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Dinner 1200 paid by Rahul, split among Rahul, Priya and Amit"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<ParsedExpenseDraft>(JsonOptions);
        Assert.NotNull(draft);
        Assert.Equal("Dinner", draft.Description);
        Assert.Equal("1200.00", draft.Amount);
        Assert.Equal(m1Id, draft.PaidBy);
        Assert.Equal("Rahul", draft.PaidByName);
        Assert.Equal(3, draft.SplitAmong.Count);
        Assert.Contains(m1Id, draft.SplitAmong);
        Assert.Contains(m2Id, draft.SplitAmong);
        Assert.Contains(m3Id, draft.SplitAmong);
    }

    [Fact]
    public async Task Parse_JsonInCodeFence_ParsesSuccessfully()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("""
        ```json
        {
            "description": "Groceries",
            "amount": 450.50,
            "paid_by": "Priya",
            "split_among": ["Rahul", "Priya"]
        }
        ```
        """));

        var (groupId, m1Id, m2Id, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Groceries 450.50 paid by Priya split between Rahul and Priya"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<ParsedExpenseDraft>(JsonOptions);
        Assert.NotNull(draft);
        Assert.Equal("Groceries", draft.Description);
        Assert.Equal("450.50", draft.Amount);
        Assert.Equal(m2Id, draft.PaidBy);
        Assert.Equal(2, draft.SplitAmong.Count);
        Assert.Contains(m1Id, draft.SplitAmong);
        Assert.Contains(m2Id, draft.SplitAmong);
    }

    [Fact]
    public async Task Parse_MalformedJson_Returns400ProblemDetails()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("I am unable to parse this input."));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Dinner 1200"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("JSON", problem.Detail);
    }

    [Fact]
    public async Task Parse_UnknownMemberInPayer_Returns400ProblemDetails()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("""
        {
            "description": "Lunch",
            "amount": 500,
            "paid_by": "Charlie",
            "split_among": ["Rahul", "Priya"]
        }
        """));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Lunch 500 paid by Charlie"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("Unknown member 'Charlie'", problem.Detail);
    }

    [Fact]
    public async Task Parse_UnknownNameAloneInSplitAmong_Returns400ProblemDetails()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("""
        {
            "description": "Lunch",
            "amount": 500,
            "paid_by": "Rahul",
            "split_among": ["UnknownUser"]
        }
        """));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Lunch 500 paid by Rahul split with UnknownUser"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("Unknown member 'UnknownUser'", problem.Detail);
    }

    [Fact]
    public async Task Parse_UnknownNameMixedWithValidInSplitAmong_Returns400ProblemDetails()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("""
        {
            "description": "Lunch",
            "amount": 500,
            "paid_by": "Rahul",
            "split_among": ["Rahul", "UnknownUser"]
        }
        """));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Lunch 500 paid by Rahul split with UnknownUser"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("Unknown member 'UnknownUser'", problem.Detail);
    }

    [Fact]
    public async Task Parse_DuplicateNamesInSplitAmong_DedupesSuccessfully()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("""
        {
            "description": "Coffee",
            "amount": 100.00,
            "paid_by": "Rahul",
            "split_among": ["Rahul", "Rahul", "Priya"]
        }
        """));
        var (groupId, m1Id, m2Id, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Coffee 100 paid by Rahul split between Rahul and Priya"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<ParsedExpenseDraft>(JsonOptions);
        Assert.NotNull(draft);
        Assert.Equal(2, draft.SplitAmong.Count);
        Assert.Contains(m1Id, draft.SplitAmong);
        Assert.Contains(m2Id, draft.SplitAmong);
        Assert.Equal(2, draft.SplitAmongNames.Count);
    }

    [Fact]
    public async Task Parse_NegativeAmountMinus500_Returns400ProblemDetails()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("""
        {
            "description": "Refund",
            "amount": -500,
            "paid_by": "Rahul",
            "split_among": ["Rahul", "Priya"]
        }
        """));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Refund -500 paid by Rahul"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("greater than zero", problem.Detail);
    }

    [Fact]
    public async Task Parse_ZeroAmount_Returns400ProblemDetails()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("""
        {
            "description": "Free snack",
            "amount": 0,
            "paid_by": "Rahul",
            "split_among": ["Rahul", "Priya"]
        }
        """));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Free snack 0 paid by Rahul"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("greater than zero", problem.Detail);
    }

    [Fact]
    public async Task Parse_ThreeDecimals_Returns400ProblemDetails()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("""
        {
            "description": "Snacks",
            "amount": 10.005,
            "paid_by": "Rahul",
            "split_among": ["Rahul", "Priya"]
        }
        """));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Snacks 10.005 paid by Rahul"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("two decimal places", problem.Detail);
    }

    [Fact]
    public async Task Parse_MissingField_Returns400ProblemDetails()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("""
        {
            "amount": 100.00,
            "paid_by": "Rahul",
            "split_among": ["Rahul", "Priya"]
        }
        """));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("100 paid by Rahul"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("Description is required", problem.Detail);
    }

    [Fact]
    public async Task Parse_EmptyReply_Returns400ProblemDetails()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("   "));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Dinner 500 paid by Rahul"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("empty response", problem.Detail);
    }

    [Fact]
    public async Task Parse_ProviderError_Returns502ProblemDetails()
    {
        var client = CreateClientWithLlm(_ => throw new LlmProviderException("Simulated provider failure"));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Dinner 500 paid by Rahul"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(502, problem.Status);
    }

    [Fact]
    public async Task Parse_OversizedSentence_Returns400ProblemDetails()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("{}"));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var longSentence = new string('A', 301);
        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest(longSentence),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("300 characters", problem.Detail);
    }

    [Fact]
    public async Task Parse_NoApiKeyConfigured_Returns503ProblemDetails()
    {
        var client = CreateClientWithLlm(_ => throw new LlmNotConfiguredException());
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Dinner 500 paid by Rahul"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(503, problem.Status);
        Assert.Contains("AI parse service is not configured", problem.Detail);
    }

    [Fact]
    public async Task Parse_NoModelConfigured_Returns503ProblemDetails()
    {
        var client = CreateClientWithLlm(_ => throw new LlmNotConfiguredException("AI parse service is not configured. A model is required."));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Dinner 500 paid by Rahul"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(503, problem.Status);
        Assert.Contains("A model is required", problem.Detail);
    }

    [Fact]
    public async Task Parse_NegativeAmount_UnicodeMinus_Returns400ProblemDetails()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("""
        {
            "description": "Refund",
            "amount": 200,
            "paid_by": "Rahul",
            "split_among": ["Rahul", "Priya"]
        }
        """));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Refund \u2212200 paid by Rahul"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("greater than zero", problem.Detail);
    }

    [Fact]
    public async Task Parse_NegativeAmount_WordMinus_Returns400ProblemDetails()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("""
        {
            "description": "Refund",
            "amount": 200,
            "paid_by": "Rahul",
            "split_among": ["Rahul", "Priya"]
        }
        """));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Refund minus 200 paid by Rahul"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("greater than zero", problem.Detail);
    }

    [Fact]
    public async Task Parse_NegativeAmount_WordNegative_Returns400ProblemDetails()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("""
        {
            "description": "Refund",
            "amount": 200,
            "paid_by": "Rahul",
            "split_among": ["Rahul", "Priya"]
        }
        """));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Refund negative 200 paid by Rahul"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("greater than zero", problem.Detail);
    }

    [Fact]
    public async Task Parse_HyphenAsPunctuation_DoesNotReject()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("""
        {
            "description": "Taxi",
            "amount": 300,
            "paid_by": "Rahul",
            "split_among": ["Rahul", "Priya"]
        }
        """));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Taxi 300 - paid by Rahul, split among all"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<ParsedExpenseDraft>(JsonOptions);
        Assert.NotNull(draft);
        Assert.Equal("Taxi", draft.Description);
        Assert.Equal("300.00", draft.Amount);
    }

    [Fact]
    public async Task Parse_HyphenatedWord_DoesNotReject()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("""
        {
            "description": "Pre-dinner drinks",
            "amount": 500,
            "paid_by": "Rahul",
            "split_among": ["Rahul", "Priya"]
        }
        """));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Pre-dinner drinks 500 paid by Rahul, split among all"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<ParsedExpenseDraft>(JsonOptions);
        Assert.NotNull(draft);
        Assert.Equal("Pre-dinner drinks", draft.Description);
        Assert.Equal("500.00", draft.Amount);
    }

    [Fact]
    public async Task Parse_NumberRangeHyphen_DoesNotReject()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("""
        {
            "description": "Hotel room 101-102",
            "amount": 3600,
            "paid_by": "Rahul",
            "split_among": ["Rahul", "Priya"]
        }
        """));
        var (groupId, _, _, _) = await CreateTestGroupWithMembersAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/groups/{groupId}/expenses/parse/",
            new ParseExpenseRequest("Hotel room 101-102 3600 paid by Rahul, split among all"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<ParsedExpenseDraft>(JsonOptions);
        Assert.NotNull(draft);
        Assert.Equal("Hotel room 101-102", draft.Description);
        Assert.Equal("3600.00", draft.Amount);
    }

    [Fact]
    public async Task ParseExpense_NonExistentGroup_Returns404NotFound()
    {
        var client = CreateClientWithLlm(_ => Task.FromResult("{}"));
        var response = await client.PostAsJsonAsync(
            "/api/groups/999999/expenses/parse/",
            new ParseExpenseRequest("Dinner 500 paid by Rahul"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
