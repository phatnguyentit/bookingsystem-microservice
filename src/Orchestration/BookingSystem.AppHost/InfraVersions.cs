using System.Text.Json;

namespace BookingSystem.AppHost;

internal sealed record InfraVersions(
    string Kafka,
    string Postgres,
    string Redis,
    string Elasticsearch)
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static InfraVersions Load(string appHostDirectory)
    {
        var path = Path.GetFullPath(
            Path.Combine(appHostDirectory, "..", "..", "..", "infra-versions.json"));
        return JsonSerializer.Deserialize<InfraVersions>(File.ReadAllText(path), _opts)!;
    }
}
