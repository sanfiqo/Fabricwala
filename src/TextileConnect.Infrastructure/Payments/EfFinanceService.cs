using Microsoft.EntityFrameworkCore;
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
        return type == "PlatformCommission" ? await CreateFeeStatementAsync(sellerOrder, cancellationToken) : await CreateTaxInvoiceAsync(sellerOrder, cancellationToken);
    }

    private Task<Domain.Orders.SellerOrder> LoadAsync(long sellerOrderId, CancellationToken cancellationToken)
        => db.SellerOrders.Include(x => x.Order).Include(x => x.Items).SingleAsync(x => x.Id == sellerOrderId, cancellationToken);

    /// <summary>
    /// The GST invoice of a seller to a retailer. It exists once per seller order: asking again returns the same document, because a tax invoice must not change after it is issued.
    /// It is refused until the goods are dispatched (the invoice is raised for a supply), and when the GSTIN or the address of the seller is missing, with a message that says what to complete.
    /// </summary>
    private async Task<Invoice> CreateTaxInvoiceAsync(Domain.Orders.SellerOrder sellerOrder, CancellationToken cancellationToken)
    {
        if (sellerOrder.Status is not (SellerOrderStatus.Dispatched or SellerOrderStatus.Delivered))
            throw new RuleViolationException("invoice_not_ready", "The invoice is made when the seller dispatches the order.");

        for (var attempt = 1; ; attempt++)
        {
            var existing = await db.Invoices.SingleOrDefaultAsync(x => x.SellerOrderId == sellerOrder.Id && x.InvoiceType == "SellerToRetailer", cancellationToken);
            if (existing is not null) return existing;

            var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
            var year = FinancialYear.Code(now);
            var sequence = await db.InvoiceSequences.SingleOrDefaultAsync(x => x.SellerBusinessId == sellerOrder.ManufacturerBusinessId && x.FinancialYear == year, cancellationToken);
            if (sequence is null) { sequence = new InvoiceSequence { SellerBusinessId = sellerOrder.ManufacturerBusinessId, FinancialYear = year }; db.InvoiceSequences.Add(sequence); }
            sequence.LastNumber++;
            var number = InvoiceNumbers.Format(sellerOrder.ManufacturerBusinessId, year, sequence.LastNumber);

            var model = await BuildModelAsync(sellerOrder, number, now, cancellationToken);
            await using var content = new MemoryStream(GstInvoicePdf.Render(model));
            var invoice = new Invoice { SellerOrderId = sellerOrder.Id, InvoiceNumber = number, InvoiceType = "SellerToRetailer", IssuedAt = now };
            invoice.FileKey = await storage.SavePrivateAsync(content, $"{number.Replace('/', '-')}.pdf", cancellationToken);
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

    private async Task<GstInvoice> BuildModelAsync(Domain.Orders.SellerOrder sellerOrder, string number, DateTime now, CancellationToken cancellationToken)
    {
        var order = sellerOrder.Order!;
        var seller = await db.Businesses.AsNoTracking().SingleAsync(x => x.Id == sellerOrder.ManufacturerBusinessId, cancellationToken);
        var buyer = await db.Businesses.AsNoTracking().SingleAsync(x => x.Id == order.RetailerBusinessId, cancellationToken);

        if (string.IsNullOrWhiteSpace(seller.Gstin) || !TextileConnect.Application.Businesses.GstinRules.IsValid(seller.Gstin))
            throw new RuleViolationException("seller_gstin_missing", "The seller must add a valid GSTIN to the business profile before a tax invoice can be made.");

        var states = await db.States.AsNoTracking().ToListAsync(cancellationToken);
        var address = await RegisteredAddressAsync(seller.Id, cancellationToken)
            ?? throw new RuleViolationException("seller_address_missing", "The seller must add the registered address (street line, city and pincode) before a tax invoice can be made.");

        var sellerStateCode = seller.Gstin![..2];
        var sellerState = states.FirstOrDefault(x => GstStateCodes.For(x.Code) == sellerStateCode)?.Name ?? $"State {sellerStateCode}";

        var supply = await (from p in db.PostalCodes.AsNoTracking()
                            join c in db.Cities.AsNoTracking() on p.CityId equals c.Id
                            join s in db.States.AsNoTracking() on c.StateId equals s.Id
                            where p.Pincode == order.DeliveryPincode
                            select new { s.Name, s.Code }).FirstOrDefaultAsync(cancellationToken);
        var supplyCode = supply is null ? null : GstStateCodes.For(supply.Code);
        if (supply is null || supplyCode is null)
            throw new RuleViolationException("place_of_supply_unknown", "The state of the delivery pincode is not known, so the GST cannot be split. Please contact support.");

        var buyerAddress = await RegisteredAddressAsync(buyer.Id, cancellationToken);
        var delivery = Join(order.DeliveryLine1, order.DeliveryLine2, order.DeliveryCity, order.DeliveryPincode);
        var buyerText = buyerAddress is null ? delivery : Join(buyerAddress.Line1, buyerAddress.Line2, buyerAddress.City, buyerAddress.Pincode);
        var buyerStateCode = buyer.Gstin is { Length: >= 2 } ? buyer.Gstin[..2] : supplyCode;
        var buyerState = states.FirstOrDefault(x => GstStateCodes.For(x.Code) == buyerStateCode)?.Name ?? supply.Name;

        return GstInvoiceBuilder.Build(new GstInvoiceInput(
            number, now,
            new GstParty(seller.LegalName, seller.Gstin, Join(address.Line1, address.Line2, address.City, address.Pincode), sellerState, sellerStateCode),
            new GstParty(buyer.LegalName, buyer.Gstin, buyerText, buyerState, buyerStateCode),
            delivery, supply.Name, supplyCode, order.OrderNumber,
            sellerOrder.Items.OrderBy(x => x.Id).Select(x => new GstItemInput(x.Title, x.Size, x.Colour, x.HsnCode, x.Quantity, x.UnitPrice, x.GstRate, x.GstAmount)).ToList(),
            sellerOrder.ShippingAmount, sellerOrder.Total));
    }

    private sealed record AddressRow(string Line1, string? Line2, string Pincode, string City);

    private Task<AddressRow?> RegisteredAddressAsync(long businessId, CancellationToken cancellationToken)
        => (from a in db.BusinessAddresses.AsNoTracking()
            join c in db.Cities.AsNoTracking() on a.CityId equals c.Id
            where a.BusinessId == businessId && a.AddressType == AddressType.Registered && a.Line1 != string.Empty
            orderby a.IsDefault descending, a.Id
            select new AddressRow(a.Line1, a.Line2, a.Pincode, c.Name)).FirstOrDefaultAsync(cancellationToken);

    private static string Join(params string?[] parts) => string.Join(", ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));

    /// <summary>The platform fee note for the seller. It is not a tax invoice, so it has no GST series and no place of supply.</summary>
    private async Task<Invoice> CreateFeeStatementAsync(Domain.Orders.SellerOrder sellerOrder, CancellationToken cancellationToken)
    {
        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var sellerName = await db.Businesses.AsNoTracking().Where(x => x.Id == sellerOrder.ManufacturerBusinessId).Select(x => x.LegalName).SingleAsync(cancellationToken);
        var invoice = new Invoice { SellerOrderId = sellerOrder.Id, InvoiceNumber = $"TC-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..20], InvoiceType = "PlatformCommission", IssuedAt = now };
        var pdf = GstInvoicePdf.RenderFeeStatement(invoice.InvoiceNumber, sellerOrder.Order!.OrderNumber, sellerName, sellerOrder.SubTotal, sellerOrder.CommissionRate * 100m, sellerOrder.CommissionAmount);
        await using var content = new MemoryStream(pdf);
        invoice.FileKey = await storage.SavePrivateAsync(content, $"{invoice.InvoiceNumber}.pdf", cancellationToken);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);
        return invoice;
    }
}
