using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Admin;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bojan.Api.Tests;

/// <summary>
/// Which gateway takes the money follows the stored setting, per call.
/// </summary>
/// <remarks>
/// <para>
/// The choice used to be made at registration from <c>Payment:GatewayUrl</c>,
/// which meant the shop could only change gateways by being redeployed. It is
/// now a row the owner writes from the panel, and these cover the three
/// properties that arrangement has to keep.
/// </para>
/// <para>
/// The one that matters most is the last: a stub that approves everything must
/// never be able to put spendable balance in a wallet, and neither must a shop
/// that has not chosen a gateway at all.
/// </para>
/// </remarks>
public sealed class PaymentGatewaySelectionTests : IClassFixture<BojanApiFactory>
{
    private readonly BojanApiFactory _factory;

    public PaymentGatewaySelectionTests(BojanApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    private async Task SetProviderAsync(string provider, string? merchantId = null)
    {
        await _factory.WithDbAsync(async db =>
        {
            await SetAsync(db, "provider", provider);

            if (merchantId is not null)
            {
                // Written through the store so it is sealed the way the panel
                // would have sealed it — a raw row would not decrypt.
                using var scope = _factory.Services.CreateScope();
                var store = scope.ServiceProvider
                    .GetRequiredService<Infrastructure.Payments.PaymentGatewaySettingsStore>();

                await store.SaveAsync(
                    new PaymentGatewaySettingsDto(
                        provider,
                        UseSandboxEndpoints: true,
                        HasMerchantId: false,
                        CallbackUrl: "https://shop.example/checkout/payment/callback",
                        Description: "تست"),
                    merchantId,
                    CancellationToken.None);
            }
        });
    }

    private static async Task SetAsync(BojanDbContext db, string key, string value)
    {
        var entry = await db.Settings.FirstOrDefaultAsync(s => s.Section == "payment" && s.Key == key);

        if (entry is null)
        {
            db.Settings.Add(new SettingEntry
            {
                Section = "payment",
                Key = key,
                Value = value,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            entry.Value = value;
        }

        await db.SaveChangesAsync();
    }

    private IPaymentGateway Gateway => _factory.Services.GetRequiredService<IPaymentGateway>();

    [Fact]
    public async Task With_no_provider_chosen_starting_a_payment_fails_rather_than_pretending()
    {
        await SetProviderAsync(PaymentProviders.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Gateway.StartAsync("BJ-1", 50_000, CancellationToken.None));
    }

    /// <summary>
    /// Verification is the one that must not resolve to a boolean when nobody
    /// was asked: false is written to an order as "the bank declined this".
    /// </summary>
    [Fact]
    public async Task With_no_provider_chosen_verification_throws_rather_than_answering_no()
    {
        await SetProviderAsync(PaymentProviders.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Gateway.VerifyAsync("A0000", 50_000, CancellationToken.None));
    }

    [Fact]
    public async Task Choosing_the_stub_makes_the_money_path_reachable_without_a_bank()
    {
        await SetProviderAsync(PaymentProviders.Sandbox);

        var session = await Gateway.StartAsync("BJ-2", 50_000, CancellationToken.None);

        Assert.NotEmpty(session.Reference);
        Assert.Contains("Authority=", session.PaymentUrl, StringComparison.Ordinal);
    }

    /// <summary>
    /// The guarantee wallet top-up depends on. <c>StartGatewayTopUpAsync</c>
    /// refuses outright unless this is true, because a verification nobody
    /// asked a bank would let it mint balance out of nothing.
    /// </summary>
    [Theory]
    [InlineData(PaymentProviders.None)]
    [InlineData(PaymentProviders.Sandbox)]
    public async Task Neither_no_gateway_nor_the_stub_counts_as_taking_real_money(string provider)
    {
        await SetProviderAsync(provider);

        Assert.False(await Gateway.TakesRealMoneyAsync(CancellationToken.None));
    }

    /// <summary>
    /// A provider selected but left without a credential reads as no provider.
    /// Stored half-configured it looks chosen on the screen and fails on the
    /// first order — and, worse, would have counted as a real gateway to the
    /// wallet.
    /// </summary>
    [Fact]
    public async Task A_gateway_chosen_without_a_merchant_id_does_not_count_as_configured()
    {
        await SetProviderAsync(PaymentProviders.ZarinPal, merchantId: string.Empty);

        Assert.False(await Gateway.TakesRealMoneyAsync(CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Gateway.StartAsync("BJ-3", 50_000, CancellationToken.None));
    }

    [Fact]
    public async Task A_gateway_with_a_merchant_id_counts_as_taking_real_money()
    {
        await SetProviderAsync(PaymentProviders.ZarinPal, merchantId: "00000000-0000-0000-0000-000000000000");

        Assert.True(await Gateway.TakesRealMoneyAsync(CancellationToken.None));
    }
}
