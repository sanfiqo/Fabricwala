using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Common;
using TextileConnect.Application.Files;
using TextileConnect.Application.Invoicing;
using TextileConnect.Application.Payments;
using TextileConnect.Domain;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Payments;

public sealed class EfFinanceService(MarketplaceDbContext db, IFileStorage storage, TimeProvider? clock = null) : IFinanceService
{
    private const int NumberingAttempts = 5;
    private const string SellerInvoice = "SellerToRetailer";
    private const string FeeInvoice = "PlatformCommission";
    private const string CreditNote = "CreditNote";
    /// <summary>Series of the platform fee invoices. A seller series is the seller id, which is never letters.</summary>
    private const string PlatformSeries = "PF";
    /// <summary>Accounting code for the platform fee (other support services). To be confirmed with the owner's accountant.</summary>
    private const string FeeServiceCode = "998599";

    public async Task<PayoutAccount> AddPayoutAccountAsync(long businessId, string linkedAccountId, CancellationToken cancellationToken = default)
    {
        var account = await db.PayoutAccounts.SingleOrDefaultAsync(x => x.BusinessId == businessId, cancellationToken);
        if (account is null) { account = new PayoutAccount { BusinessId = businessId, GatewayLinkedAccountId = linkedAccountId }; db.PayoutAccounts.Add(account); }
        else account.GatewayLinkedAccountId = linkedAccountId;
        await db.SaveChangesAsync(cancellationToken);
        return account;
    }

    public async Task<Invoice> CreateInvoiceAsync(long sellerOrderId, string type, CancellationToken cancellationToken = default)
    {
        var sellerOrder = await LoadAsync(sellerOrderId, cancellationToken);
        if (type == SellerInvoice) return await IssueAsync(sellerOrder, SellerInvoice, sellerOrder.ManufacturerBusinessId.ToString(System.Globalization.CultureInfo.InvariantCulture), BuildSellerInvoiceAsync, cancellationToken);
        // The platform fee: a tax invoice when the order carries GST on the fee, otherwise the plain statement of before the platform company was set up.
        return sellerOrder.CommissionGst > 0
            ? await IssueAsync(sellerOrder, FeeInvoice, PlatformSeries, BuildFeeInvoiceAsync, cancellationToken)
            : await CreateFeeStatementAsync(sellerOrder, cancellationToken);
    }

    /// <summary>
    /// A credit note for a refund that went through. It names the tax invoice it corrects, takes the GST share out of the refund in the same proportion as that invoice carried it
    /// and follows the same CGST/SGST or IGST. Its number comes from the seller series "C&lt;seller id&gt;". Refused while the refund is not processed, and when no tax invoice was ever
    /// issued for the order (nothing to correct: for example an order rejected before dispatch).
    /// </summary>
    public async Task<Invoice> CreateCreditNoteAsync(long refundId, CancellationToken cancellationToken = default)
    {
        var refund = await db.Refunds.AsNoTracking().SingleOrDefaultAsync(x => x.Id == refundId, cancellationToken) ?? throw new KeyNotFoundException("Refund not found.");
        var sellerOrder = await LoadAsync(refund.SellerOrderId, cancellationToken);
        var existing = await db.Invoices.AsNoTracking().SingleOrDefaultAsync(x => x.SellerOrderId == sellerOrder.Id && x.InvoiceType == CreditNote && x.RefundId == refundId, cancellationToken);
        if (existing is not null) return existing;
        if (refund.Status != RefundStatus.Processed)
            throw new RuleViolationException("refund_not_processed", "The credit note is made when the refund has gone through.");
        var original = await db.Invoices.AsNoTracking().SingleOrDefaultAsync(x => x.SellerOrderId == sellerOrder.Id && x.InvoiceType == SellerInvoice && x.RefundId == 0, cancellationToken)
            ?? throw new RuleViolationException("credit_note_not_applicable", "No tax invoice was issued for this order, so there is nothing to correct.");

        return await IssueAsync(sellerOrder, CreditNote, "C" + sellerOrder.ManufacturerBusinessId.ToString(System.Globalization.CultureInfo.InvariantCulture), async (order, number, now, ct) =>
        {
            var basis = await BuildSellerInvoiceAsync(order, original.InvoiceNumber, original.IssuedAt, ct);
            var (taxable, tax) = CreditNoteSplit.Split(refund.Amount, basis.Tax, basis.Total);
            var percent = taxable == 0 ? 0m : decimal.Round(tax * 100m / taxable, 2, MidpointRounding.AwayFromZero);
            return GstInvoiceBuilder.Build(new GstInvoiceInput(number, now, basis.Seller, basis.Buyer, string.Empty, basis.PlaceOfSupplyName, basis.PlaceOfSupplyCode, basis.OrderNumber,
                [new GstItemInput($"Refund for order {basis.OrderNumber}: {refund.Reason}", null, null, null, 1, taxable, percent, tax)], 0m, refund.Amount,
                $"Credit note against tax invoice {original.InvoiceNumber} dated {FinancialYear.IndiaDate(original.IssuedAt):dd MMM yyyy}. The amount is refunded to the retailer. This is a computer generated document and needs no signature.",
                "CREDIT NOTE"));
        }, cancellationToken, refundId);
    }

    private Task<Domain.Orders.SellerOrder> LoadAsync(long sellerOrderId, CancellationToken cancellationToken)
        => db.SellerOrders.Include(x => x.Order).Include(x => x.Items).SingleAsync(x => x.Id == sellerOrderId, cancellationToken);

    /// <summary>
    /// Issues one tax invoice per seller order and type. Asking again returns the same document, because a tax invoice must not change after it is issued.
    /// It is refused until the goods are dispatched (the invoice is raised for a supply). The number is the next value of the series, taken in the same save as the invoice.
    /// </summary>
    private async Task<Invoice> IssueAsync(Domain.Orders.SellerOrder sellerOrder, string type, string series, Func<Domain.Orders.SellerOrder, string, DateTime, CancellationToken, Task<GstInvoice>> build, CancellationToken cancellationToken, long refundId = 0)
    {
        // A credit note follows a tax invoice that exists, which is checked when it is built; the others need a dispatched order.
        if (refundId == 0 && sellerOrder.Status is not (SellerOrderStatus.Dispatched or SellerOrderStatus.Delivered))
            throw new RuleViolationException("invoice_not_ready", "The invoice is made when the seller dispatches the order.");

        for (var attempt = 1; ; attempt++)
        {
            var existing = await db.Invoices.SingleOrDefaultAsync(x => x.SellerOrderId == sellerOrder.Id && x.InvoiceType == type && x.RefundId == refundId, cancellationToken);
            if (existing is not null) return existing;

            var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
            var year = FinancialYear.Code(now);
            var sequence = await db.InvoiceSequences.SingleOrDefaultAsync(x => x.Series == series && x.FinancialYear == year, cancellationToken);
            if (sequence is null) { sequence = new InvoiceSequence { Series = series, FinancialYear = year }; db.InvoiceSequences.Add(sequence); }
            sequence.LastNumber++;
            var number = InvoiceNumbers.Format(series, year, sequence.LastNumber);

            Invoice invoice;
            try
            {
                var model = await build(sellerOrder, number, now, cancellationToken);
                await using var content = new MemoryStream(GstInvoicePdf.Render(model));
                invoice = new Invoice
                {
                    SellerOrderId = sellerOrder.Id, InvoiceNumber = number, InvoiceType = type, RefundId = refundId, IssuedAt = now,
                    // The figures as printed, for the registers of the accountant (IInvoiceRegister). The other party of a platform fee invoice is the seller.
                    Taxable = model.Taxable, Shipping = model.Shipping, Cgst = model.Cgst, Sgst = model.Sgst, Igst = model.Igst, Total = model.Total,
                    CounterpartyName = model.Buyer.Name, CounterpartyGstin = model.Buyer.Gstin, IssuerGstin = model.Seller.Gstin, PlaceOfSupplyCode = model.PlaceOfSupplyCode
                };
                invoice.FileKey = await storage.SavePrivateAsync(content, $"{number.Replace('/', '-')}.pdf", cancellationToken);
            }
            catch
            {
                // Nothing was issued, so the number must not be used: forget the change to the counter, or the next save on this context would write it.
                db.Entry(sequence).State = EntityState.Detached;
                throw;
            }
            db.Invoices.Add(invoice);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return invoice;
            }
            catch (DbUpdateException) when (attempt < NumberingAttempts)
            {
                // Another request took the same number (or made the same invoice) first. Start again from what is stored now.
                db.ChangeTracker.Clear();
                sellerOrder = await LoadAsync(sellerOrder.Id, cancellationToken);
            }
        }
    }

    // ---- the invoice of the seller to the retailer ---------------------------------------------------------------------------------

    private async Task<GstInvoice> BuildSellerInvoiceAsync(Domain.Orders.SellerOrder sellerOrder, string number, DateTime now, CancellationToken cancellationToken)
    {
        var order = sellerOrder.Order!;
        var seller = await db.Businesses.AsNoTracking().SingleAsync(x => x.Id == sellerOrder.ManufacturerBusinessId, cancellationToken);
        var buyer = await db.Businesses.AsNoTracking().SingleAsync(x => x.Id == order.RetailerBusinessId, cancellationToken);
        RequireGstin(seller);

        var states = await db.States.AsNoTracking().ToListAsync(cancellationToken);
        var address = await RegisteredAddressAsync(seller.Id, cancellationToken)
            ?? throw new RuleViolationException("seller_address_missing", "The seller must add the registered address (street line, city and pincode) before a tax invoice can be made.");

        var sellerStateCode = seller.Gstin![..2];
        var supply = await StateOfPincodeAsync(order.DeliveryPincode, cancellationToken);
        var supplyCode = supply is null ? null : GstStateCodes.For(supply.Code);
        if (supply is null || supplyCode is null)
            throw new RuleViolationException("place_of_supply_unknown", "The state of the delivery pincode is not known, so the GST cannot be split. Please contact support.");

        var buyerAddress = await RegisteredAddressAsync(buyer.Id, cancellationToken);
        var delivery = Join(order.DeliveryLine1, order.DeliveryLine2, order.DeliveryCity, order.DeliveryPincode);
        var buyerText = buyerAddress is null ? delivery : Join(buyerAddress.Line1, buyerAddress.Line2, buyerAddress.City, buyerAddress.Pincode);
        var buyerStateCode = buyer.Gstin is { Length: >= 2 } ? buyer.Gstin[..2] : supplyCode;

        return GstInvoiceBuilder.Build(new GstInvoiceInput(
            number, now,
            new GstParty(seller.LegalName, seller.Gstin, Join(address.Line1, address.Line2, address.City, address.Pincode), StateName(states, sellerStateCode), sellerStateCode),
            new GstParty(buyer.LegalName, buyer.Gstin, buyerText, StateName(states, buyerStateCode, supply.Name), buyerStateCode),
            delivery, supply.Name, supplyCode, order.OrderNumber,
            sellerOrder.Items.OrderBy(x => x.Id).Select(x => new GstItemInput(x.Title, x.Size, x.Colour, x.HsnCode, x.Quantity, x.UnitPrice, x.GstRate, x.GstAmount)).ToList(),
            sellerOrder.ShippingAmount, sellerOrder.Total));
    }

    // ---- the invoice of the platform to the seller ---------------------------------------------------------------------------------

    /// <summary>
    /// The platform fee as a tax invoice: one line, the fee as the taxable value and the GST frozen on the order. The recipient is the seller, a registered person,
    /// so the place of supply is the state of the seller GSTIN and the split between CGST/SGST and IGST follows from it and the state of the platform GSTIN.
    /// </summary>
    private async Task<GstInvoice> BuildFeeInvoiceAsync(Domain.Orders.SellerOrder sellerOrder, string number, DateTime now, CancellationToken cancellationToken)
    {
        var platform = await PlatformCompany.LoadAsync(db, cancellationToken)
            ?? throw new RuleViolationException("platform_details_missing", "The platform company details (name, GSTIN, address) are not set in the admin settings, so the fee invoice cannot be made yet.");
        var seller = await db.Businesses.AsNoTracking().SingleAsync(x => x.Id == sellerOrder.ManufacturerBusinessId, cancellationToken);
        RequireGstin(seller);
        var states = await db.States.AsNoTracking().ToListAsync(cancellationToken);
        var sellerAddress = await RegisteredAddressAsync(seller.Id, cancellationToken)
            ?? throw new RuleViolationException("seller_address_missing", "The seller must add the registered address (street line, city and pincode) before a tax invoice can be made.");
        var sellerStateCode = seller.Gstin![..2];
        var sellerStateName = StateName(states, sellerStateCode);
        var ratePercent = sellerOrder.CommissionAmount == 0 ? 0m : decimal.Round(sellerOrder.CommissionGst * 100m / sellerOrder.CommissionAmount, 2, MidpointRounding.AwayFromZero);

        return GstInvoiceBuilder.Build(new GstInvoiceInput(
            number, now,
            new GstParty(platform.LegalName, platform.Gstin, Join(platform.Address, platform.Pincode), StateName(states, platform.StateCode), platform.StateCode),
            new GstParty(seller.LegalName, seller.Gstin, Join(sellerAddress.Line1, sellerAddress.Line2, sellerAddress.City, sellerAddress.Pincode), sellerStateName, sellerStateCode),
            string.Empty, sellerStateName, sellerStateCode, sellerOrder.Order!.OrderNumber,
            [new GstItemInput($"Platform fee for order {sellerOrder.Order.OrderNumber} ({sellerOrder.CommissionRate * 100m:0.##}% of goods Rs {sellerOrder.SubTotal:0.00})", null, null, FeeServiceCode, 1, sellerOrder.CommissionAmount, ratePercent, sellerOrder.CommissionGst)],
            0m, sellerOrder.CommissionAmount + sellerOrder.CommissionGst,
            "This is a computer generated invoice and needs no signature. The fee and its GST are deducted from the payout of the order."));
    }

    /// <summary>The platform fee note for the seller when the order carries no GST on the fee. It is not a tax invoice, so it has no GST series and no place of supply.</summary>
    private async Task<Invoice> CreateFeeStatementAsync(Domain.Orders.SellerOrder sellerOrder, CancellationToken cancellationToken)
    {
        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var sellerName = await db.Businesses.AsNoTracking().Where(x => x.Id == sellerOrder.ManufacturerBusinessId).Select(x => x.LegalName).SingleAsync(cancellationToken);
        var invoice = new Invoice { SellerOrderId = sellerOrder.Id, InvoiceNumber = $"TC-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..20], InvoiceType = FeeInvoice, IssuedAt = now };
        var pdf = GstInvoicePdf.RenderFeeStatement(invoice.InvoiceNumber, sellerOrder.Order!.OrderNumber, sellerName, sellerOrder.SubTotal, sellerOrder.CommissionRate * 100m, sellerOrder.CommissionAmount);
        await using var content = new MemoryStream(pdf);
        invoice.FileKey = await storage.SavePrivateAsync(content, $"{invoice.InvoiceNumber}.pdf", cancellationToken);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);
        return invoice;
    }

    // ---- shared pieces ------------------------------------------------------------------------------------------------------------

    private static void RequireGstin(Business seller)
    {
        if (string.IsNullOrWhiteSpace(seller.Gstin) || !GstinRules.IsValid(seller.Gstin))
            throw new RuleViolationException("seller_gstin_missing", "The seller must add a valid GSTIN to the business profile before a tax invoice can be made.");
    }

    private static string StateName(IEnumerable<State> states, string gstCode, string? fallback = null)
        => states.FirstOrDefault(x => GstStateCodes.For(x.Code) == gstCode)?.Name ?? fallback ?? $"State {gstCode}";

    private sealed record AddressRow(string Line1, string? Line2, string Pincode, string City);

    private Task<AddressRow?> RegisteredAddressAsync(long businessId, CancellationToken cancellationToken)
        => (from a in db.BusinessAddresses.AsNoTracking()
            join c in db.Cities.AsNoTracking() on a.CityId equals c.Id
            where a.BusinessId == businessId && a.AddressType == AddressType.Registered && a.Line1 != string.Empty
            orderby a.IsDefault descending, a.Id
            select new AddressRow(a.Line1, a.Line2, a.Pincode, c.Name)).FirstOrDefaultAsync(cancellationToken);

    private sealed record SupplyState(string Name, string Code);

    private Task<SupplyState?> StateOfPincodeAsync(string pincode, CancellationToken cancellationToken)
        => (from p in db.PostalCodes.AsNoTracking()
            join c in db.Cities.AsNoTracking() on p.CityId equals c.Id
            join s in db.States.AsNoTracking() on c.StateId equals s.Id
            where p.Pincode == pincode
            select new SupplyState(s.Name, s.Code)).FirstOrDefaultAsync(cancellationToken);

    private static string Join(params string?[] parts) => string.Join(", ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
}
