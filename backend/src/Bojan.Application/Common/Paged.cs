namespace Bojan.Application.Common;

/// <summary>
/// Exact shape of the frontend's <c>Paged&lt;T&gt;</c> — see
/// <c>apps/storefront/src/lib/api/types.ts</c> and <c>BACKEND.md</c> section
/// 0 and Phase 2. Every list endpoint the frontend pages through must return
/// all four fields; leaving one off is a silent shape mismatch, not an error
/// the frontend will surface clearly.
/// </summary>
public sealed record Paged<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
