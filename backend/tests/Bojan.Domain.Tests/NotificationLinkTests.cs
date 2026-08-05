using Bojan.Domain.Customers;

namespace Bojan.Domain.Tests;

/// <summary>
/// What may be put behind a notification.
/// </summary>
/// <remarks>
/// The link is rendered as an <c>href</c> in a customer's notification list. A
/// broadcast reaches every customer at once, so a value that leaves the site
/// here is a stored redirect delivered to the whole customer base by anyone who
/// can compose one. The rule was a comment on the property and nothing checked
/// it.
/// </remarks>
public class NotificationLinkTests
{
    private static CustomerNotification New() => new()
    {
        CustomerId = Guid.NewGuid(),
        Kind = NotificationKind.Account,
        Title = "عنوان",
        Body = "متن",
    };

    [Theory]
    [InlineData("/account/orders/123")]
    [InlineData("/products/pen?ref=notification")]
    [InlineData("/magazine/some-article#section")]
    public void An_in_app_path_is_accepted(string href)
    {
        Assert.True(CustomerNotification.IsInternalPath(href));
        Assert.Equal(href, New().WithLink(href).Href);
    }

    [Theory]
    // The two everyone thinks of.
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    // Absolute, and the scheme-less form of the same thing.
    [InlineData("https://evil.example/phish")]
    [InlineData("//evil.example/phish")]
    // Browsers normalise the backslash to a slash, so this is "//evil.example"
    // to anything that follows it — while passing a leading-slash check.
    [InlineData("/\\evil.example")]
    [InlineData("/account\\..\\..\\evil")]
    // A newline hides everything after it from a human reading the value back.
    [InlineData("/account\nhttps://evil.example")]
    // Not a path at all.
    [InlineData("account/orders")]
    [InlineData("/")]
    public void Anything_that_leaves_the_site_is_refused(string href)
    {
        Assert.False(CustomerNotification.IsInternalPath(href));
        Assert.Throws<ArgumentException>(() => New().WithLink(href));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_link_is_not_an_error(string? href)
    {
        Assert.Null(New().WithLink(href).Href);
    }

    [Fact]
    public void A_link_is_trimmed_before_it_is_checked()
    {
        Assert.Equal("/account/orders", New().WithLink("  /account/orders  ").Href);
    }

    [Fact]
    public void A_link_can_be_cleared_after_it_was_set()
    {
        Assert.Null(New().WithLink("/account").WithLink(null).Href);
    }
}
