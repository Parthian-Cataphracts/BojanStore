using Bojan.Domain.Admin;
using Bojan.Domain.Common;
using Bojan.Domain.Customers;
using Bojan.Domain.Orders;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// The database refuses a reference to something that is not there.
/// </summary>
/// <remarks>
/// Fifty-eight tables carried thirteen foreign keys between them, all of them
/// on collections EF wires up on its own. Everything else — an order naming its
/// customer, a review naming its product, an audit row naming who acted — was a
/// bare uuid column, so a bug that wrote the wrong id produced a row that
/// looked correct and joined to nothing. These say the constraints are actually
/// on, rather than only configured.
/// </remarks>
public sealed class SchemaIntegrityTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();

    public Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    private async Task RefusesAsync(Func<BojanDbContext, Task> write)
    {
        await Assert.ThrowsAsync<DbUpdateException>(() => _factory.WithDbAsync(write));
    }

    [Fact]
    public async Task An_order_cannot_name_a_customer_who_does_not_exist()
    {
        await RefusesAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "p-01", 100_000, stock: 3);
            var customer = await TestData.AddCustomerAsync(db, "09121110050");
            var address = await TestData.AddAddressAsync(db, customer.Id);

            db.Orders.Add(Order.Create(
                OrderNumber.NewOrderNumber(),
                Guid.NewGuid(),
                [new OrderLineDraft(product.Id, product.Slug, product.Title, product.ImageUrl, 1, product.Price)],
                address.Id,
                "تهران",
                "ارسال استاندارد",
                "پرداخت در محل",
                "cod",
                product.Price,
                Money.Zero,
                Money.Zero,
                Guid.NewGuid().ToString()));

            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task A_product_cannot_name_a_brand_that_does_not_exist()
    {
        await RefusesAsync(async db =>
        {
            var (_, categoryId) = await TestData.AddCatalogueAsync(db);
            await TestData.AddProductAsync(db, Guid.NewGuid(), categoryId, "p-02", 100_000, stock: 1);
        });
    }

    [Fact]
    public async Task An_audit_row_cannot_name_an_operator_who_does_not_exist()
    {
        await RefusesAsync(async db =>
        {
            db.AuditEntries.Add(new AuditEntry
            {
                ActorId = Guid.NewGuid(),
                ActorName = "کسی",
                Action = "test",
                Target = "test",
            });

            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task A_wallet_row_cannot_name_a_customer_who_does_not_exist()
    {
        await RefusesAsync(async db =>
        {
            db.WalletTransactions.Add(new WalletTransaction
            {
                CustomerId = Guid.NewGuid(),
                Title = "شارژ",
                Amount = 10_000,
            });

            await db.SaveChangesAsync();
        });
    }

    /// <summary>
    /// The one column that deliberately has none, so the reason stays written
    /// down rather than being read as an oversight and "fixed".
    /// </summary>
    [Fact]
    public async Task A_stock_movement_may_name_a_customer_because_they_can_cancel_their_own_order()
    {
        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "p-03", 100_000, stock: 1);
            var customer = await TestData.AddCustomerAsync(db, "09121110051");

            db.StockMovements.Add(new Domain.Inventory.StockMovement
            {
                ProductId = product.Id,
                Kind = Domain.Inventory.StockMovementKind.In,
                Quantity = 1,
                Reason = "لغو سفارش",
                ActorId = customer.Id,
            });

            await db.SaveChangesAsync();
        });
    }
}
