using Bojan.Application.Common;
using Bojan.Application.Notifications;

namespace Bojan.Api.Tests;

/// <summary>
/// What the templates actually render.
/// </summary>
/// <remarks>
/// These are the only messages the shop sends to a customer's inbox, where
/// nobody will see a mistake until it has gone out. The cases below are the
/// ones that would go wrong silently: an unescaped product title, a figure in
/// the wrong digits, a row printed empty because the value was optional.
/// </remarks>
public class EmailTemplateTests
{
    private static EmailTemplates Templates()
    {
        var links = new EmailLinks { Site = "https://bojanstore.com" };
        return new EmailTemplates(links);
    }

    private static readonly Guid AnyId = Guid.Parse("a51cea19-8653-4a08-a952-994105dd9490");

    [Fact]
    public void Every_template_produces_both_a_text_and_an_html_body()
    {
        var templates = Templates();

        var all = new (string Subject, EmailBody Body)[]
        {
            templates.PasswordReset("https://bojanstore.com/auth/reset-password?token=abc", TimeSpan.FromHours(1)),
            templates.PasswordChanged(DateTimeOffset.UtcNow),
            templates.Welcome("نیلوفر"),
            templates.OrderPlaced("BZ-1", AnyId, DateTimeOffset.UtcNow, "پرداخت در محل", "پست", [], 0, 0, 1000),
            templates.OrderShipped("BZ-1", AnyId, "پست", "123", "تهران"),
            templates.OrderDelivered("BZ-1", AnyId, "6763556755689511", DateTimeOffset.UtcNow, 1000),
            templates.OrderCancelled("BZ-1", 1000, 900, 100, 0, true),
            templates.ReturnSubmitted("RT-BZ-1", AnyId, "BZ-1", "کالا × ۱", "معیوب"),
            templates.ReturnRefunded("RT-BZ-1", AnyId, 1000, "کیف پول"),
            templates.WalletToppedUp(1000, 2000, DateTimeOffset.UtcNow),
            templates.WalletRejected(1000, DateTimeOffset.UtcNow, null),
            templates.TicketReplied(AnyId, "موضوع"),
            templates.QuoteIssued("QT-1", AnyId, "B2B-1", 1000, DateTimeOffset.UtcNow),
            templates.BackInStock("مداد", "pencil", 1000),
        };

        Assert.Equal(14, all.Length);

        foreach (var (subject, body) in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(subject));

            // The text is not a stripped copy of the markup — it is what a
            // text-only client shows and what survives a filter that drops the
            // HTML part, so it has to say something on its own.
            Assert.False(string.IsNullOrWhiteSpace(body.Text));
            Assert.DoesNotContain("<", body.Text, StringComparison.Ordinal);

            Assert.StartsWith("<!DOCTYPE html>", body.Html, StringComparison.Ordinal);
            Assert.Contains("dir=\"rtl\"", body.Html, StringComparison.Ordinal);
            Assert.Contains("بوژان", body.Html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_product_title_cannot_carry_markup_into_the_body()
    {
        // A title is text an operator typed into the product form, and an email
        // body is rendered by a mail client that will run what it is given.
        var (_, body) = Templates().BackInStock("<script>alert(1)</script>", "x", 1000);

        Assert.DoesNotContain("<script>", body.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", body.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rejection_reason_from_an_operator_is_escaped_too()
    {
        var (_, body) = Templates().WalletRejected(1000, DateTimeOffset.UtcNow, "<img src=x onerror=alert(1)>");

        // The words survive as words — that is the point of escaping rather
        // than stripping. What must not survive is the markup: with the angle
        // brackets encoded, a client renders the reason as the text it is and
        // there is no tag for `onerror` to hang off.
        Assert.DoesNotContain("<img", body.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;img", body.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Amounts_are_grouped_and_in_Persian_digits()
    {
        var (_, body) = Templates().WalletToppedUp(1_200_000, 2_050_000, DateTimeOffset.UtcNow);

        // The shop's own convention: Persian digits with an ASCII separator,
        // which is not what fa-IR produces on its own.
        Assert.Contains("۱,۲۰۰,۰۰۰ تومان", body.Html, StringComparison.Ordinal);
        Assert.Contains("۲,۰۵۰,۰۰۰ تومان", body.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Reference_codes_stay_in_Latin()
    {
        var (subject, body) = Templates().OrderDelivered(
            "BZ-889306-32SX", AnyId, "6763556755689511", DateTimeOffset.UtcNow, 1000);

        // An order number and an invoice number get read down a phone line and
        // typed back in. Transliterating them would make that harder, and the
        // shop's own screens show them in Latin.
        Assert.Contains("BZ-889306-32SX", subject, StringComparison.Ordinal);
        Assert.Contains("6763556755689511", body.Html, StringComparison.Ordinal);
        Assert.Contains("6763556755689511", body.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_row_with_no_value_is_dropped_rather_than_printed_empty()
    {
        var withTracking = Templates().OrderShipped("BZ-1", AnyId, "پست", "24598731", "تهران");
        var without = Templates().OrderShipped("BZ-1", AnyId, "پست", null, "تهران");

        Assert.Contains("کد رهگیری", withTracking.Body.Html, StringComparison.Ordinal);

        // A label beside nothing reads as a field the shop forgot to fill in.
        Assert.DoesNotContain("کد رهگیری", without.Body.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_cancellation_with_no_penalty_shows_neither_the_row_nor_the_explanation()
    {
        var (_, body) = Templates().OrderCancelled("BZ-1", 1_000_000, 1_000_000, 0, 0, penaltyExplained: true);

        Assert.DoesNotContain("جریمه", body.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("هزینه‌ی آماده‌سازی", body.Html, StringComparison.Ordinal);
        Assert.Contains("۱,۰۰۰,۰۰۰", body.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_cancellation_paid_by_card_says_the_gateway_share_is_returned_by_hand()
    {
        var (_, body) = Templates().OrderCancelled("BZ-1", 1_000_000, 0, 0, 1_000_000, penaltyExplained: false);

        // True of the product: there is no adapter behind IPaymentGateway that
        // can refund, so saying otherwise would be a promise nothing keeps.
        Assert.Contains("دستی", body.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void The_reset_window_comes_from_the_service_rather_than_the_copy()
    {
        var hour = Templates().PasswordReset("https://x", TimeSpan.FromHours(1));
        var half = Templates().PasswordReset("https://x", TimeSpan.FromMinutes(30));

        Assert.Contains("۱ ساعت", hour.Body.Html, StringComparison.Ordinal);
        Assert.Contains("۳۰ دقیقه", half.Body.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void The_reset_link_appears_as_text_as_well_as_a_button()
    {
        const string Url = "https://bojanstore.com/auth/reset-password?token=abc123";
        var (_, body) = Templates().PasswordReset(Url, TimeSpan.FromHours(1));

        // Corporate clients strip the button, and without the address beside it
        // the customer is locked out. Three times in the HTML: the button's
        // href, the fallback's href, and the fallback's visible text — the last
        // being the one a reader can copy when even the anchor is stripped.
        Assert.Contains(Url, body.Text, StringComparison.Ordinal);
        Assert.Equal(3, CountOccurrences(body.Html, Url));
    }

    [Fact]
    public void Every_link_points_at_the_configured_site()
    {
        var (_, body) = Templates().OrderDelivered("BZ-1", AnyId, "1", DateTimeOffset.UtcNow, 1000);

        Assert.Contains("https://bojanstore.com/account/orders/", body.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("localhost", body.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void No_template_reaches_for_a_remote_image()
    {
        var templates = Templates();
        var bodies = new[]
        {
            templates.Welcome("نیلوفر").Body.Html,
            templates.OrderPlaced("BZ-1", AnyId, DateTimeOffset.UtcNow, "p", "s", [], 0, 0, 1).Body.Html,
            templates.BackInStock("مداد", "pencil", 1000).Body.Html,
        };

        foreach (var html in bodies)
        {
            // Clients block remote images by default, so an email built on one
            // arrives broken — and a remote image is a read receipt besides.
            Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("background-image", html, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void A_preheader_is_written_rather_than_left_to_the_client()
    {
        var (_, body) = Templates().OrderShipped("BZ-1", AnyId, "پست", "24598731", "تهران");

        // Without one the inbox takes the opening words of the body, which are
        // the same sentence on every message of that kind.
        Assert.Contains("۲۴۵۹۸۷۳۱", body.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rejection_with_no_operator_note_still_gives_a_reason()
    {
        var (_, body) = Templates().WalletRejected(1000, DateTimeOffset.UtcNow, null);

        // The customer has sent money and been refused; "no reason given" is
        // the worst possible answer.
        Assert.Contains("مغایرت", body.Html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "۰")]
    [InlineData(1_200_000, "۱,۲۰۰,۰۰۰")]
    [InlineData(45_000, "۴۵,۰۰۰")]
    public void Amounts_group_the_way_the_storefront_groups_them(long value, string expected)
    {
        Assert.Equal(expected, PersianFormat.Amount(value));
    }

    [Fact]
    public void Dates_are_Jalali_and_in_Tehrans_own_day()
    {
        // Half past two in the morning in Tehran is the previous evening in
        // UTC. Telling a customer their order was placed a day before their
        // receipt says is the kind of wrongness nobody reports.
        var lateNightTehran = new DateTimeOffset(2026, 8, 5, 23, 0, 0, TimeSpan.Zero);

        var formatted = PersianFormat.Date(lateNightTehran);

        // 2026-08-06 in Tehran is 15 Mordad 1405.
        Assert.Contains("مرداد", formatted, StringComparison.Ordinal);
        Assert.Contains("۱۴۰۵", formatted, StringComparison.Ordinal);
        Assert.Contains("۱۵", formatted, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;

        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
