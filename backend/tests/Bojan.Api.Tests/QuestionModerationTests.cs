using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Reviews;

namespace Bojan.Api.Tests;

/// <summary>
/// The product question queue — «پرسش‌ها».
/// </summary>
/// <remarks>
/// A question arrived <see cref="ModerationStatus.Pending"/> and the storefront
/// served published ones only, and nothing anywhere could move one between the
/// two: <c>ProductQuestion.Answer</c> had no callers, and there was no admin
/// query, endpoint or screen. So every question a shopper ever asked was
/// written to the database and seen by nobody, and the product page showed none
/// of them however many had been asked.
///
/// Answering is what publishes, which is the domain's own rule. Most of what is
/// worth pinning down here is that the two cannot come apart — a published
/// question with no answer under it is the one state the product page must
/// never be able to show.
/// </remarks>
public sealed class QuestionModerationTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _admin = null!;
    private HttpClient _public = null!;

    private Guid _productId;
    private string _productSlug = null!;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;
        await _factory.WithDbAsync(async db =>
        {
            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "question-owner@bojan.test")).Id;

            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "question-target", 100_000, 5);
            _productId = product.Id;
            _productSlug = product.Slug;
        });

        _admin = _factory.CreateAdminClient(ownerId);
        _public = _factory.CreateClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_question_nobody_has_answered_is_invisible_but_queued()
    {
        await AddQuestionAsync();

        // Not on the product page…
        Assert.Empty(await ProductQuestionsAsync());
        // …but in front of the operator, which is the half that did not exist.
        Assert.Single(await QueueAsync("pending"));
    }

    [Fact]
    public async Task Answering_publishes_the_question_and_prints_the_reply()
    {
        var id = await AddQuestionAsync();

        var response = await _admin.PostAsJsonAsync(
            "/api/admin/questions/answer",
            new { id = id.ToString(), body = "بله، این قلم‌مو برای آبرنگ هم مناسب است." });

        response.EnsureSuccessStatusCode();

        var published = Assert.Single(await ProductQuestionsAsync());
        // The storefront nests the reply with who wrote it and when.
        Assert.Equal(
            "بله، این قلم‌مو برای آبرنگ هم مناسب است.",
            published.GetProperty("answer").GetProperty("body").GetString());
    }

    [Fact]
    public async Task The_reply_is_signed_with_the_operator_who_wrote_it()
    {
        // Taken from the session rather than the request, so the shop cannot be
        // made to answer in somebody else's name.
        var id = await AddQuestionAsync();

        await _admin.PostAsJsonAsync(
            "/api/admin/questions/answer",
            new { id = id.ToString(), body = "بله." });

        var queued = Assert.Single(await QueueAsync("published"));
        Assert.False(string.IsNullOrWhiteSpace(queued.GetProperty("answerAuthor").GetString()));
    }

    [Fact]
    public async Task An_empty_reply_is_refused_rather_than_published_blank()
    {
        var id = await AddQuestionAsync();

        var response = await _admin.PostAsJsonAsync(
            "/api/admin/questions/answer",
            new { id = id.ToString(), body = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await ProductQuestionsAsync());
    }

    [Fact]
    public async Task A_question_cannot_be_published_without_an_answer()
    {
        // The one state the product page must never show: a customer's question
        // with nothing under it. Answering is the only way to publish.
        var id = await AddQuestionAsync();

        var response = await _admin.PostAsJsonAsync(
            "/api/admin/questions/status",
            new { id = id.ToString(), status = "published" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await ProductQuestionsAsync());
    }

    [Fact]
    public async Task Rejecting_keeps_it_out_of_the_shop_and_in_the_queue()
    {
        var id = await AddQuestionAsync();

        var response = await _admin.PostAsJsonAsync(
            "/api/admin/questions/status",
            new { id = id.ToString(), status = "rejected" });

        response.EnsureSuccessStatusCode();

        Assert.Empty(await ProductQuestionsAsync());
        Assert.Single(await QueueAsync("rejected"));
    }

    [Fact]
    public async Task Deleting_takes_it_out_of_the_queue_as_well()
    {
        var id = await AddQuestionAsync();

        var response = await _admin.PostAsJsonAsync(
            "/api/admin/questions/delete",
            new { id = id.ToString() });

        response.EnsureSuccessStatusCode();

        Assert.Empty(await QueueAsync("pending"));
        Assert.Empty(await QueueAsync("rejected"));
    }

    [Fact]
    public async Task The_queue_counts_every_state_even_the_empty_ones()
    {
        // A tab whose count vanishes rather than reading «۰» looks like a tab
        // that failed to load.
        await AddQuestionAsync();

        var counts = await _admin.GetFromJsonAsync<JsonElement>("/api/admin/questions/counts");

        Assert.Equal(1, counts.GetProperty("pending").GetInt32());
        Assert.Equal(0, counts.GetProperty("published").GetInt32());
        Assert.Equal(0, counts.GetProperty("rejected").GetInt32());
    }

    private async Task<Guid> AddQuestionAsync(string phone = "09120000200")
    {
        var id = Guid.Empty;

        await _factory.WithDbAsync(async db =>
        {
            var customer = await TestData.AddCustomerAsync(db, phone);

            var question = new ProductQuestion
            {
                ProductId = _productId,
                CustomerId = customer.Id,
                AuthorName = "کاربر آزمون",
                Body = "آیا این محصول برای آبرنگ مناسب است؟",
            };

            db.ProductQuestions.Add(question);
            await db.SaveChangesAsync();
            id = question.Id;
        });

        return id;
    }

    private async Task<JsonElement[]> ProductQuestionsAsync() =>
        await _public.GetFromJsonAsync<JsonElement[]>($"/api/products/{_productSlug}/questions") ?? [];

    private async Task<JsonElement[]> QueueAsync(string status)
    {
        var page = await _admin.GetFromJsonAsync<JsonElement>($"/api/admin/questions?status={status}");
        return [.. page.GetProperty("items").EnumerateArray()];
    }
}
