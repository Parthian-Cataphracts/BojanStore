using AngleSharp.Html.Dom;
using Ganss.Xss;

namespace Bojan.Infrastructure.Support;

/// <summary>
/// Turns the HTML body of an arbitrary inbound email into something safe to put
/// on an operator's screen.
/// </summary>
/// <remarks>
/// <para>
/// This is the most dangerous surface the inbox adds. The body is text written
/// by whoever sent the mail — anyone with the support address, which is
/// published — and the person about to open it holds the highest-privileged
/// session in the shop. A script that runs here runs with the panel's cookies.
/// </para>
/// <para>
/// So there are two independent layers and neither is trusted to be enough on
/// its own:
/// </para>
/// <list type="number">
/// <item>This allow-list, which keeps only known-good tags and attributes and
/// so drops <c>script</c>, every <c>on*</c> handler, <c>javascript:</c> URLs,
/// <c>iframe</c>, <c>object</c> and <c>form</c>.</item>
/// <item>The panel renders the result inside a sandboxed frame with neither
/// <c>allow-scripts</c> nor <c>allow-same-origin</c>, so a bypass of this lands
/// in an origin that cannot execute anything or reach the panel's DOM.</item>
/// </list>
/// <para>
/// Remote content is dropped rather than allowed through: a remote image in an
/// email is a read receipt and an IP leak to the sender. The caller is told it
/// happened so the screen can say images were blocked, the way a mail client
/// does.
/// </para>
/// </remarks>
public static class MailHtmlSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = Build();

    private static HtmlSanitizer Build()
    {
        var sanitizer = new HtmlSanitizer();

        // Cleared and rebuilt rather than trimmed. The library's defaults are
        // already conservative, but they are written for a rich-text editor's
        // output — an email body has no business carrying most of them, and
        // starting from empty means a future version of the library cannot
        // widen this by changing its own defaults.
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[]
        {
            "a", "b", "blockquote", "br", "caption", "code", "col", "colgroup", "dd", "div", "dl", "dt",
            "em", "h1", "h2", "h3", "h4", "h5", "h6", "hr", "i", "li", "ol", "p", "pre", "q", "s",
            "small", "span", "strike", "strong", "sub", "sup", "table", "tbody", "td", "tfoot", "th",
            "thead", "tr", "u", "ul",
        })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        foreach (var attribute in new[] { "href", "title", "colspan", "rowspan", "align", "dir", "style" })
        {
            sanitizer.AllowedAttributes.Add(attribute);
        }

        // The only schemes that may appear in an href. Everything else —
        // javascript:, data:, vbscript:, file: — goes with the attribute.
        sanitizer.AllowedSchemes.Clear();
        foreach (var scheme in new[] { "http", "https", "mailto", "tel" })
        {
            sanitizer.AllowedSchemes.Add(scheme);
        }

        // Inline CSS survives because email leans on it for all its layout, but
        // only these properties — so a body cannot position itself over the
        // panel's own chrome, and cannot reach the network through CSS.
        sanitizer.AllowedCssProperties.Clear();
        foreach (var property in new[]
        {
            "color", "background-color", "font-family", "font-size", "font-style", "font-weight",
            "text-align", "text-decoration", "line-height", "margin", "margin-top", "margin-bottom",
            "margin-left", "margin-right", "padding", "padding-top", "padding-bottom", "padding-left",
            "padding-right", "border", "border-top", "border-bottom", "border-left", "border-right",
            "border-color", "border-radius", "border-style", "border-width", "width", "max-width",
            "height", "vertical-align", "direction", "white-space",
        })
        {
            sanitizer.AllowedCssProperties.Add(property);
        }

        sanitizer.AllowedAtRules.Clear();

        // There is deliberately no img handling: `img` is absent from the tags
        // and `src`, `srcset` and `background` from the attributes, so an
        // inbound image cannot survive at all. CSS cannot smuggle one back
        // either — `background-image` and the `background` shorthand are not in
        // the property list above, which is the only place a `url(…)` could
        // have been honoured.

        // Links leave the panel, so they must not hand the opener over with
        // them.
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is IHtmlAnchorElement anchor)
            {
                anchor.SetAttribute("target", "_blank");
                anchor.SetAttribute("rel", "noopener noreferrer nofollow");
            }
        };

        return sanitizer;
    }

    /// <summary>Sanitizes, and reports whether anything remote was removed on the way.</summary>
    public static (string Html, bool HadRemoteContent) Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return (string.Empty, false);
        }

        // Checked before sanitizing, because sanitizing is what removes the
        // evidence. A heuristic, and used only to decide whether to show an
        // informational line — never as a security decision.
        var hadRemote = ContainsRemoteReference(html);

        try
        {
            return (Sanitizer.Sanitize(html), hadRemote);
        }
        catch
        {
            // A body that cannot be parsed is a body that must not be rendered.
            // Empty makes the screen fall back to the plain-text alternative,
            // which is always safe.
            return (string.Empty, hadRemote);
        }
    }

    private static bool ContainsRemoteReference(string html) =>
        html.Contains("<img", StringComparison.OrdinalIgnoreCase)
        || html.Contains("background=", StringComparison.OrdinalIgnoreCase)
        || html.Contains("url(", StringComparison.OrdinalIgnoreCase);
}
