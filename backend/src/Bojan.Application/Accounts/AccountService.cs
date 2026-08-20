using Bojan.Application.Auth;
using Bojan.Application.Catalogue;
using Bojan.Application.Common;
using Bojan.Application.Notifications;
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

/// <summary>
/// One product on a return request — the <c>items</c> field of
/// <c>POST /me/returns</c>.
/// </summary>
/// <param name="SkuId">
/// The combination being sent back, for an order line that sold one. Null means
/// "the product itself", which is the whole story for a product with no
/// variants — and the only thing this could say before the field existed.
/// </param>
public sealed record ReturnItemRequest(Guid ProductId, int Quantity, Guid? SkuId = null);

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
    ICustomerMailer mailer,
    EmailTemplates templates,
    EmailVerificationService emailVerification,
    WalletOptions wallet)
{
    /// <summary>The only folder a customer's own picture may come from.</summary>
    private const string AvatarFolder = "avatars";

    /// <summary>Same ceiling a shopper could reach by paying for one very large cart — a top-up has no reason to exceed it.</summary>
    private const long MaxTopUpAmount = 500_000_000;

    /// <summary>The only folder a card-to-card receipt may come from.</summary>
    private const string ReceiptFolder = "receipts";

    /// <summary>
    /// The order reference a wallet top-up is paid under.
    /// </summary>
    /// <remarks>
    /// The top-up's own id, so every attempt is a distinct order to the gateway
    /// — see <see cref="StartGatewayTopUpAsync"/>. Prefixed because it shares a
    /// namespace with real order numbers on the settlement path, and one glance
    /// at a gateway's dashboard should say which is which.
    /// </remarks>
    private static string WalletReference(Guid topUpId) => $"WALLET-{topUpId:N}";

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

        var emailChanged = false;
        if (request.Email is not null)
        {
            var newEmail = Blank(request.Email);
            if (!string.Equals(customer.Email, newEmail, StringComparison.Ordinal))
            {
                customer.Email = newEmail;
                customer.IsEmailVerified = false;
                emailChanged = true;
            }
        }

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

        // Fired after the save, and never lets a mail failure turn a
        // successful profile update into an error — see
        // EmailVerificationService and ICustomerMailer's own contract.
        if (emailChanged && customer.Email is not null)
        {
            await emailVerification.RequestAsync(customerId, cancellationToken);
        }

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
            customer.LoyaltyPoints,
            customer.IsEmailVerified,
            customer.IsPhoneVerified);
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
            else
            {
                // Unticking the box used to do nothing at all — the branch
                // above only ever set the flag, so the form offered a choice it
                // could not carry out and the address stayed default. It is
                // honoured now, but by handing the title to another address
                // rather than by leaving the customer with none.
                address.IsDefault = false;

                // The address being demoted is not a candidate to inherit from
                // itself; if it is the only one, it keeps the flag, because a
                // customer with an address and no default is the state the
                // checkout cannot pre-select from.
                PromoteADefault(customer.Addresses.Where(other => other.Id != address.Id));
                if (!customer.Addresses.Any(other => other.IsDefault)) address.IsDefault = true;
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
        var customer = await repository.FindWithAddressesAsync(customerId, cancellationToken);
        var address = customer?.Addresses.FirstOrDefault(candidate => candidate.Id == addressId);

        if (customer is null || address is null)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        repository.RemoveAddress(address);

        // Deleting the default left the customer with addresses and no default,
        // and nothing ever put one back — so the checkout pre-selected nothing
        // and the account screen showed a list with no marked entry, for good.
        PromoteADefault(customer.Addresses.Where(candidate => candidate.Id != addressId));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    /// <summary>
    /// Gives one of <paramref name="candidates"/> the default flag, if none of
    /// them already holds it.
    /// </summary>
    /// <remarks>
    /// The invariant the rest of the system already assumes — the checkout
    /// pre-selects the default, and the account screen marks it — but which
    /// only creation upheld. The oldest candidate takes the title, because "the
    /// one you have had longest" is the least surprising answer when the
    /// customer did not choose.
    /// </remarks>
    private static void PromoteADefault(IEnumerable<Address> candidates)
    {
        var eligible = candidates.OrderBy(a => a.CreatedAtUtc).ToList();

        if (eligible.Count == 0 || eligible.Any(a => a.IsDefault))
        {
            return;
        }

        eligible[0].IsDefault = true;
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
    /// <c>GET /me/wallet</c> — what screen 58 needs to draw itself.
    /// </summary>
    /// <remarks>
    /// The store's limits travel with the balance so the form can offer exactly
    /// what the API would accept, rather than duplicating the rules in the
    /// browser and drifting from them.
    /// </remarks>
    public async Task<UseCaseResult<WalletOverviewDto>> GetWalletAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var customer = await repository.FindAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return UseCaseResult<WalletOverviewDto>.Failure(UseCaseError.Unauthorized);
        }

        var pending = await repository.ListPendingTopUpsAsync(customerId, cancellationToken);

        return new WalletOverviewDto(
            customer.WalletBalance.Amount,
            wallet.ManualTopUpEnabled,
            wallet.RequireReceipt,
            wallet.MinimumAmount,
            Math.Min(wallet.MaximumAmount, MaxTopUpAmount),
            [.. pending.Select(Describe)]);
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

        // A stub gateway — or none at all — approves every verification without
        // asking a bank, so on this path, where approval *is* money, it must
        // not be reachable.
        if (!await gateway.TakesRealMoneyAsync(cancellationToken))
        {
            return UseCaseResult<WalletTopUpStartedDto>.Failure(UseCaseError.Invalid, "gateway-unavailable");
        }

        var customer = await repository.FindAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return UseCaseResult<WalletTopUpStartedDto>.Failure(UseCaseError.Unauthorized);
        }

        // The id is settled before the gateway is called, because the order
        // reference has to name *this* top-up and nothing else. It used to be
        // `WALLET-{customerId}` — the same string every time that customer
        // topped up, which ZarinPal tolerates and IDPay refuses outright, since
        // it verifies on the pair of its own id and the order id and will not
        // accept one it has seen before. A repeated reference is also how a
        // second top-up could be settled against the first one's payment.
        var topUpId = Guid.NewGuid();

        var session = await gateway.StartAsync(WalletReference(topUpId), amount, cancellationToken);

        var topUp = new WalletTopUp
        {
            Id = topUpId,
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
    /// <para>
    /// Safe to call repeatedly, which matters because a shopper who refreshes
    /// the callback page calls it again, and it has to stay safe when two of
    /// those arrive at once. The verification is asked for the amount recorded
    /// when the request was filed, not one the caller supplies, and
    /// <see cref="WalletTopUp.Approve"/> only acts on a pending request.
    /// </para>
    /// <para>
    /// That status check is only idempotent if it is made under the top-up's own
    /// row lock: read the status first and two concurrent callbacks both see
    /// Pending, both approve, and the wallet is credited twice for one payment.
    /// So the decision runs inside a transaction against the row returned by
    /// <see cref="IAccountRepository.FindTopUpForUpdateAsync"/>, and the loser of
    /// the race re-reads Approved and credits nothing.
    /// </para>
    /// <para>
    /// The gateway call stays outside that transaction on purpose. It is a
    /// network round trip, it is a read rather than a state change, and holding
    /// a database row lock across someone else's HTTP timeout is how a lock
    /// queue becomes an outage.
    /// </para>
    /// </remarks>
    public async Task<UseCaseResult<WalletTopUpDto>> ConfirmGatewayTopUpAsync(
        Guid customerId,
        string reference,
        CancellationToken cancellationToken)
    {
        // Untracked peek. Establishes that the reference is this customer's and
        // is worth asking the gateway about; it is never the instance decided.
        var peek = await repository.FindTopUpByReferenceAsync(customerId, reference, cancellationToken);
        if (peek is null)
        {
            return UseCaseResult<WalletTopUpDto>.Failure(UseCaseError.NotFound);
        }

        if (peek.Status is not WalletTopUpStatus.Pending)
        {
            // Already settled — report what it settled as rather than failing,
            // so a refreshed callback page shows the outcome instead of an error.
            return Describe(peek);
        }

        var verified = await gateway.VerifyAsync(
            reference,
            WalletReference(peek.Id),
            peek.Amount.Amount,
            cancellationToken);

        return await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var topUp = await repository.FindTopUpForUpdateAsync(peek.Id, token);
                if (topUp is null)
                {
                    return UseCaseResult<WalletTopUpDto>.Failure(UseCaseError.NotFound);
                }

                if (topUp.Status is not WalletTopUpStatus.Pending)
                {
                    // The racing callback settled it while the gateway was being
                    // asked. Report its outcome; do not decide it a second time.
                    return Describe(topUp);
                }

                await DecideAsync(topUp, verified, adminId: null, note: null, token);

                return verified
                    ? Describe(topUp)
                    : UseCaseResult<WalletTopUpDto>.Failure(UseCaseError.Invalid, "payment-declined");
            },
            cancellationToken);
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

        // And it has to be a file this API actually stored. Every other image a
        // customer supplies is checked this way; this one was taken as given,
        // and it is the worst field to take as given — an operator opens it, on
        // the screen where they decide whether to put money in a wallet, so any
        // URL here is a link the shop asks its own staff to follow.
        if (!string.IsNullOrWhiteSpace(receipt) && !storage.IsOwnUrl(receipt, ReceiptFolder))
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
    /// The one place a top-up may touch a balance, and it assumes its caller has
    /// already done two things: opened a transaction, and loaded
    /// <paramref name="topUp"/> through
    /// <see cref="IAccountRepository.FindTopUpForUpdateAsync"/> so its row is
    /// locked. <see cref="WalletTopUp.Approve"/> refusing a non-pending request
    /// is what makes a repeated decision harmless, and that refusal is only
    /// trustworthy when the status it reads was read under that lock. Both
    /// callers — the gateway callback and the operator's review queue — do this.
    /// The customer lock taken here is the separate guarantee that the balance
    /// arithmetic does not lose an update.
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
    /// <remarks>
    /// Repeated products are summed before anything is checked, for the reason
    /// the checkout consolidates its basket lines: the quantity rule is about
    /// the product, so checking each entry on its own lets the same product
    /// appear twice and pass twice. A line of five units would accept
    /// <c>[{p,5},{p,5}]</c> as two valid entries and file a return for ten of
    /// them — a claim for more than was ever bought, put in front of an operator
    /// as if the order backed it.
    /// </remarks>
    public async Task<UseCaseResult<ReturnRequestDto>> CreateReturnAsync(
        Guid customerId,
        CreateReturnRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return UseCaseResult<ReturnRequestDto>.Failure(UseCaseError.Invalid, "items");
        }

        // Grouped by product *and* combination. Keying on the product alone
        // merged a red one and a blue one into a single entry, so a request for
        // one of each became a request for two of whichever happened to be
        // first — and the other variant was never mentioned again.
        request = request with
        {
            Items = [.. request.Items
                .GroupBy(item => (item.ProductId, item.SkuId))
                .Select(group => group.First() with { Quantity = group.Sum(item => item.Quantity) })],
        };

        // Read the order, count what is already claimed and write the new
        // request as one transaction, with the order row locked.
        //
        // These were three separate steps. Two requests filed at the same moment
        // both read "five remaining", both passed the check, and both were
        // written — a customer could ask the shop to take back ten of something
        // it sold five of, and an operator would see two requests that each
        // looked correct on its own. The lock is on the order because that is
        // what both are counting against.
        var built = await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var order = await repository.FindOrderForUpdateAsync(customerId, request.OrderId, token);
                if (order is null)
                {
                    return UseCaseResult<ReturnRequest>.Failure(UseCaseError.NotFound);
                }

                if (order.Status is not (OrderStatus.Delivered or OrderStatus.Shipped))
                {
                    // Nothing has arrived yet, so there is nothing to return —
                    // cancelling is the action for an order still in flight.
                    return UseCaseResult<ReturnRequest>.Failure(UseCaseError.Invalid, "order-status");
                }

                // What earlier requests against this order already claimed.
                // Checking only against the order line let a customer file the
                // same full-quantity return twice and ask the shop to take back
                // more than it sold.
                var claimed = await repository.GetClaimedReturnQuantitiesAsync(order.Id, token);

                var items = new List<ReturnItem>(request.Items.Count);
                var requestId = Guid.NewGuid();

                foreach (var requested in request.Items)
                {
                    // Matched on the combination as well as the product: an
                    // order can hold two lines of the same product in different
                    // variants, and FirstOrDefault on the product alone picked
                    // whichever came first.
                    var line = order.Lines.FirstOrDefault(candidate =>
                        candidate.ProductId == requested.ProductId && candidate.SkuId == requested.SkuId);

                    if (line is null)
                    {
                        return UseCaseResult<ReturnRequest>.Failure(UseCaseError.Invalid, "unknown-item");
                    }

                    var already = claimed.TryGetValue((line.ProductId, line.SkuId), out var taken) ? taken : 0;
                    var remaining = line.Quantity - already;

                    if (requested.Quantity < 1 || requested.Quantity > remaining)
                    {
                        return UseCaseResult<ReturnRequest>.Failure(UseCaseError.Invalid, "quantity");
                    }

                    items.Add(new ReturnItem
                    {
                        ReturnRequestId = requestId,
                        ProductId = line.ProductId,
                        SkuId = line.SkuId,
                        ProductSlug = line.ProductSlug,
                        ProductTitle = line.ProductTitle,
                        ProductImageUrl = line.ProductImageUrl,
                        Quantity = requested.Quantity,
                    });
                }

                var created = ReturnRequest.Create(
                    OrderNumber.NewReturnCode(),
                    customerId,
                    order.Id,
                    order.Number,
                    request.Reason,
                    request.Description,
                    request.RefundMethod ?? "wallet",
                    items,
                    clock.UtcNow);

                repository.AddReturnRequest(created);
                await unitOfWork.SaveChangesAsync(token);

                return created;
            },
            cancellationToken);

        if (!built.IsSuccess)
        {
            return UseCaseResult<ReturnRequestDto>.Failure(built.Error!.Value, built.Detail);
        }

        var returnRequest = built.Value!;
        var first = returnRequest.Items.First();

        // A return is the longest wait the shop asks a customer to sit through,
        // so the receipt says what the remaining steps are rather than only
        // that the request arrived.
        var requester = await repository.FindAsync(customerId, cancellationToken);
        await mailer.SendAsync(
            requester?.Email,
            templates.ReturnSubmitted(
                returnRequest.Code,
                returnRequest.Id,
                returnRequest.OrderNumber,
                $"{first.ProductTitle} × {PersianFormat.Number(first.Quantity)}",
                returnRequest.Reason),
            cancellationToken);

        return new ReturnRequestDto(
            returnRequest.Id.ToString(),
            returnRequest.Code,
            returnRequest.OrderId.ToString(),
            returnRequest.OrderNumber,
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
