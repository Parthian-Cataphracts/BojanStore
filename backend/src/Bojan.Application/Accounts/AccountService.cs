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
    IPaymentGateway gateway,
    WalletOptions wallet)
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
    /// <c>POST /me/wallet/topup</c> — screen 58's "افزایش اعتبار", the gateway
    /// route.
    /// </summary>
    /// <remarks>
    /// Files the request and hands back somewhere to pay. It credits nothing:
    /// the balance moves in <see cref="ConfirmGatewayTopUpAsync"/>, after the
    /// gateway has been asked whether the money actually arrived. Splitting it
    /// in two is the point — a top-up's whole effect is the credit, with no
    /// goods or debt behind it, so the credit has to wait for an answer from
    /// something that is not the customer's browser.
    /// </remarks>
    public async Task<UseCaseResult<WalletTopUpStartedDto>> StartGatewayTopUpAsync(
        Guid customerId,
        long amount,
        CancellationToken cancellationToken)
    {
        if (!IsAcceptableAmount(amount))
        {
            return UseCaseResult<WalletTopUpStartedDto>.Failure(UseCaseError.Invalid, "amount");
        }

        // The sandbox approves every verification without asking a bank, so on
        // this path — where approval *is* money — it must not be reachable.
        if (gateway.IsSandbox)
        {
            return UseCaseResult<WalletTopUpStartedDto>.Failure(UseCaseError.Invalid, "gateway-unavailable");
        }

        var customer = await repository.FindAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return UseCaseResult<WalletTopUpStartedDto>.Failure(UseCaseError.Unauthorized);
        }

        var session = await gateway.StartAsync($"WALLET-{customerId:N}", amount, cancellationToken);

        var topUp = new WalletTopUp
        {
            CustomerId = customerId,
            Amount = new Money(amount),
            Method = WalletTopUpMethod.Gateway,
            GatewayReference = session.Reference,
            CreatedAtUtc = clock.UtcNow,
        };

        FileTopUp(topUp, "افزایش اعتبار کیف پول");
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new WalletTopUpStartedDto(topUp.Id.ToString(), session.Reference, session.PaymentUrl);
    }

    /// <summary>
    /// <c>POST /me/wallet/topup/confirm</c> — where the gateway sends the
    /// shopper back to.
    /// </summary>
    /// <remarks>
    /// Safe to call repeatedly, which matters because a shopper who refreshes
    /// the callback page calls it again. The verification is asked for the
    /// amount recorded when the request was filed, not one the caller supplies,
    /// and <see cref="WalletTopUp.Approve"/> only acts on a pending request —
    /// so a second call verifies the same reference and then credits nothing.
    /// </remarks>
    public async Task<UseCaseResult<WalletTopUpDto>> ConfirmGatewayTopUpAsync(
        Guid customerId,
        string reference,
        CancellationToken cancellationToken)
    {
        var topUp = await repository.FindTopUpByReferenceAsync(customerId, reference, cancellationToken);
        if (topUp is null)
        {
            return UseCaseResult<WalletTopUpDto>.Failure(UseCaseError.NotFound);
        }

        if (topUp.Status is not WalletTopUpStatus.Pending)
        {
            // Already settled — report what it settled as rather than failing,
            // so a refreshed callback page shows the outcome instead of an error.
            return Describe(topUp);
        }

        var verified = await gateway.VerifyAsync(reference, topUp.Amount.Amount, cancellationToken);
        if (!verified)
        {
            await DecideAsync(topUp, approved: false, adminId: null, note: null, cancellationToken);
            return UseCaseResult<WalletTopUpDto>.Failure(UseCaseError.Invalid, "payment-declined");
        }

        await DecideAsync(topUp, approved: true, adminId: null, note: null, cancellationToken);
        return Describe(topUp);
    }

    /// <summary>
    /// <c>POST /me/wallet/topup/manual</c> — a card-to-card transfer filed for
    /// review.
    /// </summary>
    /// <remarks>
    /// Nothing here credits anything, and nothing here can be made to: the row
    /// is written pending and an operator decides it against the bank statement.
    /// Refused outright unless <see cref="WalletOptions.ManualTopUpEnabled"/>
    /// says the store is staffing that review — an unattended queue would
    /// either sit forever or be waved through, and the second is how a wallet
    /// gets filled with money nobody sent.
    /// </remarks>
    public async Task<UseCaseResult<WalletTopUpDto>> SubmitManualTopUpAsync(
        Guid customerId,
        ManualTopUpRequest request,
        CancellationToken cancellationToken)
    {
        if (!wallet.ManualTopUpEnabled)
        {
            return UseCaseResult<WalletTopUpDto>.Failure(UseCaseError.Invalid, "manual-topup-disabled");
        }

        if (!IsAcceptableAmount(request.Amount))
        {
            return UseCaseResult<WalletTopUpDto>.Failure(UseCaseError.Invalid, "amount");
        }

        var tracking = request.TrackingNumber?.Trim();
        if (string.IsNullOrWhiteSpace(tracking))
        {
            return UseCaseResult<WalletTopUpDto>.Failure(UseCaseError.Invalid, "tracking-number");
        }

        // A transfer cannot have been made later than today, and a date the
        // operator cannot match against a statement is not evidence.
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        if (request.PaidOn is null || request.PaidOn > today)
        {
            return UseCaseResult<WalletTopUpDto>.Failure(UseCaseError.Invalid, "paid-on");
        }

        var receipt = request.ReceiptUrl?.Trim();
        if (wallet.RequireReceipt && string.IsNullOrWhiteSpace(receipt))
        {
            return UseCaseResult<WalletTopUpDto>.Failure(UseCaseError.Invalid, "receipt");
        }

        var customer = await repository.FindAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return UseCaseResult<WalletTopUpDto>.Failure(UseCaseError.Unauthorized);
        }

        var topUp = new WalletTopUp
        {
            CustomerId = customerId,
            Amount = new Money(request.Amount),
            Method = WalletTopUpMethod.Manual,
            ReceiptUrl = string.IsNullOrWhiteSpace(receipt) ? null : receipt,
            TrackingNumber = tracking,
            PaidOn = request.PaidOn,
            CustomerNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAtUtc = clock.UtcNow,
        };

        FileTopUp(topUp, "افزایش اعتبار (کارت به کارت)");
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Describe(topUp);
    }

    private bool IsAcceptableAmount(long amount) =>
        amount >= wallet.MinimumAmount &&
        amount <= Math.Min(wallet.MaximumAmount, MaxTopUpAmount);

    /// <summary>
    /// Writes the request and the pending ledger row it owns, linked both ways.
    /// </summary>
    /// <remarks>
    /// The ledger row exists from the start so the customer's wallet screen
    /// shows the top-up waiting rather than nothing at all — a transfer that
    /// vanishes until someone approves it is how support tickets are made. It
    /// is Pending, so it is visibly not spendable.
    /// </remarks>
    private void FileTopUp(WalletTopUp topUp, string title)
    {
        var ledger = new WalletTransaction
        {
            CustomerId = topUp.CustomerId,
            Title = title,
            Amount = topUp.Amount.Amount,
            Status = WalletTransactionStatus.Pending,
            Icon = "add_circle",
            CreatedAtUtc = topUp.CreatedAtUtc,
        };

        topUp.WalletTransactionId = ledger.Id;
        repository.AddWalletTransaction(ledger);
        repository.AddWalletTopUp(topUp);
    }

    /// <summary>
    /// Settles a pending request: credits the wallet on approval, and moves the
    /// ledger row off Pending either way.
    /// </summary>
    /// <remarks>
    /// The one place a top-up may touch a balance. The customer row is locked
    /// before it is read, and <see cref="WalletTopUp.Approve"/> refuses a
    /// request that is not pending — between them, two callers arriving at once
    /// credit the money once.
    /// </remarks>
    internal async Task DecideAsync(
        WalletTopUp topUp,
        bool approved,
        Guid? adminId,
        string? note,
        CancellationToken cancellationToken)
    {
        var decided = approved
            ? topUp.Approve(adminId, clock.UtcNow, note)
            : topUp.Reject(adminId, clock.UtcNow, note);

        if (!decided)
        {
            return;
        }

        if (approved)
        {
            var customer = await repository.FindForUpdateAsync(topUp.CustomerId, cancellationToken);
            customer?.CreditWallet(topUp.Amount);
        }

        if (topUp.WalletTransactionId is { } ledgerId)
        {
            var ledger = await repository.FindWalletTransactionAsync(ledgerId, cancellationToken);
            if (ledger is not null)
            {
                ledger.Status = approved ? WalletTransactionStatus.Success : WalletTransactionStatus.Failed;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static WalletTopUpDto Describe(WalletTopUp topUp) =>
        new(
            topUp.Id.ToString(),
            topUp.Amount.Amount,
            topUp.Method.ToString().ToLowerInvariant(),
            topUp.Status.ToString().ToLowerInvariant(),
            topUp.TrackingNumber,
            topUp.PaidOn,
            topUp.ReviewNote,
            topUp.CreatedAtUtc);

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
