using Bojan.Infrastructure.Support;

namespace Bojan.Api.Tests;

/// <summary>
/// What survives sanitizing, and what must not.
/// </summary>
/// <remarks>
/// The body of an inbound mail is written by whoever sent it — anyone who knows
/// the published support address — and the person about to open it holds the
/// highest-privileged session in the shop. These are the payloads that matter,
/// tested against the real sanitizer rather than a description of it.
/// </remarks>
public class MailHtmlSanitizerTests
{
    private static string Clean(string html) => MailHtmlSanitizer.Sanitize(html).Html;

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<SCRIPT SRC=//evil.example/x.js></SCRIPT>")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>")]
    [InlineData("<object data=\"evil.swf\"></object>")]
    [InlineData("<embed src=\"evil.swf\">")]
    [InlineData("<form action=\"https://evil.example\"><input name=\"a\"></form>")]
    [InlineData("<svg><script>alert(1)</script></svg>")]
    [InlineData("<math><mtext><script>alert(1)</script></mtext></math>")]
    [InlineData("<base href=\"https://evil.example/\">")]
    [InlineData("<meta http-equiv=\"refresh\" content=\"0;url=https://evil.example\">")]
    public void Executable_markup_does_not_survive(string html)
    {
        var cleaned = Clean(html);

        Assert.DoesNotContain("script", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("iframe", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil.example", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http-equiv", cleaned, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<p onclick=\"alert(1)\">سلام</p>")]
    [InlineData("<p onmouseover=\"alert(1)\">سلام</p>")]
    [InlineData("<div onerror=\"alert(1)\">سلام</div>")]
    [InlineData("<a href=\"#\" onfocus=\"alert(1)\" autofocus>x</a>")]
    public void Event_handlers_are_stripped_but_the_text_stays(string html)
    {
        var cleaned = Clean(html);

        Assert.DoesNotContain("alert", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onmouseover", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", cleaned, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<a href=\"javascript:alert(1)\">کلیک</a>")]
    [InlineData("<a href=\"JaVaScRiPt:alert(1)\">کلیک</a>")]
    [InlineData("<a href=\"data:text/html;base64,PHNjcmlwdD4=\">کلیک</a>")]
    [InlineData("<a href=\"vbscript:msgbox(1)\">کلیک</a>")]
    [InlineData("<a href=\"file:///etc/passwd\">کلیک</a>")]
    public void Only_ordinary_link_schemes_survive(string html)
    {
        var cleaned = Clean(html);

        Assert.DoesNotContain("javascript", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vbscript", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:text/html", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("file://", cleaned, StringComparison.OrdinalIgnoreCase);

        // The text is kept — the customer wrote it, and dropping their words
        // because of the link they attached would be losing the message.
        Assert.Contains("کلیک", cleaned, StringComparison.Ordinal);
    }

    [Fact]
    public void An_ordinary_link_survives_and_cannot_reach_its_opener()
    {
        var cleaned = Clean("<a href=\"https://example.com/order\">سفارش</a>");

        Assert.Contains("https://example.com/order", cleaned, StringComparison.Ordinal);
        Assert.Contains("target=\"_blank\"", cleaned, StringComparison.Ordinal);
        Assert.Contains("noopener", cleaned, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<img src=\"https://tracker.example/pixel.gif\">")]
    [InlineData("<div style=\"background-image:url('https://tracker.example/p.png')\">x</div>")]
    [InlineData("<td background=\"https://tracker.example/p.png\">x</td>")]
    public void Remote_content_is_removed_and_reported(string html)
    {
        var (cleaned, hadRemote) = MailHtmlSanitizer.Sanitize(html);

        // A remote image in an email is a read receipt and an IP leak to
        // whoever sent it.
        Assert.DoesNotContain("tracker.example", cleaned, StringComparison.OrdinalIgnoreCase);

        // Reported so the screen can say images were blocked rather than
        // silently showing a body with holes in it.
        Assert.True(hadRemote);
    }

    [Fact]
    public void The_formatting_an_email_actually_uses_survives()
    {
        var cleaned = Clean(
            "<p style=\"color:#333;text-align:right\"><strong>سلام</strong><br>"
            + "<em>سفارش من</em></p><table><tr><td>۱</td></tr></table><ul><li>مورد</li></ul>");

        Assert.Contains("<strong>", cleaned, StringComparison.Ordinal);
        Assert.Contains("<em>", cleaned, StringComparison.Ordinal);
        Assert.Contains("<table>", cleaned, StringComparison.Ordinal);
        Assert.Contains("<li>", cleaned, StringComparison.Ordinal);
        Assert.Contains("color", cleaned, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<div style=\"position:fixed;top:0;left:0;width:100vw\">پوشش</div>", "position")]
    [InlineData("<div style=\"behavior:url(evil.htc)\">x</div>", "behavior")]
    [InlineData("<div style=\"-moz-binding:url(evil.xml)\">x</div>", "binding")]
    public void Css_that_could_escape_the_message_is_dropped(string html, string forbidden)
    {
        // An inbound body must not be able to position itself over the panel's
        // own chrome, or reach the network through a stylesheet.
        Assert.DoesNotContain(forbidden, Clean(html), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_body_is_empty_rather_than_an_error(string? html)
    {
        var (cleaned, hadRemote) = MailHtmlSanitizer.Sanitize(html);

        Assert.Equal(string.Empty, cleaned);
        Assert.False(hadRemote);
    }

    [Fact]
    public void Plain_text_passes_through_untouched()
    {
        Assert.Contains("سلام، سفارش من کجاست؟", Clean("سلام، سفارش من کجاست؟"), StringComparison.Ordinal);
    }

    /// <summary>
    /// An unknown wrapper loses the tag, never the message.
    /// </summary>
    /// <remarks>
    /// eNamad wraps its whole mail in &lt;center&gt;, which is not on the
    /// allow-list. Dropping the subtree with the tag left the operator looking
    /// at "(بدون متن)" over a body that had arrived intact — and that mail
    /// carries no text/plain alternative to fall back to.
    /// </remarks>
    [Theory]
    [InlineData("<center><p>کد شما ۹۷۱۴۲۵ است</p></center>")]
    [InlineData("<font size=\"3\"><p>کد شما ۹۷۱۴۲۵ است</p></font>")]
    [InlineData("<main><article><p>کد شما ۹۷۱۴۲۵ است</p></article></main>")]
    [InlineData("<marquee><p>کد شما ۹۷۱۴۲۵ است</p></marquee>")]
    public void An_unknown_wrapper_is_unwrapped_and_its_text_survives(string html)
    {
        var cleaned = Clean(html);

        Assert.Contains("۹۷۱۴۲۵", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("<center", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<font", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<marquee", cleaned, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Unwrapping must not turn code into visible text.
    /// </summary>
    /// <remarks>
    /// Keeping the children of a removed tag is right for a wrapper and wrong
    /// for a script: there the content is the payload, and leaving it behind
    /// would print the attack on the operator's screen.
    /// </remarks>
    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<style>body{background:url(//evil.example/x)}</style>")]
    [InlineData("<svg><script>alert(1)</script></svg>")]
    [InlineData("<template><p>alert(1)</p></template>")]
    [InlineData("<noscript>alert(1)</noscript>")]
    public void What_a_dangerous_tag_contained_does_not_come_back_as_text(string html)
    {
        var cleaned = Clean(html);

        Assert.DoesNotContain("alert", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil.example", cleaned, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The body that actually arrived, end to end.</summary>
    [Fact]
    public void The_enamad_verification_mail_renders()
    {
        var cleaned = Clean(
            "<center> <div align=\"center\" bgcolor=\"#ffffff\" style=\"padding: 24px; font-family:tahoma;\">"
            + "<h1 style=\"color: rgb(30, 30, 162);\">مرکز توسعه تجارت الکترونیک</h1>"
            + "<p>لطفا کد زیر را در قسمت دسترسی فنی سایت اینماد وارد نمایید</p>"
            + "<h1 style=\"color: rgb(227, 84, 14);\">971425</h1></div></center>");

        Assert.Contains("971425", cleaned, StringComparison.Ordinal);
        Assert.Contains("مرکز توسعه تجارت الکترونیک", cleaned, StringComparison.Ordinal);
        Assert.Contains("<div", cleaned, StringComparison.OrdinalIgnoreCase);
    }
}
