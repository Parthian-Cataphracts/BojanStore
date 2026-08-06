namespace Bojan.Infrastructure.Auth;

/// <summary>
/// Whether the console stand-ins for SMS and email may be used, and whether
/// they may print what they were asked to send.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ConsoleSmsSender"/> and <see cref="ConsoleEmailSender"/> are the
/// only implementations of their ports, and they were registered
/// unconditionally — so a production deployment ran them by default and wrote
/// every one-time sign-in code and every password-reset token to its own log at
/// Information and Warning. Anyone able to read the log could take over any
/// account by phone number alone, without holding a single credential.
/// </para>
/// <para>
/// Two separate switches, because they answer different questions. Deliveries
/// have to keep working on a developer's machine with no gateway, so
/// <see cref="Allowed"/> is turned on for Development in <c>Program.cs</c> the
/// way <c>AddDevelopmentSignIn</c> is. Printing the message body is what makes
/// the log a credential store, so <see cref="LogMessageBodies"/> is off unless
/// the same switch says the host is a developer's.
/// </para>
/// <para>
/// A deployment that genuinely wants the stand-ins — a staging box with no
/// gateway contract yet — sets <c>Notifications:AllowConsoleSenders</c> and
/// says so deliberately. There is no default that works, matching this
/// solution's treatment of <c>Jwt:SigningKey</c> and <c>Payment:GatewayUrl</c>.
/// </para>
/// </remarks>
public sealed class ConsoleSenderOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Set from <c>Notifications:AllowConsoleSenders</c>, or by the host in Development.</summary>
    public bool Allowed { get; set; }

    /// <summary>Never on outside Development — the body carries the code or the token.</summary>
    public bool LogMessageBodies { get; set; }
}
