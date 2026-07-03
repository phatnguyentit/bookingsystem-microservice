using BookingSystem.UserService.Api.Features.Create;
using BookingSystem.UserService.Infrastructure.Persistence;
using BookingSystem.UserService.Infrastructure.Repositories;
using FluentAssertions;
using NSubstitute;

namespace UserService.Tests.Features;

public class CreateUserHandlerTests
{
    private readonly IUserRepository _repo = Substitute.For<IUserRepository>();

    [Fact]
    public async Task Handle_ValidCommand_PersistsUserWithCommandValues()
    {
        User? added = null;
        await _repo.AddAsync(Arg.Do<User>(u => added = u), Arg.Any<CancellationToken>());
        var cmd = new CreateUserCommand("alice@example.com", "Alice Smith", "hashed-pw");

        var result = await new CreateUserHandler(_repo).Handle(cmd, default);

        added.Should().NotBeNull();
        added!.Email.Should().Be("alice@example.com");
        added.FullName.Should().Be("Alice Smith");
        added.PasswordHash.Should().Be("hashed-pw");
        added.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsGeneratedId()
    {
        User? added = null;
        await _repo.AddAsync(Arg.Do<User>(u => added = u), Arg.Any<CancellationToken>());

        var result = await new CreateUserHandler(_repo)
            .Handle(new CreateUserCommand("a@b.com", "A", "h"), default);

        result.Should().NotBeEmpty();
        result.Should().Be(added!.Id);
    }
}
