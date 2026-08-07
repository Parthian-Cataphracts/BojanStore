using Bojan.Infrastructure.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bojan.Api.Tests;

/// <summary>
/// The console senders were the only implementations of their ports and were
/// registered with no gate, so a production host printed every one-time code
/// and every password-reset token to its own log — a log anyone who can read it
/// can sign in with.
/// </summary>
public sealed class ConsoleSenderGuardTests
{
    /// <summary>Captures what a sender wrote, so the assertions can be about the text itself.</summary>
    private sealed class Recorder<T> : ILogger<T>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));

        public string Written => string.Join("\n", Lines);
    }

    private static IOptions<ConsoleSenderOptions> Configured(bool logBodies) =>
        Options.Create(new ConsoleSenderOptions { AllowConsoleSenders = true, LogMessageBodies = logBodies });

    [Fact]
    public async Task The_sign_in_code_is_not_logged_unless_the_host_allows_it()
    {
        var recorder = new Recorder<ConsoleSmsSender>();
        var sender = new ConsoleSmsSender(Configured(logBodies: false), recorder);

        await sender.SendAsync("09120000000", "کد ورود شما: 483920", CancellationToken.None);

        Assert.DoesNotContain("483920", recorder.Written, StringComparison.Ordinal);
        // The recipient stays, so a deployment running the stand-in is still
        // visible in the logs rather than only in an incident.
        Assert.Contains("09120000000", recorder.Written, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_reset_token_is_not_logged_unless_the_host_allows_it()
    {
        var recorder = new Recorder<ConsoleEmailSender>();
        var sender = new ConsoleEmailSender(Configured(logBodies: false), recorder);

        await sender.SendAsync(
            "a@b.test", "بازیابی رمز", "https://shop.test/reset?token=SECRET-TOKEN", CancellationToken.None);

        Assert.DoesNotContain("SECRET-TOKEN", recorder.Written, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_developer_host_still_sees_the_code_it_has_no_gateway_to_deliver()
    {
        var recorder = new Recorder<ConsoleSmsSender>();
        var sender = new ConsoleSmsSender(Configured(logBodies: true), recorder);

        await sender.SendAsync("09120000000", "کد ورود شما: 483920", CancellationToken.None);

        Assert.Contains("483920", recorder.Written, StringComparison.Ordinal);
    }

    [Fact]
    public void A_host_that_has_not_opted_in_refuses_to_resolve_the_options()
    {
        // What ValidateOnStart does at boot, in miniature: the same predicate,
        // against an unconfigured section.
        var services = new ServiceCollection();
        services.AddOptions<ConsoleSenderOptions>()
            .Validate(console => console.AllowConsoleSenders, "console senders not permitted")
            .ValidateOnStart();

        using var provider = services.BuildServiceProvider();

        var failure = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ConsoleSenderOptions>>().Value);

        Assert.Contains("console senders not permitted", failure.Message, StringComparison.Ordinal);
    }
}
