using Bojan.Application.Accounts;
using Bojan.Infrastructure.Auth;
using Bojan.Infrastructure.Payments;
using Bojan.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;

namespace Bojan.Api.Tests;

/// <summary>
/// Every environment variable the deployment sets has to reach the property it
/// is named for.
/// </summary>
/// <remarks>
/// <para>
/// Options binding matches on the property name and says nothing when it
/// matches none — an unrecognised key is simply not applied. So
/// <c>Notifications__AllowConsoleSenders</c>, which the compose file sets, the
/// validation message tells the operator to set, and the type's own
/// documentation names, bound to a property called <c>Allowed</c> and therefore
/// bound to nothing. The flag could not be turned on from configuration by any
/// spelling, and every container refused to start quoting a variable that would
/// not have helped.
/// </para>
/// <para>
/// Nothing else would have caught it. It compiles, it has no warning, and the
/// unit tests construct the options object directly — where the property does
/// exist. Only binding a real key to a real object shows the gap, so these
/// tests bind the exact strings <c>docker-compose.yml</c> uses.
/// </para>
/// </remarks>
public class ConfigurationBindingTests
{
    private static T Bind<T>(string section, params (string Key, string Value)[] settings)
    {
        // Double underscore is how a shell passes a nested key, and is what the
        // compose file writes — so it is what the test writes too.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s =>
                new KeyValuePair<string, string?>(s.Key.Replace("__", ":"), s.Value)))
            .Build();

        // `Get<T>` rather than binding onto an instance: some of these option
        // types have required members and cannot be constructed empty.
        return configuration.GetSection(section).Get<T>()
            ?? throw new InvalidOperationException($"Nothing bound for section '{section}'.");
    }

    [Fact]
    public void Notifications__AllowConsoleSenders_reaches_the_option()
    {
        var options = Bind<ConsoleSenderOptions>(
            ConsoleSenderOptions.SectionName,
            ("Notifications__AllowConsoleSenders", "true"));

        Assert.True(
            options.AllowConsoleSenders,
            "The API refuses to start unless this binds — see the validation in AddInfrastructure.");
    }

    /// <summary>
    /// The other direction, and the one the compose file actually sends when
    /// the deployment has not opted in: the key is present and false.
    /// </summary>
    [Fact]
    public void A_console_sender_flag_set_to_false_stays_off()
    {
        var options = Bind<ConsoleSenderOptions>(
            ConsoleSenderOptions.SectionName,
            ("Notifications__AllowConsoleSenders", "false"));

        Assert.False(options.AllowConsoleSenders);
        // Never implied by the other switch — printing the body is what turns
        // the log into a credential store.
        Assert.False(options.LogMessageBodies);
    }

    [Fact]
    public void Wallet__ManualTopUpEnabled_reaches_the_option()
    {
        var options = Bind<WalletOptions>(
            WalletOptions.SectionName,
            ("Wallet__ManualTopUpEnabled", "true"),
            ("Wallet__MinimumAmount", "25000"));

        Assert.True(options.ManualTopUpEnabled);
        Assert.Equal(25_000, options.MinimumAmount);
    }

    [Fact]
    public void Storage__PublicBaseUrl_reaches_the_option()
    {
        var options = Bind<FileStorageOptions>(
            FileStorageOptions.SectionName,
            ("Storage__PublicBaseUrl", "https://example.test/media"),
            ("Storage__RootPath", "/data/uploads"));

        Assert.Equal("https://example.test/media", options.PublicBaseUrl);
        Assert.Equal("/data/uploads", options.RootPath);
    }

    [Fact]
    public void Payment__ReturnUrl_reaches_the_option()
    {
        var options = Bind<PaymentOptions>(
            PaymentOptions.SectionName,
            ("Payment__ReturnUrl", "https://example.test/checkout/payment/callback"));

        Assert.Equal("https://example.test/checkout/payment/callback", options.ReturnUrl);
    }

    [Fact]
    public void Jwt__SigningKey_reaches_the_option()
    {
        var options = Bind<JwtOptions>(
            JwtOptions.SectionName,
            ("Jwt__SigningKey", new string('k', 32)),
            ("Jwt__Issuer", "bojan-api"));

        Assert.Equal(new string('k', 32), options.SigningKey);
        Assert.Equal("bojan-api", options.Issuer);
    }
}
