using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Common;
using Bojan.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// The operator's card-to-card review queue — <c>POST /api/admin/wallet/topups/decide</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the one admin write whose mistake costs the store cash rather than
/// data: approving credits spendable balance that buys real goods. What makes
/// it safe is that a decision is one-way — <see cref="WalletTopUp.Approve"/>
/// refuses a request that is not pending — and these tests hold that property
/// at the endpoint, where a double-clicked button or two operators working the
/// same queue actually arrive.
/// </para>
/// <para>
/// The status check is only trustworthy if it is made under the top-up's own
/// row lock, which is why the service loads it through
/// <c>FindWalletTopUpAsync</c> inside a transaction. SQLite serialises writers
/// on its own so the ordering cannot be forced here; what these cover is that
/// the guard exists, that it is the row's own committed status being read, and
/// that a refused second decision credits nothing.
/// </para>
/// </remarks>
public sealed class WalletTopUpDecisionTests : IAsyncLifetime, IDisposable
{
    private const long Amount = 250_000;

    private readonly BojanApiFactory _factory = new();
    private HttpClient _owner = null!;
    private Guid _customerId;
    private Guid _topUpId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            var customer = await TestData.AddCustomerAsync(db, "09121110044");
            var admin = await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@bojan.test");

            // Filed directly rather than through the customer endpoint: this
            // suite is about deciding a pending request, and manual top-ups are
            // disabled by default so submitting one would be refused before it
            // could be reviewed.
            var ledger = new WalletTransaction
            {
                CustomerId = customer.Id,
                Title = "افزایش اعتبار (کارت به کارت)",
                Amount = Amount,
                Status = WalletTransactionStatus.Pending,
                Icon = "add_circle",
            };

            var topUp = new WalletTopUp
            {
                CustomerId = customer.Id,
                Amount = new Money(Amount),
                Method = WalletTopUpMethod.Manual,
                TrackingNumber = "123456",
                PaidOn = DateOnly.FromDateTime(DateTime.UtcNow),
                ReceiptUrl = "/uploads/returns/receipt.png",
                WalletTransactionId = ledger.Id,
            };

            db.WalletTransactions.Add(ledger);
            db.WalletTopUps.Add(topUp);
            await db.SaveChangesAsync();

            _customerId = customer.Id;
            _topUpId = topUp.Id;
            _owner = _factory.CreateAdminClient(admin.Id);
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _owner?.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> DecideAsync(bool approve) =>
        _owner.PostAsJsonAsync(
            "/api/admin/wallet/topups/decide",
            new { id = _topUpId.ToString(), approve, note = (string?)null });

    [Fact]
    public async Task Approving_a_transfer_credits_the_wallet_once_and_settles_its_ledger_row()
    {
        (await DecideAsync(approve: true)).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(Amount, (await db.Customers.SingleAsync(c => c.Id == _customerId)).WalletBalance.Amount);

            var topUp = await db.WalletTopUps.SingleAsync(t => t.Id == _topUpId);
            Assert.Equal(WalletTopUpStatus.Approved, topUp.Status);

            // The request and its ledger line describe the same money, so the
            // line must not still read Pending on a wallet screen.
            var ledger = await db.WalletTransactions.SingleAsync(t => t.Id == topUp.WalletTransactionId);
            Assert.Equal(WalletTransactionStatus.Success, ledger.Status);
        });
    }

    /// <summary>
    /// The property the whole design rests on: deciding twice credits once.
    /// </summary>
    /// <remarks>
    /// A second approval must be refused on the row's committed status rather
    /// than adding the amount again. Before the decision was made under the
    /// top-up's own lock inside a transaction, the guard read state fetched
    /// before any lock was taken, so two approvals arriving together could both
    /// see Pending and both credit — one transfer, twice the balance.
    /// </remarks>
    [Fact]
    public async Task Approving_the_same_transfer_twice_credits_it_once()
    {
        (await DecideAsync(approve: true)).EnsureSuccessStatusCode();

        var second = await DecideAsync(approve: true);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        await _factory.WithDbAsync(async db =>
            Assert.Equal(Amount, (await db.Customers.SingleAsync(c => c.Id == _customerId)).WalletBalance.Amount));
    }

    /// <summary>A decision is one-way in both directions — a rejection cannot be walked back into a credit.</summary>
    [Fact]
    public async Task A_rejected_transfer_cannot_then_be_approved()
    {
        (await DecideAsync(approve: false)).EnsureSuccessStatusCode();

        var reversal = await DecideAsync(approve: true);
        Assert.Equal(HttpStatusCode.BadRequest, reversal.StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(0, (await db.Customers.SingleAsync(c => c.Id == _customerId)).WalletBalance.Amount);

            var topUp = await db.WalletTopUps.SingleAsync(t => t.Id == _topUpId);
            Assert.Equal(WalletTopUpStatus.Rejected, topUp.Status);

            var ledger = await db.WalletTransactions.SingleAsync(t => t.Id == topUp.WalletTransactionId);
            Assert.Equal(WalletTransactionStatus.Failed, ledger.Status);
        });
    }

    /// <summary>
    /// Only an owner works this queue, and the role is read from this API's own
    /// records rather than from anything the caller sent.
    /// </summary>
    [Fact]
    public async Task An_operator_who_is_not_an_owner_cannot_decide_a_transfer()
    {
        Guid supportId = default;
        await _factory.WithDbAsync(async db =>
            supportId = (await TestData.AddAdminAsync(db, AdminRole.Support, "support@bojan.test")).Id);

        using var support = _factory.CreateAdminClient(supportId);
        var response = await support.PostAsJsonAsync(
            "/api/admin/wallet/topups/decide",
            new { id = _topUpId.ToString(), approve = true, note = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await _factory.WithDbAsync(async db =>
            Assert.Equal(0, (await db.Customers.SingleAsync(c => c.Id == _customerId)).WalletBalance.Amount));
    }
}
