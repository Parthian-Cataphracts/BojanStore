using Bojan.Infrastructure.Support;

namespace Bojan.Api.Tests;

/// <summary>
/// What makes two messages the same conversation.
/// </summary>
/// <remarks>
/// Getting this wrong is not a crash — it is an inbox that scatters one
/// exchange across several rows, or piles four unrelated topics from one
/// customer into a single thread. Neither shows up in a smoke test.
/// </remarks>
public class MailSubjectTests
{
    [Theory]
    [InlineData("Re: سفارش من", "سفارش من")]
    [InlineData("RE: سفارش من", "سفارش من")]
    [InlineData("Fwd: سفارش من", "سفارش من")]
    [InlineData("FW: سفارش من", "سفارش من")]
    [InlineData("AW: سفارش من", "سفارش من")]
    [InlineData("پاسخ: سفارش من", "سفارش من")]
    [InlineData("ارجاع: سفارش من", "سفارش من")]
    public void One_prefix_comes_off(string subject, string expected)
    {
        Assert.Equal(expected, MailSubject.Normalize(subject));
    }

    [Theory]
    // A message that has been round twice carries two, and clients do not agree
    // on whether to add another.
    [InlineData("Re: Fwd: سفارش من")]
    [InlineData("Re: Re: Re: سفارش من")]
    [InlineData("Fwd: پاسخ: سفارش من")]
    [InlineData("RE:  FW:   سفارش من")]
    public void A_run_of_prefixes_comes_off(string subject)
    {
        Assert.Equal("سفارش من", MailSubject.Normalize(subject));
    }

    [Theory]
    [InlineData("سفارش من", "سفارش من")]
    [InlineData("  سفارش من  ", "سفارش من")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void A_subject_with_no_prefix_is_left_alone(string? subject, string expected)
    {
        Assert.Equal(expected, MailSubject.Normalize(subject));
    }

    [Theory]
    // "Return" and "Reply" begin with the letters of a prefix but are words in
    // the subject, and stripping them would merge unrelated threads.
    [InlineData("Return of order 12")]
    [InlineData("Refund please")]
    [InlineData("Review of my order")]
    public void A_word_that_merely_starts_like_a_prefix_survives(string subject)
    {
        Assert.Equal(subject, MailSubject.Normalize(subject));
    }

    [Fact]
    public void A_reply_and_its_original_normalize_to_the_same_thing()
    {
        // The property the whole threading rests on.
        Assert.Equal(
            MailSubject.Normalize("مشکل در تحویل سفارش BZ-1024"),
            MailSubject.Normalize("Re: مشکل در تحویل سفارش BZ-1024"));
    }
}
