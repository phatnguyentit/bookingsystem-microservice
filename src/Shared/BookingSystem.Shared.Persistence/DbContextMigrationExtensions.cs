using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookingSystem.Shared.Persistence;

public static class DbContextMigrationExtensions
{
    public static async Task MigrateWithRetryAsync(this DbContext dbContext, ILogger logger, int attempts = 5, TimeSpan? delay = null)
    {
        var currentDelay = delay ?? TimeSpan.FromSeconds(2);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                if (!await dbContext.Database.CanConnectAsync())
                    throw new InvalidOperationException("Unable to connect to the database.");

                await dbContext.Database.MigrateAsync();
                return;
            }
            catch (Exception ex) when (attempt < attempts)
            {
                logger.LogWarning(ex, "Migration attempt {Attempt} failed. Retrying in {Delay}s...", attempt, currentDelay.TotalSeconds);
                await Task.Delay(currentDelay);
                currentDelay *= 2;
            }
        }
    }
}
