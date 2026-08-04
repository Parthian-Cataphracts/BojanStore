namespace Bojan.Domain.Admin;

/// <summary>
/// The areas of the panel screen 146's permission grid has a column for.
/// </summary>
/// <remarks>
/// <para>
/// Stable keys, not the Persian labels the grid draws. The grid used to post
/// its own labels and they were stored verbatim, which made a permission depend
/// on a display string: rewording "درخواست‌های سازمانی" in a component would
/// silently revoke it for everyone who had it. The panel maps these to labels
/// for rendering and sends the key.
/// </para>
/// <para>
/// A section is coarser than an endpoint on purpose. It is the unit an owner
/// actually thinks in when deciding what a role may touch, and the unit the
/// grid already presents.
/// </para>
/// </remarks>
public static class PanelSection
{
    public const string Orders = "orders";
    public const string Products = "products";
    public const string Inventory = "inventory";
    public const string Customers = "customers";
    public const string Business = "business";
    public const string Content = "content";
    public const string Campaigns = "campaigns";
    public const string Support = "support";
    public const string Reports = "reports";
    public const string Settings = "settings";

    public static readonly IReadOnlyList<string> All =
    [
        Orders, Products, Inventory, Customers, Business,
        Content, Campaigns, Support, Reports, Settings,
    ];

    public static bool IsKnown(string? section) =>
        section is not null && All.Contains(section, StringComparer.Ordinal);
}
