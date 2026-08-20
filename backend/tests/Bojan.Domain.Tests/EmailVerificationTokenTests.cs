using Bojan.Domain.Identity;

namespace Bojan.Domain.Tests;

public class EmailVerificationTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static EmailVerificationToken MakeToken(DateTimeOffset? expiresAt = null) => new()
    {
        CustomerId = Guid.NewGuid(),
        Email = "shopper@example.com",
        TokenHash = "hash-of-token",
        ExpiresAtUtc = expiresAt ?? Now.AddHours(24),
    };

    [Fact]
    public void A_fresh_token_is_not_spent()
    {
        var token = MakeToken();

        Assert.False(token.IsSpent(Now));
    }

    [Fact]
    public void Consuming_a_fresh_token_succeeds_and_marks_it_spent()
    {
        var token = MakeToken();

        var consumed = token.Consume(Now);

        Assert.True(consumed);
        Assert.True(token.IsSpent(Now));
    }

    [Fact]
    public void Consuming_the_same_token_twice_fails_the_second_time()
    {
        var token = MakeToken();
        token.Consume(Now);

        var second = token.Consume(Now);

        Assert.False(second);
    }

    [Fact]
    public void An_expired_token_cannot_be_consumed()
    {
        var token = MakeToken(expiresAt: Now.AddSeconds(-1));

        var consumed = token.Consume(Now);

        Assert.False(consumed);
    }

    [Fact]
    public void An_expired_token_reports_itself_as_spent()
    {
        var token = MakeToken(expiresAt: Now.AddSeconds(-1));

        Assert.True(token.IsSpent(Now));
    }
}
