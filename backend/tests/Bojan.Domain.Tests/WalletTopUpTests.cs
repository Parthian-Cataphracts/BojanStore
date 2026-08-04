using Bojan.Domain.Common;
using Bojan.Domain.Customers;

namespace Bojan.Domain.Tests;

/// <summary>
/// The transitions that keep a wallet from being credited for money nobody
/// sent. The arithmetic lives in <see cref="WalletSplitTests"/>; this is about
/// who may decide, and how many times.
/// </summary>
public class WalletTopUpTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    private static WalletTopUp Pending(WalletTopUpMethod method = WalletTopUpMethod.Manual) => new()
    {
        CustomerId = Guid.NewGuid(),
        Amount = new Money(250_000),
        Method = method,
    };

    [Fact]
    public void A_filed_request_starts_pending_and_undecided()
    {
        var topUp = Pending();

        Assert.Equal(WalletTopUpStatus.Pending, topUp.Status);
        Assert.Null(topUp.ReviewedAtUtc);
        Assert.Null(topUp.ReviewedByAdminId);
    }

    [Fact]
    public void Approving_records_who_decided_it_and_when()
    {
        var topUp = Pending();
        var admin = Guid.NewGuid();

        Assert.True(topUp.Approve(admin, Now, "matched the statement"));

        Assert.Equal(WalletTopUpStatus.Approved, topUp.Status);
        Assert.Equal(admin, topUp.ReviewedByAdminId);
        Assert.Equal(Now, topUp.ReviewedAtUtc);
        Assert.Equal("matched the statement", topUp.ReviewNote);
    }

    /// <summary>
    /// The one that matters: the caller credits the wallet only when this
    /// returns true, so a second approval returning false is what stops a
    /// retried callback or a double-clicked button paying twice.
    /// </summary>
    [Fact]
    public void Approving_twice_reports_the_second_as_a_no_op()
    {
        var topUp = Pending();
        var first = Guid.NewGuid();

        Assert.True(topUp.Approve(first, Now));
        Assert.False(topUp.Approve(Guid.NewGuid(), Now.AddMinutes(5)));

        // And the second attempt did not overwrite who owns the decision.
        Assert.Equal(first, topUp.ReviewedByAdminId);
        Assert.Equal(Now, topUp.ReviewedAtUtc);
    }

    [Fact]
    public void A_rejected_request_cannot_later_be_approved()
    {
        var topUp = Pending();

        Assert.True(topUp.Reject(Guid.NewGuid(), Now, "no transfer found"));
        Assert.False(topUp.Approve(Guid.NewGuid(), Now.AddHours(1)));

        Assert.Equal(WalletTopUpStatus.Rejected, topUp.Status);
    }

    [Fact]
    public void An_approved_request_cannot_later_be_rejected()
    {
        var topUp = Pending();

        Assert.True(topUp.Approve(Guid.NewGuid(), Now));
        Assert.False(topUp.Reject(Guid.NewGuid(), Now.AddHours(1)));

        Assert.Equal(WalletTopUpStatus.Approved, topUp.Status);
    }

    [Fact]
    public void A_gateway_approval_has_no_operator_behind_it()
    {
        var topUp = Pending(WalletTopUpMethod.Gateway);

        Assert.True(topUp.Approve(reviewedByAdminId: null, Now));

        Assert.Equal(WalletTopUpStatus.Approved, topUp.Status);
        Assert.Null(topUp.ReviewedByAdminId);
        Assert.Equal(Now, topUp.ReviewedAtUtc);
    }
}
