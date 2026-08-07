using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Admin;

namespace Bojan.Api.Tests;

/// <summary>
/// A write the database refuses used to reach the client as a 500.
/// </summary>
/// <remarks>
/// Ten unique indexes guard this schema and every writer checked first and then
/// inserted; nothing caught the violation when two requests passed that check
/// at once, or when one request carried the same value twice.
/// <c>DbUpdateException</c> appeared nowhere in the backend.
/// </remarks>
public sealed class PersistenceConflictTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private Guid _owner;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            _owner = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@conflict.test")).Id;
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    /// <summary>Puts two coupons on one code and returns the answer.</summary>
    /// <remarks>
    /// The vehicle used to be the permission grid posting the same cell twice.
    /// That is no longer a conflict — a grid repeating itself is one grant, and
    /// the save says so — so a genuine collision is needed here instead: two
    /// coupons cannot share a code, and renaming one onto another is a caller
    /// asking for something the database will not do.
    /// </remarks>
    private async Task<HttpResponseMessage> RenameOntoTakenCodeAsync()
    {
        Guid movingId = default;

        await _factory.WithDbAsync(async db =>
        {
            db.Coupons.Add(new Domain.Orders.Coupon { Code = "TAKEN20", PercentOff = 20 });
            var moving = new Domain.Orders.Coupon { Code = "MOVING20", PercentOff = 20 };
            db.Coupons.Add(moving);
            await db.SaveChangesAsync();
            movingId = moving.Id;
        });

        using var owner = _factory.CreateAdminClient(_owner);
        return await owner.PostAsJsonAsync(
            "/api/admin/coupons", new { id = movingId.ToString(), code = "TAKEN20" });
    }

    [Fact]
    public async Task A_unique_violation_is_a_conflict_rather_than_a_server_error()
    {
        var response = await RenameOntoTakenCodeAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task The_response_names_no_constraint_or_column()
    {
        var response = await RenameOntoTakenCodeAsync();
        var body = await response.Content.ReadAsStringAsync();

        // Constraint names and column names stay in the log.
        Assert.DoesNotContain("IX_", body, StringComparison.Ordinal);
        Assert.DoesNotContain("coupons", body, StringComparison.Ordinal);
        Assert.Contains("conflict", body, StringComparison.Ordinal);
    }
}
