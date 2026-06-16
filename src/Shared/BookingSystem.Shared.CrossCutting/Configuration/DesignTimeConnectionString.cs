using Microsoft.Extensions.Configuration;

namespace BookingSystem.Shared.CrossCutting.Configuration;

/// <summary>
/// Resolves a database connection string for EF Core design-time tooling
/// (<c>dotnet ef</c>). Prefers the connection string Aspire injects as an
/// environment variable, and falls back to the API project's appsettings.json
/// for standalone runs against the local docker-compose Postgres.
/// </summary>
public static class DesignTimeConnectionString
{
    public static string Resolve(string connectionName, string apiProjectName)
    {
        var apiProjectPath = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", apiProjectName));

        return Environment.GetEnvironmentVariable($"ConnectionStrings__{connectionName}")
            ?? new ConfigurationBuilder()
                .SetBasePath(apiProjectPath)
                .AddJsonFile("appsettings.json", optional: true)
                .Build()
                .GetConnectionString(connectionName)
            ?? throw new InvalidOperationException(
                $"Connection string '{connectionName}' not found in appsettings.json or the " +
                $"ConnectionStrings__{connectionName} environment variable.");
    }
}
