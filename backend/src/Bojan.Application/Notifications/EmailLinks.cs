namespace Bojan.Application.Notifications;

/// <summary>
/// Where the links in an email point.
/// </summary>
/// <remarks>
/// <para>
/// The API does not otherwise know the storefront's address — it answers
/// requests, it does not compose pages. An email is the one thing it produces
/// that has to be readable somewhere else entirely, so the site's own URL has
/// to be configured rather than derived from the request: mail is composed on a
/// background worker where there is no request to derive it from.
/// </para>
/// <para>
/// Every path here mirrors a real route in <c>apps/storefront</c>. Getting one
/// wrong is a dead link in a customer's inbox that nothing in the build would
/// catch, which is why they are written in one place rather than at each call
/// site.
/// </para>
/// </remarks>
public sealed class EmailLinks
{
    public const string SectionName = "Email";

    private string _site = "http://localhost:3000";

    /// <summary>The storefront's public origin, without a trailing slash.</summary>
    public string Site
    {
        get => _site;
        set => _site = string.IsNullOrWhiteSpace(value) ? _site : value.TrimEnd('/');
    }

    public string Support => Path("/account/support");

    public string Wallet => Path("/account/wallet");

    public string ForgotPassword => Path("/auth/forgot-password");

    public string Order(Guid orderId) => Path($"/account/orders/{orderId}");

    public string Invoice(Guid orderId) => Path($"/account/orders/{orderId}/invoice");

    public string Return(Guid returnId) => Path($"/account/returns/{returnId}");

    public string Ticket(Guid ticketId) => Path($"/account/support/{ticketId}");

    public string Quote(Guid quoteId) => Path($"/business/quotes/{quoteId}");

    public string Product(string slug) => Path($"/products/{Uri.EscapeDataString(slug)}");

    public string ResetPassword(string token) => Path($"/auth/reset-password?token={Uri.EscapeDataString(token)}");

    private string Path(string path) => $"{Site}{path}";
}
