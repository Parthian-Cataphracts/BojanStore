using Bojan.Domain.Catalogue;
using Bojan.Domain.Common;

namespace Bojan.Domain.Tests;

public class ProductTests
{
    private static Product MakeProduct(int stock) => new()
    {
        Slug = "test-product",
        Title = "Test Product",
        BrandId = Guid.NewGuid(),
        CategoryId = Guid.NewGuid(),
        Price = new Money(100_000),
        ImageUrl = "https://example.com/p.jpg",
        Stock = stock,
    };

    [Fact]
    public void ReduceStock_lowers_the_count()
    {
        var product = MakeProduct(stock: 10);

        product.ReduceStock(3);

        Assert.Equal(7, product.Stock);
    }

    [Fact]
    public void ReduceStock_throws_rather_than_going_negative()
    {
        var product = MakeProduct(stock: 2);

        Assert.Throws<InvalidOperationException>(() => product.ReduceStock(3));
        // The failed attempt must not have partially applied.
        Assert.Equal(2, product.Stock);
    }

    [Fact]
    public void ReduceStock_rejects_a_negative_quantity()
    {
        var product = MakeProduct(stock: 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => product.ReduceStock(-1));
    }

    [Fact]
    public void IncreaseStock_raises_the_count()
    {
        var product = MakeProduct(stock: 5);

        product.IncreaseStock(20);

        Assert.Equal(25, product.Stock);
    }

    [Fact]
    public void SoftDelete_hides_without_destroying()
    {
        var product = MakeProduct(stock: 1);
        var now = DateTimeOffset.UtcNow;

        product.SoftDelete(now);

        Assert.True(product.IsDeleted);
        Assert.Equal(now, product.DeletedAtUtc);

        product.Restore();
        Assert.False(product.IsDeleted);
    }
}
