using Bojan.Domain.Common;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bojan.Api.Tests;

/// <summary>
/// The two halves of the search fold, held against each other.
/// </summary>
/// <remarks>
/// <para>
/// A search folds the needle in C# and the column in SQL, so the two must
/// produce the same string for the same input. Nothing about the code makes
/// that true — they are a static method and a Postgres function written
/// separately — and if they drift, nothing fails loudly: the search simply
/// stops matching some words, which looks like a shop that does not stock them.
/// </para>
/// <para>
/// So the agreement is asserted rather than assumed, over the whole set of
/// things Persian does differently, and over the two alphabets of digits a
/// shopper's keyboard might be in.
/// </para>
/// </remarks>
public sealed class PersianFoldTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();

    public Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    /// <summary>Every way this application expects one word to arrive.</summary>
    public static TheoryData<string> Inputs() =>
    [
        "آبرنگ",
        "ابرنگ",
        "آب‌رنگ", // half-space
        "آب رنگ", // full space
        "كيف پول", // Arabic kaf and yeh
        "کیف پول", // Persian kaf and yeh
        "مدرسة", // teh marbuta
        "خانۀ", // heh with yeh above
        "مُحَمَّد", // harakat
        "کـــیف", // tatweel
        "مسئله",
        "۱۲۳۴۵۶۷۸۹۰", // Persian digits
        "١٢٣٤٥٦٧٨٩٠", // Arabic-Indic digits
        "BZ-P-01", // a SKU: Latin, and case-folded like everything else
        "bz-p-01",
        "أحمد",
        "إسماعيل",
        "",
        "   ",
    ];

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task The_database_folds_a_string_exactly_as_the_application_does(string input)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BojanDbContext>();

        // Round-tripped through the database rather than computed here, so what
        // is compared is the function the queries actually call.
        var fromSql = await db.Database
            .SqlQuery<string>($"SELECT bojan_fold({input}) AS \"Value\"")
            .SingleAsync();

        Assert.Equal(PersianText.Fold(input), fromSql);
    }

    /// <summary>
    /// The point of the exercise, stated as the shopper would: these are the
    /// same word and have to fold to the same string.
    /// </summary>
    [Theory]
    [InlineData("آبرنگ", "ابرنگ")]
    [InlineData("آبرنگ", "آب‌رنگ")]
    [InlineData("آبرنگ", "آب رنگ")]
    [InlineData("كيف", "کیف")]
    [InlineData("مدرسة", "مدرسه")]
    [InlineData("مُحَمَّد", "محمد")]
    [InlineData("۱۲۳", "123")]
    [InlineData("١٢٣", "123")]
    public void Variants_of_one_word_fold_together(string typed, string stored) =>
        Assert.Equal(PersianText.Fold(stored), PersianText.Fold(typed));

    /// <summary>Different words stay different — a fold that matches everything matches nothing useful.</summary>
    [Theory]
    [InlineData("آبرنگ", "روغنی")]
    [InlineData("کیف", "کفش")]
    [InlineData("۱۲۳", "۳۲۱")]
    public void Different_words_do_not_fold_together(string one, string other) =>
        Assert.NotEqual(PersianText.Fold(one), PersianText.Fold(other));
}
