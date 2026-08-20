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

    [Fact]
    public void A_live_challenge_blocks_a_fresh_request()
    {
        var challenge = MakeChallenge(expiresAt: Now.AddMinutes(2));

        Assert.True(challenge.BlocksResend(Now));
    }

    [Fact]
    public void An_expired_challenge_does_not_block_a_fresh_request()
    {
        var challenge = MakeChallenge(expiresAt: Now.AddSeconds(-1));

        Assert.False(challenge.BlocksResend(Now));
    }

    [Fact]
    public void A_consumed_challenge_does_not_block_a_fresh_request_even_though_it_has_not_expired()
    {
        var challenge = MakeChallenge(expiresAt: Now.AddMinutes(2));
        challenge.Validate(RightHash, Now);

        Assert.False(challenge.BlocksResend(Now));
    }

    [Fact]
    public void A_challenge_that_ran_out_its_attempts_still_blocks_a_fresh_request()
    {
        var challenge = MakeChallenge(expiresAt: Now.AddMinutes(2));
        for (var i = 0; i < OtpChallenge.MaxAttempts; i++)
        {
            challenge.Validate(WrongHash, Now);
        }

        // Exhausting every guess must not be a faster way to a new code than
        // guessing right the first time would have been.
        Assert.True(challenge.BlocksResend(Now));
    }

    [Fact]
    public void A_challenge_defaults_to_the_sign_in_purpose()
    {
        var challenge = MakeChallenge();

        Assert.Equal(OtpPurpose.SignIn, challenge.Purpose);
        Assert.Null(challenge.CustomerId);
    }

    [Fact]
    public void A_phone_verification_challenge_carries_the_customer_it_belongs_to()
    {
        var customerId = Guid.NewGuid();

        var challenge = new OtpChallenge
        {
            Phone = "09121234567",
            Purpose = OtpPurpose.PhoneVerification,
            CustomerId = customerId,
            CodeHash = RightHash,
            ExpiresAtUtc = Now.AddMinutes(2),
        };

        Assert.Equal(OtpPurpose.PhoneVerification, challenge.Purpose);
        Assert.Equal(customerId, challenge.CustomerId);

        var outcome = challenge.Validate(RightHash, Now);
        Assert.Equal(OtpChallenge.Outcome.Accepted, outcome);
    }
}
