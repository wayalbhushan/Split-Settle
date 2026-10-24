namespace SplitSettle.Tests.Integration;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitSettle.Data;
using Xunit;

public class SplitSettleApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly object Lock = new();
    private static bool _migrated;

    public static string GetTestConnectionString()
    {
        var envVar = Environment.GetEnvironmentVariable("SPLITSETTLE_TEST_DB");
        if (!string.IsNullOrWhiteSpace(envVar))
        {
            return envVar;
        }

        string user = "splitsettle";
        string password = "";
        string port = "5432";

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? envPath = null;
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
            {
                envPath = candidate;
                break;
            }
            dir = dir.Parent;
        }

        if (envPath != null && File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
                var parts = trimmed.Split('=', 2);
                if (parts.Length != 2) continue;
                var key = parts[0].Trim();
                var val = parts[1].Trim();

                if (key == "POSTGRES_USER") user = val;
                else if (key == "POSTGRES_PASSWORD") password = val;
                else if (key == "POSTGRES_PORT") port = val;
            }
        }

        return $"Host=localhost;Port={port};Database=splitsettle_test;Username={user};Password={password}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            var connStr = GetTestConnectionString();
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connStr));
        });
    }

    public async Task InitializeAsync()
    {
        lock (Lock)
        {
            if (_migrated) return;
        }

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();

        lock (Lock)
        {
            _migrated = true;
        }
    }

    public new Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
