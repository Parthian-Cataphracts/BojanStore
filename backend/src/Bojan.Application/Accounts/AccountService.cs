using Bojan.Application.Catalogue;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Common;
using Bojan.Domain.Customers;
using Bojan.Domain.Orders;
using Bojan.Domain.Reviews;

namespace Bojan.Application.Accounts;

/// <summary>Fields of <c>PUT /me</c>. Only these six — the frontend's proxy drops the rest before forwarding.</summary>
public sealed record UpdateProfileRequest(
    string? FirstName,
    string? LastName,
    string? Email,
    string? BirthDate,
    string? City,
    string? NationalId,
    /// <summary>
    /// A URL this API already issued from <c>POST /uploads/avatars</c>. Empty
    /// clears the picture; see the check in <c>UpdateProfileAsync</c> for why
    /// an arbitrary URL is not accepted here.
    /// </summary>
    string? AvatarUrl = null);

/// <summary>Fields of <c>POST /me/addresses</c>. <c>Id</c> present means edit, absent means create.</summary>
public sealed record SaveAddressRequest(
    Guid? Id,
    string Title,
    string Recipient,
    string Phone,
    string Province,
    string City,
    string PostalCode,
    string Line,
    bool IsDefault);

/// <summary>One product on a return request — the <c>items</c> field of <c>POST /me/returns</c>.</summary>
public sealed record ReturnItemRequest(Guid ProductId, int Quantity);

public sealed record CreateReturnRequest(
    string OrderId,
    IReadOnlyList<ReturnItemRequest> Items,
    string Reason,
    string? Description,
    string? RefundMethod);

public sealed record CreateReviewRequest(string ProductSlug, int Rating, string? Title, string Body, bool Recommend);

public sealed record CreateQuestionRequest(string ProductSlug, string Body);

/// <summary>
/// The customer's own writes — Phase 5's private half.
/// </summary>
/// <remarks>
/// Every method takes <c>customerId</c> as its first parameter and it is
/// always derived from the credential, never from the body. That is
/// <c>BACKEND.md</c> section 1.3's closing rule, and it is the reason none of
/// these request records carries a customer id field for a caller to set.
/// </remarks>
public sealed class AccountService(
    IAccountRepository repository,
    ICatalogueQueries catalogue,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IFileStorage storage,
    IPaymentGateway gateway)
{
    /// <summary>The only folder a customer's own picture may come from.</summary>
    private const string AvatarFolder = "avatars";

    /// <summary>Same ceiling a shopper could reach by paying for one very large cart — a top-up has no reason to exceed it.</summary>
    private const long MaxTopUpAmount = 500_000_000;

    public async Task<UseCaseResult<UserDto>> UpdateProfileAsync(
        Guid customerId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await repository.FindAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return UseCaseResult<UserDto>.Failure(UseCaseError.NotFound);
        }

        // Each field is optional: the profile form posts only what it shows,
        // and a partial body must not blank the fields it left out.
        if (request.FirstName is not null) customer.FirstName = request.FirstName.Trim();
        if (request.LastName is not null) customer.LastName = request.LastName.Trim();
        if (request.Email is not null) customer.Email = Blank(request.Email);
        if (request.City is not null) customer.City = Blank(request.City);
        if (request.NationalId is not null) customer.NationalId = Blank(request.NationalId);

        if (request.AvatarUrl is not null)
        {
            var avatar = request.AvatarUrl.Trim();

            if (avatar.Length == 0)
            {
                customer.AvatarUrl = null;
            }
            else if (storage.IsOwnUrl(avatar, AvatarFolder))
            {
                customer.AvatarUrl = avatar;
            }
            else
            {
                // Refused rather than ignored. Silently dropping it would save
                // the rest of the form and leave the customer looking at a
                // picture that was never stored.
                return UseCaseResult<UserDto>.Failure(UseCaseError.Invalid, "avatar");
            }
        }

        if (request.BirthDate is not null)
        {
            if (request.BirthDate.Length == 0)
            {
                customer.BirthDate = null;
            }
            else if (DateOnly.TryParse(request.BirthDate, out var birthDate))
            {
                customer.BirthDate = birthDate;
            }
            else
            {
                // The frontend renders the date as Jalali but posts ISO. A
                // value that is neither is a bug worth surfacing, not a field
                // to silently drop.
                return UseCaseResult<UserDto>.Failure(UseCaseError.Invalid, "birthDate");
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserDto(
            customer.Id.ToString(),
            customer.FirstName,
            customer.LastName,
            customer.Phone,
            customer.Email,
            customer.BirthDate?.ToString("yyyy-MM-dd"),
            customer.City,
            customer.AvatarUrl,
            customer.WalletBalance.Amount,
            customer.LoyaltyPoints);
    }

    public async Task<UseCaseResult<AddressDto>> SaveAddressAsync(
        Guid customerId,
        SaveAddressRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await repository.FindWithAddressesAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return UseCaseResult<AddressDto>.Failure(UseCaseError.Unauthorized);
        }

        Address address;

        if (request.Id is { } id)
        {
            // Looked up through the customer's own collection, so an id
            // belonging to someone else simply is not found.
            var existing = customer.Addresses.FirstOrDefault(candidate => candidate.Id == id);
            if (existing is null)
            {
                return UseCaseResult<AddressDto>.Failure(UseCaseError.NotFound);
            }

            address = existing;
            address.Title = request.Title;
            address.Recipient = request.Recipient;
            address.Phone = request.Phone;
            address.Province = request.Province;
            address.City = request.City;
            address.PostalCode = request.PostalCode;
            address.Line = request.Line;

            if (request.IsDefault)
            {
                foreach (var other in customer.Addresses)
                {
                    other.IsDefault = false;
                }

                address.IsDefault = true;
            }
        }
        else
        {
            address = customer.AddAddress(new Address
            {
                CustomerId = customerId,
                Title = request.Title,
                Recipient = request.Recipient,
                Phone = request.Phone,
                Province = request.Province,
                City = request.City,
                PostalCode = request.PostalCode,
                Line = request.Line,
                // The first address a customer saves is their default whether
                // they ticked the box or not — a customer with addresses and
                // no default breaks the checkout's pre-selection.
                IsDefault = request.IsDefault || customer.Addresses.Count == 0,
            });

            repository.AddAddress(address);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(address);
    }

    public async Task<UseCaseResult> DeleteAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken)
    {
        var address = await repository.FindAddressAsync(customerId, addressId, cancellationToken);
        if (address is null)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        repository.RemoveAddress(address);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    public async Task<int> MarkNotificationsReadAsync(
        Guid customerId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        var notifications = await repository.FindNotificationsAsync(customerId, ids, cancellationToken);
        foreach (var notification in notifications)
        {
            notification.MarkRead();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return notifications.Count;
    }

    public async Task<UseCaseResult> RemoveFromWishlistAsync(
        Guid customerId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var item = await repository.FindWishlistItemAsync(customerId, productId, cancellationToken);
        if (item is null)
        {
            // Removing something that is already gone is the outcome the caller
            // wanted. A 404 here would make a double-tap look like a failure.
            return UseCaseResult.Success();
        }

        repository.RemoveWishlistItem(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    public async Task<int> ClearSearchHistoryAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var removed = await repository.ClearSearchHistoryAsync(customerId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return removed;
    }

    /// <summary>
    /// <c>POST /me/wallet/topup</c> — screen 58's "افزایش اعتبار".
    /// </summary>
    /// <remarks>
    /// Goes through the same <see cref="IPaymentGateway"/> port checkout uses,
    /// so a real PSP integration credits the wallet no differently than it
    /// settles an order: started, then verified, and only a verified amount
    /// ever reaches <see cref="Customer.CreditWallet"/>. The sandbox
    /// implementation approves everything it is asked to verify — see its own
    /// remarks for why that must not survive into production.
    /// </remarks>
    public async Task<UseCaseResult<WalletTransactionDto>> TopUpWalletAsync(
        Guid customerId,
        long amount,
        CancellationToken cancellationToken)
    {
        if (amount < 1 || amount > MaxTopUpAmount)
        {
            return UseCaseResult<WalletTransactionDto>.Failure(UseCaseError.Invalid, "amount");
        }

        var customer = await repository.FindAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return UseCaseResult<WalletTransactionDto>.Failure(UseCaseError.Unauthorized);
        }

        var session = await gateway.StartAsync($"WALLET-{customerId:N}", amount, cancellationToken);
        var verified = await gateway.VerifyAsync(session.Reference, amount, cancellationToken);
        if (!verified)
        {
            return UseCaseResult<WalletTransactionDto>.Failure(UseCaseError.Invalid, "payment-declined");
        }

        customer.CreditWallet(new Money(amount));

        var transaction = new WalletTransaction
        {
            CustomerId = customerId,
            Title = "افزایش اعتبار کیف پول",
            Amount = amount,
            Status = WalletTransactionStatus.Success,
            Icon = "add_circle",
            CreatedAtUtc = clock.UtcNow,
        };
        repository.AddWalletTransaction(transaction);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new WalletTransactionDto(
            transaction.Id.ToString(),
            transaction.Title,
            transaction.Amount,
            transaction.CreatedAtUtc,
            transaction.Status.ToString().ToLowerInvariant(),
            transaction.Icon);
    }

    /// <summary>
    /// <c>POST /me/returns</c>. The order has to be the caller's own and has
    /// to actually contain what they are sending back.
    /// </summary>
    public async Task<UseCaseResult<ReturnRequestDto>> CreateReturnAsync(
        Guid customerId,
        CreateReturnRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return UseCaseResult<ReturnRequestDto>.Failure(UseCaseError.Invalid, "items");
        }

        var order = await repository.FindOrderAsync(customerId, request.OrderId, cancellationToken);
        if (order is null)
        {
            return UseCaseResult<ReturnRequestDto>.Failure(UseCaseError.NotFound);
        }

        if (order.Status is not (OrderStatus.Delivered or OrderStatus.Shipped))
        {
            // Nothing has arrived yet, so there is nothing to return —
            // cancelling is the action for an order still in flight.
            return UseCaseResult<ReturnRequestDto>.Failure(UseCaseError.Invalid, "order-status");
        }

        var items = new List<ReturnItem>(request.Items.Count);
        var requestId = Guid.NewGuid();

        foreach (var requested in request.Items)
        {
            var line = order.Lines.FirstOrDefault(candidate => candidate.ProductId == requested.ProductId);
            if (line is null)
            {
                return UseCaseResult<ReturnRequestDto>.Failure(UseCaseError.Invalid, "unknown-item");
            }

            if (requested.Quantity < 1 || requested.Quantity > line.Quantity)
            {
                return UseCaseResult<ReturnRequestDto>.Failure(UseCaseError.Invalid, "quantity");
            }

            items.Add(new ReturnItem
            {
                ReturnRequestId = requestId,
                ProductId = line.ProductId,
                ProductSlug = line.ProductSlug,
                ProductTitle = line.ProductTitle,
                ProductImageUrl = line.ProductImageUrl,
                Quantity = requested.Quantity,
            });
        }

        var returnRequest = ReturnRequest.Create(
            OrderNumber.NewReturnCode(),
            customerId,
            order.Id,
            order.Number,
            request.Reason,
            request.Description,
            request.RefundMethod ?? "wallet",
            items,
            clock.UtcNow);

        repository.AddReturnRequest(returnRequest);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var first = returnRequest.Items.First();

        return new ReturnRequestDto(
            returnRequest.Id.ToString(),
            returnRequest.Code,
            order.Id.ToString(),
            order.Number,
            first.ProductSlug,
            first.ProductTitle,
            first.ProductImageUrl,
            first.Quantity,
            returnRequest.Reason,
            returnRequest.Description,
            WireFormat.ReturnStatus(returnRequest.Status),
            returnRequest.CreatedAtUtc,
            Timelines.ForReturn(returnRequest.Status));
    }

    /// <summary>
    /// <c>POST /reviews</c>. Lands in
    /// <see cref="ModerationStatus.Pending"/> — <c>BACKEND.md</c> Phase 5:
    /// "Reviews and questions need a moderation state."
    /// </summary>
    public async Task<UseCaseResult> CreateReviewAsync(
        Guid customerId,
        CreateReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Rating is < 1 or > 5)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "rating");
        }

        var product = await catalogue.GetProductAsync(request.ProductSlug, cancellationToken);
        if (product is null || !Guid.TryParse(product.Id, out var productId))
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        if (await repository.HasReviewedAsync(customerId, productId, cancellationToken))
        {
            return UseCaseResult.Failure(UseCaseError.Conflict, "already-reviewed");
        }

        var customer = await repository.FindAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return UseCaseResult.Failure(UseCaseError.Unauthorized);
        }

        repository.AddReview(new ProductReview
        {
            ProductId = productId,
            CustomerId = customerId,
            AuthorName = DisplayName(customer),
            Rating = request.Rating,
            Title = request.Title,
            Body = request.Body,
            Recommend = request.Recommend,
            IsVerifiedPurchase = await repository.HasPurchasedAsync(customerId, productId, cancellationToken),
            CreatedAtUtc = clock.UtcNow,
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    public async Task<UseCaseResult> CreateQuestionAsync(
        Guid customerId,
        CreateQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var product = await catalogue.GetProductAsync(request.ProductSlug, cancellationToken);
        if (product is null || !Guid.TryParse(product.Id, out var productId))
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        var customer = await repository.FindAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return UseCaseResult.Failure(UseCaseError.Unauthorized);
        }

        repository.AddQuestion(new ProductQuestion
        {
            ProductId = productId,
            CustomerId = customerId,
            AuthorName = DisplayName(customer),
            Body = request.Body,
            AskedAtUtc = clock.UtcNow,
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    /// <summary>
    /// A shopper who has not filled in their name still needs an attribution.
    /// The masked phone is what the design shows for an anonymous reviewer.
    /// </summary>
    private static string DisplayName(Customer customer)
    {
        var name = $"{customer.FirstName} {customer.LastName}".Trim();
        return name.Length > 0 ? name : $"{customer.Phone[..4]}***{customer.Phone[^2..]}";
    }

    private static string? Blank(string value) => value.Trim() is { Length: > 0 } trimmed ? trimmed : null;

    private static AddressDto ToDto(Address address) => new(
        address.Id.ToString(),
        address.Title,
        address.Recipient,
        address.Phone,
        address.Province,
        address.City,
        address.PostalCode,
        address.Line,
        address.IsDefault);
}
