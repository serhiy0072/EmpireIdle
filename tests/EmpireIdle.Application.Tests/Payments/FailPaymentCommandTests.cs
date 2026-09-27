using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Payments.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Payments;

/// <summary>
/// Невдала оплата з вебхука: відкладений платіж відхилено або сесія прострочена.
/// Stripe повторює подію — обробка ідемпотентна, а зарахований платіж не «провалюється».
/// </summary>
public class FailPaymentCommandTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    private const string SessionId = "cs_test_1";

    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private FailPaymentCommandHandler Handler() => new(_payments, _unitOfWork, NullLogger<FailPaymentCommandHandler>.Instance);

    private Payment GivenPayment()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 1, "gems_small", 100, 499, "usd", SessionId, Now);
        _payments.GetBySessionIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(payment);

        return payment;
    }

    [Fact]
    public async Task Handle_ShouldMarkAPendingPaymentFailed()
    {
        var payment = GivenPayment();

        await Handler().Handle(new FailPaymentCommand(SessionId), CancellationToken.None);

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Gems уже в гаманці — пізній «failed» статус не чіпає.</summary>
    [Fact]
    public async Task Handle_ShouldLeaveACompletedPaymentAlone()
    {
        var payment = GivenPayment();
        payment.Complete(Now);

        await Handler().Handle(new FailPaymentCommand(SessionId), CancellationToken.None);

        Assert.Equal(PaymentStatus.Completed, payment.Status);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldIgnoreARepeatedFailure()
    {
        var payment = GivenPayment();
        payment.Fail();

        await Handler().Handle(new FailPaymentCommand(SessionId), CancellationToken.None);

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldIgnoreAnUnknownSession()
    {
        _payments.GetBySessionIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns((Payment?)null);

        await Handler().Handle(new FailPaymentCommand(SessionId), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
