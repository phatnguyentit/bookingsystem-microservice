using BookingSystem.UserService.Api.Features.GetById;
using BookingSystem.UserService.Infrastructure.Persistence;
using BookingSystem.UserService.Infrastructure.Repositories;
using FluentAssertions;
using NSubstitute;

namespace UserService.Tests.Features;

public class GetUserByIdHandlerTests
{
    private readonly IUserRepository _repo = Substitute.For<IUserRepository>();

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNull()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var dto = await new GetUserByIdHandler(_repo).Handle(new GetUserByIdQuery(Guid.NewGuid()), default);

        dto.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UserExists_MapsAllFieldsExceptPasswordHash()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "alice@example.com",
            FullName = "Alice Smith",
            PasswordHash = "secret-hash",
            CreatedAt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };
        _repo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var dto = await new GetUserByIdHandler(_repo).Handle(new GetUserByIdQuery(user.Id), default);

        dto.Should().Be(new UserDto(user.Id, "alice@example.com", "Alice Smith", user.CreatedAt));
    }
}
