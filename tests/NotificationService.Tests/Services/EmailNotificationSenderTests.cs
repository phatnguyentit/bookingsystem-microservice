using BookingSystem.NotificationService.Infrastructure.Persistence;
using BookingSystem.NotificationService.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace NotificationService.Tests.Services;

public sealed class EmailNotificationSenderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly NotifDbContext _db;
    private readonly EmailNotificationSender _sender;

    public EmailNotificationSenderTests()
    {
        // Sqlite in-memory gives a real relational database per test (lives while the connection is open)
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new NotifDbContext(new DbContextOptionsBuilder<NotifDbContext>()
            .UseSqlite(_connection)
            .Options);
        _db.Database.EnsureCreated();
        _sender = new EmailNotificationSender(_db, NullLogger<EmailNotificationSender>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task SendEmailAsync_PersistsDeliveredEmailLog()
    {
        var recipientId = Guid.NewGuid();

        await _sender.SendEmailAsync(recipientId, "Your booking has been created!");

        var log = _db.NotificationLogs.Single();
        log.RecipientId.Should().Be(recipientId);
        log.Message.Should().Be("Your booking has been created!");
        log.Channel.Should().Be("Email");
        log.IsDelivered.Should().BeTrue();
        log.SentAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SendEmailAsync_EachCallWritesASeparateLogRow()
    {
        var recipientId = Guid.NewGuid();

        await _sender.SendEmailAsync(recipientId, "first");
        await _sender.SendEmailAsync(recipientId, "second");

        _db.NotificationLogs.Count().Should().Be(2);
        _db.NotificationLogs.Select(l => l.Message).Should().BeEquivalentTo("first", "second");
    }
}
