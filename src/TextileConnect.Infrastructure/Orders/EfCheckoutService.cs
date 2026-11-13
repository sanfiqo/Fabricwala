using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Orders;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Domain.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Orders;

public sealed class EfCheckoutService(MarketplaceDbContext db) : ICheckoutService
{
    public async Task<Order?> CheckoutAsync(long retailerBusinessId, string userId, long deliveryAddressId, CancellationToken cancellationToken = default)
    {
        if (!await db.BusinessAddresses.AnyAsync(x => x.Id == deliveryAddressId && x.BusinessId == retailerBusinessId, cancellationToken))
            return null;
        var rows = await (from item in db.CartItems
                          join variant in db.ProductVariants on item.VariantId equals variant.Id
                          join product in db.Products on variant.ProductId equals product.Id
                          join slab in db.PriceSlabs on product.Id equals slab.ProductId into slabs
                          where item.RetailerBusinessId == retailerBusinessId && !product.IsDeleted && product.Status == ProductStatus.Active
                          select new { item, variant, product, slabs }).ToListAsync(cancellationToken);
        if (rows.Count == 0) return null;
        var order = new Order { OrderNumber = $"TC-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..24], RetailerBusinessId = retailerBusinessId, PlacedByUserId = userId };
        foreach (var sellerRows in rows.GroupBy(x => x.product.SellerBusinessId))
        {
            var sellerOrder = new SellerOrder { ManufacturerBusinessId = sellerRows.Key, Order = order, CommissionRate = 0.02m };
            foreach (var row in sellerRows)
            {
                var price = PriceSlabRules.Select(row.slabs, row.item.Quantity)?.PricePerUnit;
                if (price is null || row.item.Quantity < row.product.Moq || row.item.Quantity > row.variant.StockQty) return null;
                sellerOrder.SubTotal += price.Value * row.item.Quantity;
                db.OrderItems.Add(new OrderItem { SellerOrder = sellerOrder, ProductId = row.product.Id, Quantity = row.item.Quantity, UnitPrice = price.Value });
            }
            order.GrandTotal += sellerOrder.SubTotal + sellerOrder.SubTotal * sellerOrder.CommissionRate;
            db.SellerOrders.Add(sellerOrder);
        }
        db.Orders.Add(order);
        db.CartItems.RemoveRange(rows.Select(x => x.item));
        await db.SaveChangesAsync(cancellationToken);
        return order;
    }
}
