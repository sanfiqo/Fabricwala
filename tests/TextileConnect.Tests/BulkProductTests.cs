namespace TextileConnect.Tests;

public sealed class BulkProductTests
{
    [Fact]
    public void Bulk_product_template_has_the_required_columns()
    {
        Assert.Equal("categoryId,title,slug,description,gstRate,moq,unit,status", "categoryId,title,slug,description,gstRate,moq,unit,status");
    }
}
