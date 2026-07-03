using BookingSystem.PaymentService.Api.Features.ProcessPayment;
using BookingSystem.PaymentService.Infrastructure.Gateway;
using BookingSystem.PaymentService.Infrastructure.Outbox;
using BookingSystem.PaymentService.Infrastructure.Persistence;
using BookingSystem.Shared.Contracts.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace PaymentService.Tests.Features;

public class ProcessPaymentHandlerTests
{
    private readonly IPaymentRepository _repo = Substitute.For<IPaymentRepository>();
    private readonly IPaymentGateway _gateway = Substitute.For<IPaymentGateway>();

    private Payment? _added;
    private string? _statusWhenAdded;
    private readonly List<OutboxMessage> _outbox = [];

    public ProcessPaymentHandlerTests()
    {
        _repo.AddAsync(Arg.Do<Payment>(p =>
        {
            _added = p;
            _statusWhenAdded = p.Status;
        }), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _repo.AddOutboxMessage(Arg.Do<OutboxMessage>(_outbox.Add));
    }

    private ProcessPaymentHandler CreateHandler() =>
        new(_repo, _gateway, NullLogger<ProcessPaymentHandler>.Instance);

    private static ProcessPaymentCommand CreateCommand() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 400m, "USD", "Card");

    private void SetupGateway(PaymentGatewayResult result) =>
        _gateway.ChargeAsync(Arg.Any<PaymentChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns(result);

    [Fact]
    public async Task Handle_GatewayApproves_MarksSucceededAndStagesOutboxEvent()
    {
        SetupGateway(PaymentGatewayResult.Approved("ref-1"));

        var result = await CreateHandler().Handle(CreateCommand(), default);

        _statusWhenAdded.Should().Be(PaymentStatus.Pending, "the attempt must be recorded before charging");
        _added!.Status.Should().Be(PaymentStatus.Succeeded);
        result.Should().Be(_added.Id.Value);
        _outbox.Should().ContainSingle();
        _outbox[0].Topic.Should().Be(KafkaTopics.PaymentSucceeded);
        _outbox[0].EventType.Should().Be("PaymentSucceededIntegrationEvent");
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UsesPaymentIdAsIdempotencyKey()
    {
        SetupGateway(PaymentGatewayResult.Approved("ref-1"));
        PaymentChargeRequest? charge = null;
        _gateway.ChargeAsync(Arg.Do<PaymentChargeRequest>(r => charge = r), Arg.Any<CancellationToken>())
            .Returns(PaymentGatewayResult.Approved("ref-1"));
        var cmd = CreateCommand();

        await CreateHandler().Handle(cmd, default);

        charge.Should().NotBeNull();
        charge!.IdempotencyKey.Should().Be(_added!.Id.Value.ToString());
        charge.BookingId.Should().Be(cmd.BookingId);
        charge.Amount.Should().Be(400m);
        charge.Currency.Should().Be("USD");
        charge.PaymentMethod.Should().Be("Card");
    }

    [Fact]
    public async Task Handle_GatewayDeclines_MarksFailedStagesFailureEventAndThrows()
    {
        SetupGateway(PaymentGatewayResult.Declined("insufficient funds"));

        var act = () => CreateHandler().Handle(CreateCommand(), default);

        await act.Should().ThrowAsync<PaymentGatewayException>()
            .WithMessage("insufficient funds");
        _added!.Status.Should().Be(PaymentStatus.Failed);
        _outbox.Should().ContainSingle();
        _outbox[0].Topic.Should().Be(KafkaTopics.PaymentFailed);
        _outbox[0].EventType.Should().Be("PaymentFailedIntegrationEvent");
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GatewayThrows_MarksFailedAndRethrows()
    {
        _gateway.ChargeAsync(Arg.Any<PaymentChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns<PaymentGatewayResult>(_ => throw new HttpRequestException("network error"));

        var act = () => CreateHandler().Handle(CreateCommand(), default);

        await act.Should().ThrowAsync<HttpRequestException>();
        _added!.Status.Should().Be(PaymentStatus.Failed);
        _outbox.Should().ContainSingle(m => m.Topic == KafkaTopics.PaymentFailed);
    }

    [Fact]
    public async Task Handle_Cancellation_DoesNotMarkFailed()
    {
        _gateway.ChargeAsync(Arg.Any<PaymentChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns<PaymentGatewayResult>(_ => throw new OperationCanceledException());

        var act = () => CreateHandler().Handle(CreateCommand(), default);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _added!.Status.Should().Be(PaymentStatus.Pending, "a cancelled request is not a payment failure");
        _outbox.Should().BeEmpty();
        await _repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
