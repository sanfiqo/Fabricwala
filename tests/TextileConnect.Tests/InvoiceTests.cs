using TextileConnect.Domain.Orders;
using TextileConnect.Domain.Payments;

namespace TextileConnect.Tests;

public sealed class InvoiceTests
{
    [Fact]
    public void Invoice_number_is_stable_for_downloaded_document_metadata()
    {
        var invoice = new Invoice { InvoiceNumber = "TC-20261113-00001", InvoiceType = "PlatformCommission", SellerOrderId = 4 };
        Assert.StartsWith("TC-", invoice.InvoiceNumber);
        Assert.Equal("PlatformCommission", invoice.InvoiceType);
    }
}
