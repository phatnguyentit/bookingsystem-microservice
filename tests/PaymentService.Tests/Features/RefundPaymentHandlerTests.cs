using BookingSystem.PaymentService.Api.Features.RefundPayment;
using BookingSystem.PaymentService.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace PaymentService.Tests.Features;

public class RefundPaymentHandlerTests
{
    private readonly IPaymentRepository _repo = Substitute.For<IPaymentRepository>();

    private RefundPaymentHandler CreateHandler() =>
        new(_repo, NullLogger<RefundPaymentHandler>.Instance);

    private Payment SetupCapturedPayment(string status)
    {
        var payment = new Payment
        {
            Id = PaymentId.New(),
            BookingId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 400m,
            Currency = "USD",
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
        _repo.GetCapturedByBookingIdAsync(payment.BookingId, Arg.Any<CancellationToken>())
            .Returns(payment);
        return payment;
    }

    [Fact]
    public async Task Handle_NoCapturedPayment_LogsAndReturnsWithoutThrowing()
    {
        _repo.GetCapturedByBookingIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Payment?)null);

        var act = () => CreateHandler().Handle(new RefundPaymentCommand(Guid.NewGuid(), "reason"), default);

        await act.Should().NotThrowAsync("retrying could never make a payment appear");
        await _repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CapturedPayment_RecordsRefundObligationAndCommits()
    {
        var payment = SetupCapturedPayment(PaymentStatus.Succeeded);

        await CreateHandler().Handle(new RefundPaymentCommand(payment.BookingId, "confirmation failed"), default);

        payment.Status.Should().Be(PaymentStatus.RefundPending);
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(PaymentStatus.RefundPending)]
    [InlineData(PaymentStatus.Refunded)]
    [InlineData(PaymentStatus.RefundFailed)]
    public async Task Handle_RefundAlreadyInProgressOrTerminal_IsIdempotentNoOp(string status)
    {
        var payment = SetupCapturedPayment(status);

        await CreateHandler().Handle(new RefundPaymentCommand(payment.BookingId, "redelivered"), default);

        payment.Status.Should().Be(status, "a duplicate compensation event must not reset refund state");
        await _repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
