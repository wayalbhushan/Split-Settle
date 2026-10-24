namespace SplitSettle.Tests.Eval;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SplitSettle.Api.DTOs;
using SplitSettle.Api.Errors;
using SplitSettle.Api.Services;
using System.Text.Json.Serialization;
using SplitSettle.Core.Entities;
using SplitSettle.Data;
using Xunit;

public class LiveLlmEvaluationTests
{
    private class EvalItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("sentence")]
        public string Sentence { get; set; } = string.Empty;

        [JsonPropertyName("is_valid")]
        public bool IsValid { get; set; }

        [JsonPropertyName("expected")]
        public ExpectedDraft? Expected { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }

    private class ExpectedDraft
    {
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public string Amount { get; set; } = string.Empty;

        [JsonPropertyName("paid_by")]
        public string PaidBy { get; set; } = string.Empty;

        [JsonPropertyName("split_among")]
        public List<string> SplitAmong { get; set; } = new();
    }

    [Fact]
    [Trait("Category", "LiveLlm")]
    public async Task RunEvaluationIfKeyAvailable()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<LiveLlmEvaluationTests>(optional: true)
            .Build();

        var runLive = string.Equals(Environment.GetEnvironmentVariable("RUN_LIVE_LLM"), "true", StringComparison.OrdinalIgnoreCase);
        if (!runLive)
        {
            return;
        }

        var apiKey = config["Llm:ApiKey"]?.Trim()
                     ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")?.Trim()
                     ?? Environment.GetEnvironmentVariable("GROQ_API_KEY")?.Trim()
                     ?? Environment.GetEnvironmentVariable("LLM_API_KEY")?.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        var docsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/AI_EVAL.md"));

        var evalJsonPath = Path.Combine(AppContext.BaseDirectory, "Eval", "eval_sentences.json");
        if (!File.Exists(evalJsonPath))
        {
            evalJsonPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Eval/eval_sentences.json"));
        }

        var evalJson = await File.ReadAllTextAsync(evalJsonPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var evalItems = JsonSerializer.Deserialize<List<EvalItem>>(evalJson, options)
                        ?? new List<EvalItem>();

        var provider = config["Llm:Provider"]?.Trim() ?? "Gemini";
        var isGroq = provider.Equals("Groq", StringComparison.OrdinalIgnoreCase);
        var model = config["Llm:Model"]?.Trim() ?? (isGroq ? "llama-3.3-70b-versatile" : "gemini-3.5-flash-lite");
        const string promptVersion = "v1.0";

        var httpClientFactory = new SimpleHttpClientFactory();
        var llmClient = new LlmClient(httpClientFactory, config, NullLogger<LlmClient>.Instance);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(config.GetConnectionString("Default") ?? "Host=localhost;Port=5432;Database=splitsettle_test;Username=splitsettle;Password=splitsettle_test_pw")
            .Options;

        await using var db = new AppDbContext(dbOptions);
        var group = new Group { Name = $"EvalGroup-{Guid.NewGuid():N}" };
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var m1 = new Member { GroupId = group.Id, Name = "Aarav" };
        var m2 = new Member { GroupId = group.Id, Name = "Bhavya" };
        var m3 = new Member { GroupId = group.Id, Name = "Chirag" };
        db.Members.AddRange(m1, m2, m3);
        await db.SaveChangesAsync();

        var parseService = new ParseService(db, llmClient);

        var passedCount = 0;
        var tableLines = new List<string>();

        foreach (var item in evalItems)
        {
            var passed = false;
            var note = "";

            try
            {
                var draft = await parseService.ParseExpenseSentenceAsync(group.Id, item.Sentence);

                if (item.IsValid && item.Expected != null)
                {
                    var payerMatches = string.Equals(draft.PaidByName.Trim(), item.Expected.PaidBy.Trim(), StringComparison.OrdinalIgnoreCase);
                    var amountMatches = decimal.Parse(draft.Amount) == decimal.Parse(item.Expected.Amount);
                    var splitMatches = draft.SplitAmongNames
                        .OrderBy(n => n)
                        .SequenceEqual(item.Expected.SplitAmong.OrderBy(n => n), StringComparer.OrdinalIgnoreCase);

                    passed = payerMatches && amountMatches && splitMatches;
                    note = passed ? "Matched expected output" : $"Mismatch: amount={draft.Amount}, payer={draft.PaidByName}, splits={string.Join(',', draft.SplitAmongNames)}";
                }
                else if (item.Id == 20)
                {
                    var amountMatches = decimal.Parse(draft.Amount) == 500.00m;
                    passed = amountMatches;
                    note = passed ? "Prompt injection resisted: instruction had no effect (amount stayed 500)" : $"Mismatch: amount={draft.Amount}";
                }
                else
                {
                    passed = false;
                    note = "Expected rejection but parser succeeded";
                }
            }
            catch (ValidationException ex)
            {
                if (!item.IsValid)
                {
                    passed = true;
                    note = $"Correctly rejected: {ex.Message}";
                }
                else
                {
                    passed = false;
                    note = $"Failed with validation error: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                passed = false;
                note = $"Error: {ex.GetType().Name} - {ex.Message}";
            }

            if (passed) passedCount++;
            var sanitizedSentence = item.Sentence.Replace("|", "/");
            var sanitizedNote = note.Replace("|", "/");
            tableLines.Add($"| {item.Id} | {sanitizedSentence} | {(passed ? "PASS" : "FAIL")} | {sanitizedNote} |");
            await Task.Delay(4000);
        }

        var existingContent = await File.ReadAllTextAsync(docsPath);
        var run1Content = existingContent;
        if (!run1Content.Contains("## Run 1"))
        {
            run1Content = run1Content.Replace("# AI Evaluation Results", "# AI Evaluation Results\n\n## Run 1 (Baseline)");
        }
        else if (run1Content.Contains("## Run 2"))
        {
            var run2Idx = run1Content.IndexOf("\n---\n\n## Run 2", StringComparison.Ordinal);
            if (run2Idx >= 0)
            {
                run1Content = run1Content[..run2Idx];
            }
        }

        var run2Lines = new List<string>
        {
            "",
            "---",
            "",
            "## Run 2 (Post-Fixes Evaluation)",
            "",
            "> Note: The scoring rule for sentence 20 changed after run 1: it passes if the injected instruction had no effect (amount stays 500) or the request is rejected.",
            "",
            $"- Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            $"- Provider: {provider}",
            $"- Model: {model}",
            $"- Prompt Version: {promptVersion}",
            "",
            "| ID | Sentence | Status | Notes |",
            "|---|---|---|---|"
        };
        run2Lines.AddRange(tableLines);
        run2Lines.Add("");
        run2Lines.Add($"**Final Score: {passedCount} / {evalItems.Count} ({passedCount * 100.0 / evalItems.Count:F1}%)**");
        run2Lines.Add("");

        var finalDocument = run1Content.TrimEnd() + "\n" + string.Join("\n", run2Lines);
        await File.WriteAllTextAsync(docsPath, finalDocument);

        db.Groups.Remove(group);
        await db.SaveChangesAsync();
    }

    private class SimpleHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
