using Bojan.Application.Common;
using Bojan.Infrastructure;
using Bojan.Infrastructure.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bojan.Api.Tests;

/// <summary>
/// The sandbox gateway must not stand in for a real one.
/// </summary>
/// <remarks>
/// <see cref="SandboxPaymentGateway.VerifyAsync"/> returns <c>true</c> without
/// contacting anything, so a deployment that meant to use a real gateway and
/// got this instead would mark orders paid for money nobody took. The class
/// documented a gate on <c>Payment:GatewayUrl</c> that did not exist — it was
/// registered unconditionally — and these cover the one that does now.
/// </remarks>
public sealed class PaymentGatewayGateTests
{
    private static ServiceProvider Build(string? gatewayUrl)
    {
        var settings = new Dictionary<string, string?>
        {
            // Enough for the other startup validation to pass, so a failure
            // here is about the gateway and nothing else.
            ["Jwt:SigningKey"] = "test-signing-key-at-least-32-characters-long",
            ["ConnectionStrings:Bojan"] = "Host=localhost;Database=unused",
        };

        if (gatewayUrl is not null)
        {
            settings["Payment:GatewayUrl"] = gatewayUrl;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void With_no_gateway_configured_the_sandbox_is_used_deliberately()
    {
        using var provider = Build(null);

        // Reading the options is what triggers validation.
        var options = provider.GetRequiredService<IOptions<PaymentOptions>>().Value;

        Assert.True(string.IsNullOrWhiteSpace(options.GatewayUrl));
        Assert.IsType<SandboxPaymentGateway>(provider.GetRequiredService<IPaymentGateway>());
    }

    /// <summary>
    /// Configuring a real gateway with no real adapter is a startup failure,
    /// not a silent fall back to the one that approves everything.
    /// </summary>
    [Fact]
    public void Configuring_a_gateway_url_refuses_to_resolve_rather_than_using_the_sandbox()
    {
        using var provider = Build("https://gateway.example/pay");

        var failure = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<PaymentOptions>>().Value);

        Assert.Contains("Payment:GatewayUrl", failure.Message, StringComparison.Ordinal);
    }
}
