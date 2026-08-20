namespace Bojan.Domain.Admin;

/// <summary>
/// One screen of the panel, as a permission an owner can grant on its own.
/// </summary>
/// <remarks>
/// <para>
/// Sections were the only unit there was, and ten of them is a coarse
/// instrument: «مرجوعی‌ها» and «سفارش‌ها» are one section, so somebody who
/// should only ever handle returns had to be given the order queue as well.
/// What an owner asks for is narrower than a section and wider than an
/// endpoint — it is a screen, which is the thing they can point at.
/// </para>
/// <para>
/// The key is the panel's own route, because the route is what already
/// identifies a screen on both sides and cannot drift the way a made-up
/// constant can. <c>apps/admin/src/lib/nav.ts</c> holds the same keys with the
/// Persian labels beside them; this side holds the mapping that decides what
/// each one opens on the API.
/// </para>
/// <para>
/// A grant row stores either a section key or one of these. Holding the section
/// is holding everything under it — that is what the rows written before this
/// existed mean, and it stays the way an owner grants a whole area in one tick.
/// </para>
/// <para>
/// The API's own gate stays at section granularity, and deliberately: a screen
/// key resolves to its section, so granting «مرجوعی‌ها» opens the order
/// endpoints. What the finer key buys is the panel — the screen is not in the
/// menu, not reachable by typing its address, and not something the operator is
/// shown at all. Narrowing the API to match would mean naming a screen on each
/// of a hundred and fifty routes, and a mislabelled one there fails closed on a
/// screen somebody needs rather than open on one they do not.
/// </para>
/// </remarks>
public static class PanelScreen
{
    /// <summary>Every grantable screen, and the API section it reaches.</summary>
    public static readonly IReadOnlyDictionary<string, string> Sections =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // فروش
            ["/orders"] = PanelSection.Orders,
            ["/invoices"] = PanelSection.Orders,
            ["/returns"] = PanelSection.Orders,
            ["/wallet/topups"] = PanelSection.Customers,

            // مشتریان
            ["/customers"] = PanelSection.Customers,
            ["/customers/groups"] = PanelSection.Customers,
            ["/business-requests"] = PanelSection.Business,

            // کاتالوگ
            ["/products"] = PanelSection.Products,
            ["/categories"] = PanelSection.Products,
            ["/brands"] = PanelSection.Products,
            ["/collections"] = PanelSection.Products,
            ["/inventory"] = PanelSection.Inventory,
            ["/inventory/low-stock"] = PanelSection.Inventory,

            // بازاریابی و محتوا
            ["/content"] = PanelSection.Content,
            ["/content/articles"] = PanelSection.Content,
            ["/campaigns"] = PanelSection.Campaigns,
            ["/coupons"] = PanelSection.Campaigns,
            ["/campaigns/notifications"] = PanelSection.Campaigns,

            // پشتیبانی
            ["/support"] = PanelSection.Support,
            ["/support/live-chat"] = PanelSection.Support,
            ["/support/mailbox"] = PanelSection.Support,
            ["/support/replies"] = PanelSection.Support,

            // گزارش‌ها
            ["/reports/sales"] = PanelSection.Reports,
            ["/reports/orders"] = PanelSection.Reports,
            ["/reports/products"] = PanelSection.Reports,
            ["/reports/inventory"] = PanelSection.Reports,
            ["/reports/customers"] = PanelSection.Reports,
            ["/reports/campaigns"] = PanelSection.Reports,
            ["/reports/finance"] = PanelSection.Reports,
            ["/reports/export"] = PanelSection.Reports,

            // تنظیمات فروشگاه
            ["/settings"] = PanelSection.Settings,
            ["/settings/orders"] = PanelSection.Settings,
            ["/settings/payment"] = PanelSection.Settings,
            ["/settings/shipping"] = PanelSection.Settings,
            ["/settings/invoice"] = PanelSection.Settings,
            ["/settings/loyalty"] = PanelSection.Settings,
            ["/settings/sms"] = PanelSection.Settings,
            ["/settings/push"] = PanelSection.Settings,
            ["/settings/verification"] = PanelSection.Settings,

            // سیستم و دسترسی
            ["/settings/users"] = PanelSection.Settings,
            ["/settings/roles"] = PanelSection.Settings,
            ["/settings/api"] = PanelSection.Settings,
            ["/settings/backup"] = PanelSection.Settings,
            ["/settings/audit"] = PanelSection.Settings,
            ["/settings/logs"] = PanelSection.Settings,
            ["/settings/system"] = PanelSection.Settings,
        };

    public static bool IsKnown(string? screen) => screen is not null && Sections.ContainsKey(screen);

    /// <summary>
    /// What a stored grant reaches on the API: itself when it is a section, the
    /// section behind it when it is a screen, and nothing when it is neither.
    /// </summary>
    public static string? SectionOf(string? grant)
    {
        if (grant is null) return null;
        if (PanelSection.IsKnown(grant)) return grant;
        return Sections.TryGetValue(grant, out var section) ? section : null;
    }
}
