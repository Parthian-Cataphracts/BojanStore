using Bojan.Domain.Identity;

namespace Bojan.Domain.Tests;

public class OtpChallengeTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const string RightHash = "hash-of-11111";
    private const string WrongHash = "hash-of-00000";

    private static OtpChallenge MakeChallenge(DateTimeOffset? expiresAt = null) => new()
    {
        Phone = "09121234567",
        CodeHash = RightHash,
        ExpiresAtUtc = expiresAt ?? Now.AddMinutes(5),
    };

    [Fact]
    public void Correct_code_is_accepted_and_consumes_the_challenge()
    {
        var challenge = MakeChallenge();

        var outcome = challenge.Validate(RightHash, Now);

        Assert.Equal(OtpChallenge.Outcome.Accepted, outcome);
        Assert.True(challenge.Consumed);
    }

    [Fact]
    public void Wrong_code_is_rejected_without_consuming_the_challenge()
    {
        var challenge = MakeChallenge();

        var outcome = challenge.Validate(WrongHash, Now);

        Assert.Equal(OtpChallenge.Outcome.WrongCode, outcome);
        Assert.False(challenge.Consumed);
    }

    [Fact]
    public void Wrong_code_increments_the_attempt_count()
    {
        var challenge = MakeChallenge();

        challenge.Validate(WrongHash, Now);

        Assert.Equal(1, challenge.Attempts);
    }

    [Fact]
    public void Expired_challenge_is_rejected_even_with_the_right_code()
    {
        var challenge = MakeChallenge(expiresAt: Now.AddMinutes(-1));

        var outcome = challenge.Validate(RightHash, Now);

        Assert.Equal(OtpChallenge.Outcome.Expired, outcome);
        Assert.False(challenge.Consumed);
    }

    [Fact]
    public void Already_consumed_challenge_cannot_be_validated_again()
    {
        var challenge = MakeChallenge();
        challenge.Validate(RightHash, Now);

        var outcome = challenge.Validate(RightHash, Now);

        Assert.Equal(OtpChallenge.Outcome.AlreadyUsed, outcome);
    }

    [Fact]
    public void Fifth_wrong_attempt_burns_the_challenge()
    {
        var challenge = MakeChallenge();

        OtpChallenge.Outcome last = default;
        for (var i = 0; i < OtpChallenge.MaxAttempts; i++)
        {
            last = challenge.Validate(WrongHash, Now);
        }

        Assert.Equal(OtpChallenge.Outcome.TooManyAttempts, last);
    }

    [Fact]
    public void After_max_attempts_even_the_right_code_is_rejected()
    {
        var challenge = MakeChallenge();
        for (var i = 0; i < OtpChallenge.MaxAttempts; i++)
        {
            challenge.Validate(WrongHash, Now);
        }

        var outcome = challenge.Validate(RightHash, Now);

        Assert.Equal(OtpChallenge.Outcome.TooManyAttempts, outcome);
        Assert.False(challenge.Consumed);
    }
}
