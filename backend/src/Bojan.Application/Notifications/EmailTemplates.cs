using System.Text;
using Bojan.Application.Common;
using static Bojan.Application.Notifications.EmailShell;

namespace Bojan.Application.Notifications;

/// <summary>
/// The fourteen transactional emails, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// Each builder returns a subject, a plain-text body and an HTML body. The text
/// is written to stand alone rather than being a stripped copy of the markup:
/// it is what a text-only client shows and what survives a filter that drops
/// the HTML part.
/// </para>
/// <para>
/// What is <em>not</em> here is as deliberate as what is. No marketing — that
/// is the campaign path, and it needs consent and an unsubscribe link that must
/// never be able to switch off a receipt. No sign-in codes — this shop signs in
/// by SMS, and mailing a code would be adding a route into the account rather
/// than a template. No email for every order status: "packed" is not news, so
/// the three that are — placed, shipped, delivered — are the three that send.
/// </para>
/// </remarks>
public sealed class EmailTemplates(EmailLinks links)
{
    // -- account and security ------------------------------------------------

    /// <summary>
    /// The reset link.
    /// </summary>
    /// <param name="lifetime">
    /// Passed in rather than written into the copy, so the sentence the
    /// customer reads cannot drift from the expiry the service actually
    /// enforces.
    /// </param>
    public (string Subject, EmailBody Body) PasswordReset(string resetUrl, TimeSpan lifetime)
    {
        const string Heading = "بازیابی گذرواژه";

        var window = lifetime.TotalHours >= 1
            ? $"{PersianFormat.Number((int)Math.Round(lifetime.TotalHours))} ساعت"
            : $"{PersianFormat.Number((int)Math.Round(lifetime.TotalMinutes))} دقیقه";

        var text = new StringBuilder()
            .AppendLine("برای حساب شما درخواست بازیابی گذرواژه ثبت شد.")
            .AppendLine()
            .AppendLine("برای تعیین گذرواژه تازه این نشانی را باز کنید:")
            .AppendLine(resetUrl)
            .AppendLine()
            .AppendLine($"این لینک {window} معتبر است و فقط یک بار کار می‌کند.")
            .AppendLine("اگر شما این درخواست را نداده‌اید، این ایمیل را نادیده بگیرید؛ گذرواژه فعلی شما تغییری نمی‌کند.")
            .ToString();

        var html = Wrap(
            Heading,
            $"این لینک تا {window} معتبر است.",
            P("برای حساب شما درخواست بازیابی گذرواژه ثبت شد. با دکمه‌ی زیر گذرواژه‌ی تازه بگذارید.")
            + Button("تعیین گذرواژه جدید", resetUrl)
            + LinkFallback(resetUrl)
            + Note(
                $"این لینک <strong>{Escape(window)}</strong> معتبر است و فقط یک بار کار می‌کند. "
                + "اگر شما این درخواست را نداده‌اید، این ایمیل را نادیده بگیرید — گذرواژه‌ی فعلی‌تان تغییری نمی‌کند.",
                NoteTone.Warn),
            links.Site,
            links.Support);

        return ("بازیابی گذرواژه بوژان", new EmailBody(text, html));
    }

    public (string Subject, EmailBody Body) PasswordChanged(DateTimeOffset whenUtc)
    {
        const string Heading = "گذرواژه شما تغییر کرد";
        var when = PersianFormat.DateTime(whenUtc);

        var text = new StringBuilder()
            .AppendLine($"گذرواژه حساب بوژان شما در {when} تغییر کرد.")
            .AppendLine()
            .AppendLine("به دلایل امنیتی، همه دستگاه‌هایی که با گذرواژه قبلی وارد شده بودند از حساب خارج شدند.")
            .AppendLine()
            .AppendLine("اگر شما این کار را نکرده‌اید، همین حالا از صفحه فراموشی رمز گذرواژه را تغییر دهید و با پشتیبانی تماس بگیرید:")
            .AppendLine(links.ForgotPassword)
            .ToString();

        var html = Wrap(
            Heading,
            "اگر این کار شما نبوده، همین حالا اقدام کنید.",
            P("گذرواژه‌ی حساب بوژان شما در تاریخ زیر تغییر کرد.")
            + Rows(("زمان", when))
            + Note(
                "به دلایل امنیتی، همه‌ی دستگاه‌هایی که با گذرواژه‌ی قبلی وارد شده بودند از حساب خارج شدند. "
                + "این طبیعی است و لازم نیست کاری بکنید.",
                NoteTone.Good)
            + Note(
                $"""<strong>اگر شما این کار را نکرده‌اید</strong>، همین حالا از <a href="{Escape(links.ForgotPassword)}" style="color:#7a2418;">صفحه‌ی فراموشی رمز</a> گذرواژه را دوباره تغییر دهید و با پشتیبانی تماس بگیرید.""",
                NoteTone.Warn),
            links.Site,
            links.Support);

        return ("گذرواژه حساب بوژان شما تغییر کرد", new EmailBody(text, html));
    }

    public (string Subject, EmailBody Body) Welcome(string firstName)
    {
        var heading = string.IsNullOrWhiteSpace(firstName) ? "خوش آمدید" : $"{firstName} عزیز، خوش آمدید";

        var text = new StringBuilder()
            .AppendLine("حساب شما در بوژان ساخته شد.")
            .AppendLine()
            .AppendLine("از این پس سفارش‌ها، فاکتورها و مرجوعی‌هایتان در حساب کاربری در دسترس است:")
            .AppendLine(links.Site)
            .AppendLine()
            .AppendLine("این نشانی ایمیل برای بازیابی گذرواژه استفاده می‌شود.")
            .ToString();

        var html = Wrap(
            heading,
            "حساب شما ساخته شد.",
            P("حساب شما در بوژان ساخته شد. از این پس سفارش‌ها، فاکتورها و مرجوعی‌هایتان در حساب کاربری در دسترس است.")
            + Button("شروع خرید", links.Site)
            + Note(
                "این نشانی ایمیل برای بازیابی گذرواژه استفاده می‌شود. اگر روزی رمزتان را فراموش کردید، "
                + "لینک بازیابی به همین نشانی می‌آید."),
            links.Site,
            links.Support);

        return ("به بوژان خوش آمدید", new EmailBody(text, html));
    }

    // -- orders --------------------------------------------------------------

    public sealed record OrderLineView(string Title, int Quantity, long LineTotal);

    public (string Subject, EmailBody Body) OrderPlaced(
        string orderNumber,
        Guid orderId,
        DateTimeOffset placedAtUtc,
        string paymentMethod,
        string shippingMethod,
        IReadOnlyList<OrderLineView> lines,
        long discount,
        long shipping,
        long total)
    {
        var heading = "سفارش شما ثبت شد";
        var url = links.Order(orderId);

        var text = new StringBuilder()
            .AppendLine("از خرید شما سپاسگزاریم. سفارش زیر ثبت شد.")
            .AppendLine()
            .AppendLine($"شماره سفارش: {orderNumber}")
            .AppendLine($"تاریخ: {PersianFormat.Date(placedAtUtc)}")
            .AppendLine($"روش پرداخت: {paymentMethod}")
            .AppendLine($"روش ارسال: {shippingMethod}")
            .AppendLine();

        foreach (var line in lines)
        {
            text.AppendLine($"- {line.Title} × {PersianFormat.Number(line.Quantity)} — {PersianFormat.Money(line.LineTotal)}");
        }

        text.AppendLine()
            .AppendLine($"مبلغ کل: {PersianFormat.Money(total)}")
            .AppendLine()
            .AppendLine($"پیگیری سفارش: {url}");

        var html = Wrap(
            heading,
            $"{PersianFormat.Number(lines.Sum(line => line.Quantity))} قلم · {PersianFormat.Money(total)}",
            P("از خرید شما سپاسگزاریم. سفارش زیر ثبت شد و به‌زودی آماده‌سازی می‌شود.")
            + Rows(
                ("شماره سفارش", orderNumber),
                ("تاریخ", PersianFormat.Date(placedAtUtc)),
                ("روش پرداخت", paymentMethod),
                ("روش ارسال", shippingMethod))
            + Items(
                [.. lines.Select(line => new Item(
                    line.Title,
                    PersianFormat.Number(line.Quantity),
                    PersianFormat.Amount(line.LineTotal)))],
                // Dropped when zero rather than printed as "۰" — an order with
                // no discount should not have a discount line.
                ("تخفیف", discount > 0 ? $"−{PersianFormat.Amount(discount)}" : null),
                ("هزینه ارسال", shipping > 0 ? PersianFormat.Amount(shipping) : null),
                ("مبلغ کل", PersianFormat.Money(total)))
            + Button("پیگیری سفارش", url),
            links.Site,
            links.Support);

        return ($"سفارش {orderNumber} ثبت شد", new EmailBody(text.ToString(), html));
    }

    public (string Subject, EmailBody Body) OrderShipped(
        string orderNumber,
        Guid orderId,
        string shippingMethod,
        string? trackingCode,
        string address)
    {
        const string Heading = "سفارش شما ارسال شد";
        var url = links.Order(orderId);
        var tracking = string.IsNullOrWhiteSpace(trackingCode) ? null : PersianFormat.Digits(trackingCode);

        var text = new StringBuilder()
            .AppendLine("بسته شما تحویل شرکت پست شد.")
            .AppendLine()
            .AppendLine($"شماره سفارش: {orderNumber}")
            .AppendLine($"روش ارسال: {shippingMethod}");

        if (tracking is not null)
        {
            text.AppendLine($"کد رهگیری: {tracking}");
        }

        text.AppendLine($"نشانی: {address}")
            .AppendLine()
            .AppendLine($"پیگیری سفارش: {url}");

        var html = Wrap(
            Heading,
            tracking is null ? "بسته شما تحویل پست شد." : $"کد رهگیری: {tracking}",
            P("بسته‌ی شما تحویل شرکت پست شد.")
            + Rows(
                ("شماره سفارش", orderNumber),
                ("روش ارسال", shippingMethod),
                ("کد رهگیری", tracking),
                ("نشانی", address))
            + Button("پیگیری سفارش", url)
            + (tracking is null
                ? string.Empty
                : Note("کد رهگیری معمولاً چند ساعت پس از تحویل به پست، در سامانه‌ی آن‌ها فعال می‌شود.")),
            links.Site,
            links.Support);

        return ($"سفارش {orderNumber} ارسال شد", new EmailBody(text.ToString(), html));
    }

    public (string Subject, EmailBody Body) OrderDelivered(
        string orderNumber,
        Guid orderId,
        string invoiceNumber,
        DateTimeOffset deliveredAtUtc,
        long total)
    {
        const string Heading = "سفارش شما تحویل شد";
        var url = links.Invoice(orderId);

        var text = new StringBuilder()
            .AppendLine("امیدواریم از خریدتان راضی باشید. فاکتور این سفارش صادر شد.")
            .AppendLine()
            .AppendLine($"شماره سفارش: {orderNumber}")
            .AppendLine($"شماره فاکتور: {invoiceNumber}")
            .AppendLine($"تاریخ تحویل: {PersianFormat.Date(deliveredAtUtc)}")
            .AppendLine($"مبلغ فاکتور: {PersianFormat.Money(total)}")
            .AppendLine()
            .AppendLine($"مشاهده فاکتور: {url}")
            .ToString();

        var html = Wrap(
            Heading,
            "فاکتور شما صادر شد.",
            P("امیدواریم از خریدتان راضی باشید. فاکتور این سفارش صادر شد.")
            + Rows(
                ("شماره سفارش", orderNumber),
                ("شماره فاکتور", invoiceNumber),
                ("تاریخ تحویل", PersianFormat.Date(deliveredAtUtc)),
                ("مبلغ فاکتور", PersianFormat.Money(total)))
            + Button("مشاهده و چاپ فاکتور", url)
            + Note("اگر کالایی مشکل داشت، تا مهلت اعلام‌شده می‌توانید از حساب کاربری درخواست مرجوعی ثبت کنید."),
            links.Site,
            links.Support);

        return ($"سفارش {orderNumber} تحویل شد", new EmailBody(text, html));
    }

    public (string Subject, EmailBody Body) OrderCancelled(
        string orderNumber,
        long orderTotal,
        long refunded,
        long penalty,
        long manualGatewayRefund,
        bool penaltyExplained)
    {
        const string Heading = "سفارش شما لغو شد";

        var text = new StringBuilder()
            .AppendLine("سفارش زیر لغو شد و مبلغ آن تسویه گردید.")
            .AppendLine()
            .AppendLine($"شماره سفارش: {orderNumber}")
            .AppendLine($"مبلغ سفارش: {PersianFormat.Money(orderTotal)}");

        if (penalty > 0)
        {
            text.AppendLine($"جریمه لغو: {PersianFormat.Money(penalty)}");
        }

        if (refunded > 0)
        {
            text.AppendLine($"بازگشت به کیف پول: {PersianFormat.Money(refunded)}");
        }

        if (manualGatewayRefund > 0)
        {
            text.AppendLine($"بازگشت از درگاه بانکی: {PersianFormat.Money(manualGatewayRefund)} (به‌صورت دستی انجام می‌شود)");
        }

        text.AppendLine().AppendLine($"کیف پول: {links.Wallet}");

        var html = Wrap(
            Heading,
            refunded > 0 ? $"{PersianFormat.Money(refunded)} به کیف پول شما بازگشت." : "سفارش شما لغو شد.",
            P("سفارش زیر لغو شد و مبلغ آن تسویه گردید.")
            + Rows(
                ("شماره سفارش", orderNumber),
                ("مبلغ سفارش", PersianFormat.Money(orderTotal)),
                ("جریمه لغو", penalty > 0 ? PersianFormat.Money(penalty) : null),
                ("بازگشت به کیف پول", refunded > 0 ? PersianFormat.Money(refunded) : null))
            // Only when a penalty was actually charged. A number without its
            // reason is what turns a cancellation into a support ticket.
            + (penalty > 0 && penaltyExplained
                ? Note("چون سفارش پس از بسته‌بندی لغو شد، طبق شرایط فروشگاه درصدی از مبلغ به‌عنوان هزینه‌ی آماده‌سازی کسر شد.")
                : string.Empty)
            // The gateway's share cannot be returned automatically — there is
            // no adapter that can do it — so saying so is the honest thing.
            + (manualGatewayRefund > 0
                ? Note(
                    $"مبلغ {Escape(PersianFormat.Money(manualGatewayRefund))} که از درگاه بانکی پرداخت شده بود، "
                    + "به‌صورت دستی توسط پشتیبانی بازگردانده می‌شود.")
                : string.Empty)
            + Button("مشاهده کیف پول", links.Wallet),
            links.Site,
            links.Support);

        return ($"سفارش {orderNumber} لغو شد", new EmailBody(text.ToString(), html));
    }

    // -- returns -------------------------------------------------------------

    public (string Subject, EmailBody Body) ReturnSubmitted(
        string code,
        Guid returnId,
        string orderNumber,
        string productSummary,
        string reason)
    {
        const string Heading = "درخواست مرجوعی شما ثبت شد";
        var url = links.Return(returnId);

        var text = new StringBuilder()
            .AppendLine("درخواست شما ثبت شد و در نوبت بررسی قرار گرفت.")
            .AppendLine()
            .AppendLine($"کد پیگیری: {code}")
            .AppendLine($"سفارش: {orderNumber}")
            .AppendLine($"کالا: {productSummary}")
            .AppendLine($"دلیل: {reason}")
            .AppendLine()
            .AppendLine("مراحل بعدی: بررسی درخواست ← تأیید ← دریافت کالا ← بازپرداخت.")
            .AppendLine()
            .AppendLine($"پیگیری مرجوعی: {url}")
            .ToString();

        var html = Wrap(
            Heading,
            "در حال بررسی است.",
            P("درخواست شما ثبت شد و در نوبت بررسی قرار گرفت.")
            + Rows(
                ("کد پیگیری", code),
                ("سفارش", orderNumber),
                ("کالا", productSummary),
                ("دلیل", reason))
            + Note(
                "مراحل بعدی: بررسی درخواست ← تأیید ← دریافت کالا ← بازپرداخت. "
                + "در هر مرحله وضعیت را در حساب کاربری می‌بینید.")
            + Button("پیگیری مرجوعی", url),
            links.Site,
            links.Support);

        return ($"درخواست مرجوعی {code} ثبت شد", new EmailBody(text, html));
    }

    public (string Subject, EmailBody Body) ReturnRefunded(string code, Guid orderId, long amount, string destination)
    {
        const string Heading = "مبلغ مرجوعی بازپرداخت شد";
        var url = links.Invoice(orderId);

        var text = new StringBuilder()
            .AppendLine("کالای مرجوعی دریافت و تأیید شد و مبلغ آن بازگردانده شد.")
            .AppendLine()
            .AppendLine($"کد پیگیری: {code}")
            .AppendLine($"مبلغ بازپرداخت: {PersianFormat.Money(amount)}")
            .AppendLine($"مقصد: {destination}")
            .AppendLine()
            .AppendLine("فاکتور آن سفارش هم به‌روز شد: قلم مرجوعی از فاکتور حذف شد.")
            .AppendLine($"مشاهده فاکتور: {url}")
            .ToString();

        var html = Wrap(
            Heading,
            $"{PersianFormat.Money(amount)} بازگردانده شد.",
            P("کالای مرجوعی دریافت و تأیید شد و مبلغ آن بازگردانده شد.")
            + Rows(
                ("کد پیگیری", code),
                ("مبلغ بازپرداخت", PersianFormat.Money(amount)),
                ("مقصد", destination))
            // Said here because the invoice really does change at this moment:
            // the returned line comes off it. A customer comparing it with the
            // delivery email would otherwise think one of them is wrong.
            + Note(
                "فاکتور آن سفارش هم به‌روز شد: قلم مرجوعی از فاکتور حذف و سهم آن از تخفیف و هزینه‌ی ارسال کسر شد.",
                NoteTone.Good)
            + Button("مشاهده فاکتور به‌روزشده", url),
            links.Site,
            links.Support);

        return ($"مبلغ مرجوعی {code} بازپرداخت شد", new EmailBody(text, html));
    }

    // -- wallet --------------------------------------------------------------

    public (string Subject, EmailBody Body) WalletToppedUp(long amount, long balance, DateTimeOffset whenUtc)
    {
        const string Heading = "کیف پول شما شارژ شد";

        var text = new StringBuilder()
            .AppendLine("رسید کارت‌به‌کارت شما بررسی و تأیید شد.")
            .AppendLine()
            .AppendLine($"مبلغ شارژ: {PersianFormat.Money(amount)}")
            .AppendLine($"موجودی جدید: {PersianFormat.Money(balance)}")
            .AppendLine($"تاریخ: {PersianFormat.Date(whenUtc)}")
            .AppendLine()
            .AppendLine($"کیف پول: {links.Wallet}")
            .ToString();

        var html = Wrap(
            Heading,
            $"موجودی جدید: {PersianFormat.Money(balance)}",
            P("رسید کارت‌به‌کارت شما بررسی و تأیید شد.")
            + Rows(
                ("مبلغ شارژ", PersianFormat.Money(amount)),
                ("موجودی جدید", PersianFormat.Money(balance)),
                ("تاریخ", PersianFormat.Date(whenUtc)))
            + Button("مشاهده کیف پول", links.Wallet),
            links.Site,
            links.Support);

        return ("شارژ کیف پول شما تأیید شد", new EmailBody(text, html));
    }

    public (string Subject, EmailBody Body) WalletRejected(long amount, DateTimeOffset whenUtc, string? reason)
    {
        const string Heading = "شارژ کیف پول تأیید نشد";

        // The operator's own note when there is one. When there is not, a
        // generic line rather than an empty box: the customer has sent money
        // and been refused, and "no reason given" is the worst possible answer.
        var explained = string.IsNullOrWhiteSpace(reason)
            ? "مغایرت در رسید ارسالی."
            : reason.Trim();

        var text = new StringBuilder()
            .AppendLine("درخواست شارژ زیر بررسی شد و تأیید نشد.")
            .AppendLine()
            .AppendLine($"مبلغ درخواست: {PersianFormat.Money(amount)}")
            .AppendLine($"تاریخ: {PersianFormat.Date(whenUtc)}")
            .AppendLine($"دلیل: {explained}")
            .AppendLine()
            .AppendLine("اگر فکر می‌کنید اشتباهی رخ داده، با پشتیبانی تماس بگیرید و کد پیگیری واریز را همراه داشته باشید:")
            .AppendLine(links.Support)
            .ToString();

        var html = Wrap(
            Heading,
            explained,
            P("درخواست شارژ زیر بررسی شد و تأیید نشد.")
            + Rows(
                ("مبلغ درخواست", PersianFormat.Money(amount)),
                ("تاریخ", PersianFormat.Date(whenUtc)))
            + Note($"<strong>دلیل:</strong> {Escape(explained)}", NoteTone.Warn)
            + P("اگر فکر می‌کنید اشتباهی رخ داده، با پشتیبانی تماس بگیرید و کد پیگیری واریز را همراه داشته باشید.")
            // The one email whose action is "talk to a person", because it is
            // the one situation where nothing on the site will help.
            + Button("تماس با پشتیبانی", links.Support),
            links.Site,
            links.Support);

        return ("شارژ کیف پول شما تأیید نشد", new EmailBody(text, html));
    }

    // -- support and business ------------------------------------------------

    public (string Subject, EmailBody Body) TicketReplied(Guid ticketId, string subject)
    {
        const string Heading = "پشتیبانی به شما پاسخ داد";
        var url = links.Ticket(ticketId);

        var text = new StringBuilder()
            .AppendLine("پشتیبانی به تیکت شما پاسخ داد.")
            .AppendLine()
            .AppendLine($"موضوع تیکت: {subject}")
            .AppendLine()
            .AppendLine($"خواندن پاسخ: {url}")
            .ToString();

        var html = Wrap(
            Heading,
            subject,
            Rows(("موضوع تیکت", subject))
            + Button("خواندن پاسخ", url)
            // The reply itself is deliberately not here: a ticket can carry
            // order details or account information, and copying it into an
            // inbox puts that outside the account it belongs to.
            + Note(
                "متن پاسخ در حساب کاربری شماست. برای ادامه‌ی گفتگو از همان‌جا پاسخ بدهید "
                + "تا سابقه‌ی تیکت یک‌جا بماند."),
            links.Site,
            links.Support);

        return ("پاسخ پشتیبانی به تیکت شما", new EmailBody(text, html));
    }

    public (string Subject, EmailBody Body) QuoteIssued(
        string quoteNumber,
        Guid quoteId,
        string requestCode,
        long total,
        DateTimeOffset? expiresAtUtc)
    {
        const string Heading = "پیش‌فاکتور شما آماده شد";
        var url = links.Quote(quoteId);
        var expires = expiresAtUtc is { } value ? PersianFormat.Date(value) : null;

        var text = new StringBuilder()
            .AppendLine("برای درخواست سازمانی شما پیش‌فاکتور صادر شد.")
            .AppendLine()
            .AppendLine($"شماره پیش‌فاکتور: {quoteNumber}")
            .AppendLine($"درخواست: {requestCode}")
            .AppendLine($"مبلغ کل: {PersianFormat.Money(total)}");

        if (expires is not null)
        {
            text.AppendLine($"اعتبار تا: {expires}");
        }

        text.AppendLine().AppendLine($"مشاهده پیش‌فاکتور: {url}");

        var html = Wrap(
            Heading,
            expires is null ? $"مبلغ کل: {PersianFormat.Money(total)}" : $"تا {expires} معتبر است.",
            P("برای درخواست سازمانی شما پیش‌فاکتور صادر شد.")
            + Rows(
                ("شماره پیش‌فاکتور", quoteNumber),
                ("درخواست", requestCode),
                ("مبلغ کل", PersianFormat.Money(total)),
                ("اعتبار تا", expires))
            + Button("مشاهده پیش‌فاکتور", url)
            + (expires is null
                ? string.Empty
                : Note($"قیمت‌ها تا {Escape(expires)} معتبرند. پس از آن باید استعلام تازه ثبت شود.")),
            links.Site,
            links.Support);

        return ($"پیش‌فاکتور {quoteNumber} آماده شد", new EmailBody(text.ToString(), html));
    }

    // -- catalogue -----------------------------------------------------------

    public (string Subject, EmailBody Body) BackInStock(string productTitle, string productSlug, long price)
    {
        var heading = "کالای مورد نظر شما موجود شد";
        var url = links.Product(productSlug);

        var text = new StringBuilder()
            .AppendLine("شما خواسته بودید وقتی این کالا موجود شد خبرتان کنیم:")
            .AppendLine()
            .AppendLine($"کالا: {productTitle}")
            .AppendLine($"قیمت: {PersianFormat.Money(price)}")
            .AppendLine()
            .AppendLine($"مشاهده و خرید: {url}")
            .ToString();

        var html = Wrap(
            heading,
            "شما خواسته بودید خبرتان کنیم.",
            // Said plainly, because this is the one template that could be
            // mistaken for marketing — and it is not: the customer asked for it.
            P("شما خواسته بودید وقتی این کالا موجود شد خبرتان کنیم:")
            + Rows(
                ("کالا", productTitle),
                ("قیمت", PersianFormat.Money(price)))
            + Button("مشاهده و خرید", url)
            + Note("موجودی محدود است و این اطلاع برای همه‌ی کسانی که درخواست داده بودند ارسال شده."),
            links.Site,
            links.Support);

        return ($"«{productTitle}» موجود شد", new EmailBody(text, html));
    }
}
