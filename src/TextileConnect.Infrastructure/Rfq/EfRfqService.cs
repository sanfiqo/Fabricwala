using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Common;
using TextileConnect.Application.Rfq;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Domain.Notifications;
using TextileConnect.Domain.Rfq;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Rfq;

public sealed class EfRfqService(MarketplaceDbContext db, IAuditTrail? audit = null, TimeProvider? clock = null) : IRfqService
{
    public const int MaxOpenPerRetailer = 10;
    public const int DefaultDays = 14;
    public const int MaxDays = 30;
    /// <summary>How many manufacturers of the category are told about a new request.</summary>
    public const int MaxCategoryAlerts = 50;

    private DateTime Now => (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    private static RuleViolationException Rule(string code, string message, int status = 400) => new(code, message, status);

    private static string Effective(Domain.Rfq.Rfq rfq, DateTime now) => rfq.Status == RfqStatus.Open && rfq.ExpiresAtUtc <= now ? "Expired" : rfq.Status;

    private static bool IsOpen(Domain.Rfq.Rfq rfq, DateTime now) => rfq.Status == RfqStatus.Open && rfq.ExpiresAtUtc > now;

    // ---- retailer ---------------------------------------------------------------------------------------------------------------

    public async Task<RfqDetail> CreateAsync(long retailerBusinessId, RfqInput input, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var title = (input.Title ?? string.Empty).Trim();
        var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        var fields = new Dictionary<string, string>();
        if (title.Length is < 5 or > 120) fields["title"] = "Write what you need in 5 to 120 characters.";
        if (description is { Length: > 2000 }) fields["description"] = "Keep the details within 2000 characters.";
        if (input.Quantity is < 1 or > 1_000_000) fields["quantity"] = "The quantity must be between 1 and 1,000,000.";
        if (input.TargetPricePerUnit is <= 0 or > 10_000_000) fields["targetPricePerUnit"] = "The target price must be more than 0.";
        if (input.NeededBy is { } needed && (needed <= now || needed > now.AddDays(365))) fields["neededBy"] = "Choose a date within the next year.";
        var days = input.ExpiresInDays ?? DefaultDays;
        if (days is < 1 or > MaxDays) fields["expiresInDays"] = $"A request stays open for 1 to {MaxDays} days.";
        var unit = ProductUnit.Piece;
        if (!string.IsNullOrWhiteSpace(input.Unit) && !Enum.TryParse(input.Unit, ignoreCase: true, out unit)) fields["unit"] = "Use Piece, Set, Metre or Kg.";
        if (input.CategoryId is { } categoryId && !await db.Categories.AnyAsync(x => x.Id == categoryId, cancellationToken)) fields["categoryId"] = "This category does not exist.";
        if (fields.Count > 0) throw new RuleViolationException("validation_failed", "Please check the request.", 400) { Fields = fields };

        if (!BusinessInputRules.IsPincode(input.DeliveryPincode)) throw PostalDirectory.Invalid();
        var place = await (from p in db.PostalCodes.AsNoTracking()
                           join c in db.Cities.AsNoTracking() on p.CityId equals c.Id
                           join s in db.States.AsNoTracking() on c.StateId equals s.Id
                           where p.Pincode == input.DeliveryPincode
                           select new { City = c.Name, State = s.Name }).FirstOrDefaultAsync(cancellationToken) ?? throw PostalDirectory.Unknown(input.DeliveryPincode);

        var open = await db.Rfqs.CountAsync(x => x.RetailerBusinessId == retailerBusinessId && x.Status == RfqStatus.Open && x.ExpiresAtUtc > now, cancellationToken);
        if (open >= MaxOpenPerRetailer)
            throw Rule("rfq_limit_reached", $"You already have {MaxOpenPerRetailer} open requests. Close one, or wait until it expires, before you make another.", 409);

        var rfq = new Domain.Rfq.Rfq
        {
            RetailerBusinessId = retailerBusinessId, Title = title, Description = description, CategoryId = input.CategoryId, Quantity = input.Quantity, Unit = unit,
            TargetPricePerUnit = input.TargetPricePerUnit is null ? null : decimal.Round(input.TargetPricePerUnit.Value, 2, MidpointRounding.AwayFromZero),
            DeliveryPincode = input.DeliveryPincode, DeliveryCity = place.City, DeliveryState = place.State, NeededByUtc = input.NeededBy,
            ExpiresAtUtc = now.AddDays(days), CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.Rfqs.Add(rfq);
        await db.SaveChangesAsync(cancellationToken);
        audit?.Record("rfq.created", nameof(Domain.Rfq.Rfq), rfq.Id, $"quantity {rfq.Quantity}, {days} days");
        // Tell the manufacturers who sell in the category of the request (at most MaxCategoryAlerts of them), so the request does not wait for somebody to open the feed.
        if (rfq.CategoryId is { } alertCategory)
        {
            var sellers = await db.Products.AsNoTracking().Where(p => p.CategoryId == alertCategory && !p.IsDeleted && p.Status == ProductStatus.Active).Select(p => p.SellerBusinessId).Distinct().Take(MaxCategoryAlerts * 2).ToListAsync(cancellationToken);
            var approved = await db.Businesses.AsNoTracking().Where(b => sellers.Contains(b.Id) && !b.IsDeleted && b.BusinessType == BusinessType.Manufacturer && b.VerificationStatus == VerificationStatus.Approved)
                .Select(b => b.Id).Take(MaxCategoryAlerts).ToListAsync(cancellationToken);
            await NotifyBusinessesAsync(approved, "New request for quotation", $"A buyer asks for {rfq.Quantity} pieces: \"{rfq.Title}\".", $"/seller/rfqs/{rfq.Id}", now, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        return (await GetMineAsync(retailerBusinessId, rfq.Id, cancellationToken))!;
    }

    public async Task<PagedResponse<RfqSummary>> ListMineAsync(long retailerBusinessId, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var query = db.Rfqs.AsNoTracking().Where(x => x.RetailerBusinessId == retailerBusinessId);
        query = FilterByStatus(query, status, now);
        return await PageAsync(query.OrderByDescending(x => x.Id), page, pageSize, null, now, cancellationToken);
    }

    public async Task<RfqDetail?> GetMineAsync(long retailerBusinessId, long rfqId, CancellationToken cancellationToken = default)
    {
        var rfq = await db.Rfqs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == rfqId && x.RetailerBusinessId == retailerBusinessId, cancellationToken);
        return rfq is null ? null : await DetailAsync(rfq, viewerSellerId: null, cancellationToken);
    }

    public async Task<RfqDetail?> CancelAsync(long retailerBusinessId, long rfqId, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var rfq = await db.Rfqs.SingleOrDefaultAsync(x => x.Id == rfqId && x.RetailerBusinessId == retailerBusinessId, cancellationToken);
        if (rfq is null) return null;
        if (!IsOpen(rfq, now)) throw Rule("rfq_not_open", "This request is no longer open.", 409);
        rfq.Status = RfqStatus.Cancelled; rfq.UpdatedAtUtc = now;
        var rejected = await RejectSubmittedAsync(rfq.Id, null, now, cancellationToken);
        await NotifySellersAsync(rejected, "Request cancelled", $"The buyer cancelled the request \"{rfq.Title}\".", now, cancellationToken);
        audit?.Record("rfq.cancelled", nameof(Domain.Rfq.Rfq), rfq.Id);
        await SaveOrConflictAsync(cancellationToken);
        return await GetMineAsync(retailerBusinessId, rfqId, cancellationToken);
    }

    public async Task<RfqDetail?> AcceptAsync(long retailerBusinessId, long rfqId, long quotationId, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var rfq = await db.Rfqs.SingleOrDefaultAsync(x => x.Id == rfqId && x.RetailerBusinessId == retailerBusinessId, cancellationToken);
        if (rfq is null) return null;
        if (!IsOpen(rfq, now)) throw Rule("rfq_not_open", "This request is no longer open.", 409);
        var chosen = await db.RfqQuotations.SingleOrDefaultAsync(x => x.Id == quotationId && x.RfqId == rfqId, cancellationToken);
        if (chosen is null) return null;
        if (chosen.Status != QuotationStatus.Submitted) throw Rule("quotation_not_available", "This quotation was withdrawn or is no longer available.", 409);

        rfq.Status = RfqStatus.Awarded; rfq.AwardedQuotationId = chosen.Id; rfq.UpdatedAtUtc = now;
        chosen.Status = QuotationStatus.Accepted; chosen.UpdatedAtUtc = now;
        var rejected = await RejectSubmittedAsync(rfq.Id, chosen.Id, now, cancellationToken);
        await NotifySellersAsync([chosen.SellerBusinessId], "Your quotation was accepted", $"The buyer accepted your quotation for \"{rfq.Title}\". Open the request to see how to reach them.", now, cancellationToken, $"/seller/rfqs/{rfq.Id}");
        await NotifySellersAsync(rejected, "Another quotation was chosen", $"The buyer chose another quotation for \"{rfq.Title}\".", now, cancellationToken);
        audit?.Record("rfq.awarded", nameof(Domain.Rfq.Rfq), rfq.Id, $"quotation {chosen.Id}, {chosen.PricePerUnit.ToString("0.00", CultureInfo.InvariantCulture)} per unit");
        await SaveOrConflictAsync(cancellationToken);
        return await GetMineAsync(retailerBusinessId, rfqId, cancellationToken);
    }

    // ---- manufacturer -----------------------------------------------------------------------------------------------------------

    public async Task<PagedResponse<RfqSummary>> FeedAsync(long sellerBusinessId, long? categoryId, string? q, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var query = db.Rfqs.AsNoTracking().Where(x => x.Status == RfqStatus.Open && x.ExpiresAtUtc > now);
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId);
        var text = q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(text)) query = query.Where(x => x.Title.ToLower().Contains(text) || x.Description != null && x.Description.ToLower().Contains(text));
        return await PageAsync(query.OrderByDescending(x => x.Id), page, pageSize, sellerBusinessId, now, cancellationToken);
    }

    public async Task<RfqDetail?> GetForSellerAsync(long sellerBusinessId, long rfqId, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var rfq = await db.Rfqs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == rfqId, cancellationToken);
        if (rfq is null) return null;
        // An open request is for every manufacturer to see; a closed one only for those who quoted on it.
        var quoted = await db.RfqQuotations.AnyAsync(x => x.RfqId == rfqId && x.SellerBusinessId == sellerBusinessId, cancellationToken);
        if (!IsOpen(rfq, now) && !quoted) return null;
        return await DetailAsync(rfq, sellerBusinessId, cancellationToken);
    }

    public async Task<RfqDetail?> QuoteAsync(long sellerBusinessId, long rfqId, QuotationInput input, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var rfq = await db.Rfqs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == rfqId, cancellationToken);
        if (rfq is null) return null;
        if (!IsOpen(rfq, now)) throw Rule("rfq_not_open", "This request is no longer open.", 409);
        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        var fields = new Dictionary<string, string>();
        if (input.PricePerUnit is <= 0 or > 10_000_000) fields["pricePerUnit"] = "Enter the price for one piece (more than 0).";
        if (input.Quantity < 1 || input.Quantity > rfq.Quantity) fields["quantity"] = $"You can offer 1 to {rfq.Quantity} pieces.";
        if (input.LeadTimeDays is < 1 or > 365) fields["leadTimeDays"] = "Say in how many days you can deliver (1 to 365).";
        if (note is { Length: > 1000 }) fields["note"] = "Keep the note within 1000 characters.";
        if (fields.Count > 0) throw new RuleViolationException("validation_failed", "Please check the quotation.", 400) { Fields = fields };

        var quotation = await db.RfqQuotations.SingleOrDefaultAsync(x => x.RfqId == rfqId && x.SellerBusinessId == sellerBusinessId, cancellationToken);
        var isNew = quotation is null || quotation.Status == QuotationStatus.Withdrawn;
        if (quotation is null) { quotation = new RfqQuotation { RfqId = rfqId, SellerBusinessId = sellerBusinessId, CreatedAtUtc = now }; db.RfqQuotations.Add(quotation); }
        else if (quotation.Status is QuotationStatus.Accepted or QuotationStatus.Rejected) throw Rule("quotation_final", "This quotation has already been decided.", 409);
        quotation.PricePerUnit = decimal.Round(input.PricePerUnit, 2, MidpointRounding.AwayFromZero);
        quotation.Quantity = input.Quantity; quotation.LeadTimeDays = input.LeadTimeDays; quotation.Note = note; quotation.Status = QuotationStatus.Submitted; quotation.UpdatedAtUtc = now;
        if (isNew)
        {
            var sellerName = await db.Businesses.AsNoTracking().Where(x => x.Id == sellerBusinessId).Select(x => x.DisplayName).SingleAsync(cancellationToken);
            await NotifyBusinessesAsync([rfq.RetailerBusinessId], "New quotation", $"{sellerName} sent a quotation for \"{rfq.Title}\".", $"/rfqs/{rfq.Id}", now, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        return await GetForSellerAsync(sellerBusinessId, rfqId, cancellationToken);
    }

    public async Task<RfqDetail?> WithdrawAsync(long sellerBusinessId, long rfqId, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var quotation = await db.RfqQuotations.SingleOrDefaultAsync(x => x.RfqId == rfqId && x.SellerBusinessId == sellerBusinessId, cancellationToken);
        if (quotation is null) return null;
        if (quotation.Status != QuotationStatus.Submitted) throw Rule("quotation_final", "Only a quotation that is waiting for an answer can be withdrawn.", 409);
        quotation.Status = QuotationStatus.Withdrawn; quotation.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return await GetForSellerAsync(sellerBusinessId, rfqId, cancellationToken);
    }

    public async Task<PagedResponse<MyQuotationRow>> MyQuotationsAsync(long sellerBusinessId, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var mine = db.RfqQuotations.AsNoTracking().Where(x => x.SellerBusinessId == sellerBusinessId);
        if (!string.IsNullOrWhiteSpace(status)) mine = mine.Where(x => x.Status == status);
        var total = await mine.CountAsync(cancellationToken);
        var quotations = await mine.OrderByDescending(x => x.UpdatedAtUtc).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var rfqIds = quotations.Select(x => x.RfqId).ToArray();
        var summaries = (await SummariesAsync(db.Rfqs.AsNoTracking().Where(x => rfqIds.Contains(x.Id)), sellerBusinessId, now, cancellationToken)).ToDictionary(x => x.Id);
        var names = await db.Businesses.AsNoTracking().Where(x => x.Id == sellerBusinessId).Select(x => x.DisplayName).SingleAsync(cancellationToken);
        var rows = quotations.Select(x => new MyQuotationRow(summaries[x.RfqId], View(x, names, mine: true, contact: null))).ToList();
        return new PagedResponse<MyQuotationRow>(rows, total, page, pageSize);
    }

    // ---- admin ------------------------------------------------------------------------------------------------------------------

    public async Task<PagedResponse<RfqAdminRow>> ListAllAsync(string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var query = FilterByStatus(db.Rfqs.AsNoTracking(), status, now);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new
        {
            x.Id, x.Title, x.RetailerBusinessId, x.Quantity, x.Status, x.ExpiresAtUtc, x.CreatedAtUtc,
            Count = db.RfqQuotations.Count(q => q.RfqId == x.Id && (q.Status == QuotationStatus.Submitted || q.Status == QuotationStatus.Accepted))
        }).ToListAsync(cancellationToken);
        var retailerIds = rows.Select(x => x.RetailerBusinessId).Distinct().ToArray();
        var names = await db.Businesses.AsNoTracking().Where(x => retailerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        return new PagedResponse<RfqAdminRow>(rows.Select(x => new RfqAdminRow(x.Id, x.Title, names.GetValueOrDefault(x.RetailerBusinessId) ?? string.Empty, x.Quantity,
            x.Status == RfqStatus.Open && x.ExpiresAtUtc <= now ? "Expired" : x.Status, x.ExpiresAtUtc, x.Count, x.CreatedAtUtc)).ToList(), total, page, pageSize);
    }

    public async Task<bool> CloseAsync(long rfqId, string? reason, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var rfq = await db.Rfqs.SingleOrDefaultAsync(x => x.Id == rfqId, cancellationToken);
        if (rfq is null) return false;
        if (!IsOpen(rfq, now)) throw Rule("rfq_not_open", "This request is no longer open.", 409);
        rfq.Status = RfqStatus.Closed; rfq.UpdatedAtUtc = now;
        var rejected = await RejectSubmittedAsync(rfq.Id, null, now, cancellationToken);
        await NotifyBusinessesAsync([rfq.RetailerBusinessId], "Request closed", $"Your request \"{rfq.Title}\" was closed by the marketplace team.", $"/rfqs/{rfq.Id}", now, cancellationToken);
        await NotifySellersAsync(rejected, "Request closed", $"The request \"{rfq.Title}\" was closed by the marketplace team.", now, cancellationToken);
        audit?.Record("rfq.closed", nameof(Domain.Rfq.Rfq), rfq.Id, string.IsNullOrWhiteSpace(reason) ? null : (reason.Length > 200 ? reason[..200] : reason));
        await SaveOrConflictAsync(cancellationToken);
        return true;
    }

    // ---- shared -----------------------------------------------------------------------------------------------------------------

    private static IQueryable<Domain.Rfq.Rfq> FilterByStatus(IQueryable<Domain.Rfq.Rfq> query, string? status, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(status)) return query;
        return status switch
        {
            "Open" => query.Where(x => x.Status == RfqStatus.Open && x.ExpiresAtUtc > now),
            "Expired" => query.Where(x => x.Status == RfqStatus.Open && x.ExpiresAtUtc <= now),
            _ => query.Where(x => x.Status == status)
        };
    }

    private async Task<PagedResponse<RfqSummary>> PageAsync(IQueryable<Domain.Rfq.Rfq> ordered, int page, int pageSize, long? viewerSellerId, DateTime now, CancellationToken cancellationToken)
    {
        var total = await ordered.CountAsync(cancellationToken);
        var pageQuery = ordered.Skip((page - 1) * pageSize).Take(pageSize);
        return new PagedResponse<RfqSummary>(await SummariesAsync(pageQuery, viewerSellerId, now, cancellationToken, keepOrder: true), total, page, pageSize);
    }

    private async Task<List<RfqSummary>> SummariesAsync(IQueryable<Domain.Rfq.Rfq> source, long? viewerSellerId, DateTime now, CancellationToken cancellationToken, bool keepOrder = false)
    {
        var rows = await source.Select(x => new
        {
            Rfq = x,
            Count = db.RfqQuotations.Count(q => q.RfqId == x.Id && (q.Status == QuotationStatus.Submitted || q.Status == QuotationStatus.Accepted)),
            Mine = viewerSellerId == null ? (bool?)null : db.RfqQuotations.Any(q => q.RfqId == x.Id && q.SellerBusinessId == viewerSellerId && q.Status == QuotationStatus.Submitted)
        }).ToListAsync(cancellationToken);
        var categoryIds = rows.Where(x => x.Rfq.CategoryId.HasValue).Select(x => x.Rfq.CategoryId!.Value).Distinct().ToArray();
        var categories = await db.Categories.AsNoTracking().Where(x => categoryIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var list = rows.Select(x => new RfqSummary(x.Rfq.Id, x.Rfq.Title, x.Rfq.Quantity, x.Rfq.Unit.ToString(), Effective(x.Rfq, now), x.Rfq.ExpiresAtUtc, x.Count, x.Rfq.DeliveryCity,
            x.Rfq.DeliveryState, x.Rfq.CreatedAtUtc, x.Rfq.CategoryId, x.Rfq.CategoryId.HasValue ? categories.GetValueOrDefault(x.Rfq.CategoryId.Value) : null, x.Rfq.TargetPricePerUnit, x.Mine));
        return keepOrder ? list.ToList() : list.ToList();
    }

    private static QuotationView View(RfqQuotation q, string sellerName, bool mine, RfqContact? contact)
        => new(q.Id, q.SellerBusinessId, sellerName, q.PricePerUnit, q.Quantity, decimal.Round(q.PricePerUnit * q.Quantity, 2, MidpointRounding.AwayFromZero), q.LeadTimeDays, q.Note, q.Status, q.CreatedAtUtc, mine, contact);

    /// <summary>
    /// The request with its quotations, as one of two readers sees it. The buyer (<paramref name="viewerSellerId"/> null) sees every quotation that was not withdrawn, with the manufacturer's name,
    /// and the contact of the manufacturer whose quotation was accepted. A manufacturer sees only their own quotation, and the buyer's contact only once that quotation was accepted.
    /// </summary>
    private async Task<RfqDetail> DetailAsync(Domain.Rfq.Rfq rfq, long? viewerSellerId, CancellationToken cancellationToken)
    {
        var now = Now;
        var summary = (await SummariesAsync(db.Rfqs.AsNoTracking().Where(x => x.Id == rfq.Id), viewerSellerId, now, cancellationToken)).Single();
        var quotationQuery = db.RfqQuotations.AsNoTracking().Where(x => x.RfqId == rfq.Id);
        quotationQuery = viewerSellerId.HasValue ? quotationQuery.Where(x => x.SellerBusinessId == viewerSellerId) : quotationQuery.Where(x => x.Status != QuotationStatus.Withdrawn);
        var quotations = await quotationQuery.OrderBy(x => x.PricePerUnit).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var businessIds = quotations.Select(x => x.SellerBusinessId).Append(rfq.RetailerBusinessId).Distinct().ToArray();
        var businesses = await db.Businesses.AsNoTracking().Where(x => businessIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        RfqContact? ContactOf(long id) => businesses.TryGetValue(id, out var b) ? new RfqContact(b.DisplayName, b.Phone) : null;
        var views = quotations.Select(x => View(x, businesses.TryGetValue(x.SellerBusinessId, out var seller) ? seller.DisplayName : string.Empty, x.SellerBusinessId == viewerSellerId,
            viewerSellerId is null && x.Status == QuotationStatus.Accepted ? ContactOf(x.SellerBusinessId) : null)).ToList();
        var buyerContact = viewerSellerId.HasValue && quotations.Any(x => x.Status == QuotationStatus.Accepted) ? ContactOf(rfq.RetailerBusinessId) : null;
        return new RfqDetail(summary, rfq.Description, rfq.NeededByUtc, views, buyerContact);
    }

    /// <summary>Marks every waiting quotation of the request as rejected (except <paramref name="exceptId"/>) and returns the manufacturers who sent them.</summary>
    private async Task<long[]> RejectSubmittedAsync(long rfqId, long? exceptId, DateTime now, CancellationToken cancellationToken)
    {
        var waiting = await db.RfqQuotations.Where(x => x.RfqId == rfqId && x.Status == QuotationStatus.Submitted && x.Id != exceptId).ToListAsync(cancellationToken);
        foreach (var quotation in waiting) { quotation.Status = QuotationStatus.Rejected; quotation.UpdatedAtUtc = now; }
        return waiting.Select(x => x.SellerBusinessId).Distinct().ToArray();
    }

    private Task NotifySellersAsync(IEnumerable<long> sellerIds, string title, string message, DateTime now, CancellationToken cancellationToken, string link = "/seller/rfqs")
        => NotifyBusinessesAsync(sellerIds, title, message, link, now, cancellationToken);

    private async Task NotifyBusinessesAsync(IEnumerable<long> businessIds, string title, string message, string link, DateTime now, CancellationToken cancellationToken)
    {
        var ids = businessIds.Distinct().ToArray();
        if (ids.Length == 0) return;
        var users = await db.BusinessUsers.AsNoTracking().Where(x => ids.Contains(x.BusinessId)).Select(x => x.UserId).Distinct().ToListAsync(cancellationToken);
        db.Notifications.AddRange(users.Select(userId => new Notification { UserId = userId, Title = title, Message = message, Link = link, CreatedAtUtc = now }));
    }

    /// <summary>The status of a request is a concurrency check, so two people deciding at the same moment cannot both win.</summary>
    private async Task SaveOrConflictAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw Rule("rfq_changed", "This request was changed a moment ago. Open it again and check.", 409); }
    }
}
