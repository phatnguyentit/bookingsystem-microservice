using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BookingSystem.Shared.CrossCutting.Configuration;

/// <summary>
/// Base <see cref="IDesignTimeDbContextFactory{TContext}"/> for EF Core design-time
/// tooling (<c>dotnet ef</c>). Subclasses supply only the connection name and API
/// project name; the connection string is resolved via <see cref="DesignTimeConnectionString"/>
/// (Aspire env var → API <c>appsettings.json</c>).
/// </summary>
public abstract class DesignTimeDbContextFactoryBase<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
    protected abstract string ConnectionName { get; }
    protected abstract string ApiProjectName { get; }

    public TContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(DesignTimeConnectionString.Resolve(ConnectionName, ApiProjectName))
            .Options;

        return (TContext)Activator.CreateInstance(typeof(TContext), options)!;
    }
}
