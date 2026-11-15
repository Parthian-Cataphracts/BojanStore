using Bojan.Infrastructure.Support;

namespace Bojan.Api.Tests;

/// <summary>
/// Which timestamp a message is shown under.
/// </summary>
/// <remarks>
/// Mail arrives without a <c>Date:</c> header more often than it should —
/// eNamad's domain-verification mail is the one that surfaced this. Both
/// MailKit's envelope date and <c>MimeMessage.Date</c> report that absence as
/// <c>default</c> rather than null, and taking it at face value dated the
/// message to year 1 and sorted the newest mail in the shop to the bottom of
/// the inbox.
/// </remarks>
public class MailboxTimestampTests
{
    private static readonly DateTimeOffset Header = new(2026, 11, 15, 17, 48, 0, TimeSpan.FromHours(3.5));
    private static readonly DateTimeOffset Delivered = new(2026, 11, 15, 17, 48, 13, TimeSpan.FromHours(3.5));

    [Fact]
    public void The_header_date_is_used_when_the_message_has_one()
    {
        Assert.Equal(Header, MailboxService.Timestamp(Header, Delivered));
    }

    [Fact]
    public void A_missing_header_falls_back_to_the_delivery_time()
    {
        Assert.Equal(Delivered, MailboxService.Timestamp(null, Delivered));
    }

    [Fact]
    public void A_default_header_counts_as_missing_rather_than_as_year_one()
    {
        // The case that broke the inbox: not null, but DateTimeOffset.MinValue.
        Assert.Equal(Delivered, MailboxService.Timestamp(default(DateTimeOffset), Delivered));
    }

    [Fact]
    public void A_dated_message_sorts_above_an_undated_one_that_arrived_earlier()
    {
        var older = MailboxService.Timestamp(null, Delivered.AddDays(-30));
        var newest = MailboxService.Timestamp(default(DateTimeOffset), Delivered);

        Assert.True(newest > older);
    }
}
