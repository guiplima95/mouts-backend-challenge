using Ambev.DeveloperEvaluation.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Entities;

public class SaleTests
{
    [Fact(DisplayName = "Given 4+ items When applying rules Then 10% discount applied")]
    public void ApplyBusinessRules_4PlusItems_Applies10PercentDiscount()
    {
        var item = new SaleItem { Quantity = 4, UnitPrice = 100m };
        item.ApplyBusinessRules();
        item.Discount.Should().Be(40m);
        item.TotalAmount.Should().Be(360m);
    }

    [Fact(DisplayName = "Given 10-20 items When applying rules Then 20% discount applied")]
    public void ApplyBusinessRules_10To20Items_Applies20PercentDiscount()
    {
        var item = new SaleItem { Quantity = 10, UnitPrice = 100m };
        item.ApplyBusinessRules();
        item.Discount.Should().Be(200m);
        item.TotalAmount.Should().Be(800m);
    }

    [Fact(DisplayName = "Given less than 4 items When applying rules Then no discount")]
    public void ApplyBusinessRules_LessThan4Items_NoDiscount()
    {
        var item = new SaleItem { Quantity = 3, UnitPrice = 100m };
        item.ApplyBusinessRules();
        item.Discount.Should().Be(0m);
        item.TotalAmount.Should().Be(300m);
    }

    [Fact(DisplayName = "Given more than 20 items When applying rules Then returns business rule error")]
    public void ApplyBusinessRules_MoreThan20Items_ReturnsError()
    {
        var item = new SaleItem { Quantity = 21, UnitPrice = 100m };
        var error = item.ApplyBusinessRules();
        error.Should().NotBeNull();
        error.Should().Contain("20");
    }

    [Fact(DisplayName = "Given sale with items When calculating total Then sum of item totals")]
    public void CalculateTotalAmount_WithItems_SumsItemTotals()
    {
        var sale = new Sale();
        var item1 = new SaleItem { Quantity = 2, UnitPrice = 50m };
        item1.ApplyBusinessRules();
        var item2 = new SaleItem { Quantity = 4, UnitPrice = 100m };
        item2.ApplyBusinessRules();
        sale.Items.AddRange(new[] { item1, item2 });
        sale.CalculateTotalAmount();
        sale.TotalAmount.Should().Be(item1.TotalAmount + item2.TotalAmount);
    }

    [Fact(DisplayName = "Given sale When cancelled Then IsCancelled is true")]
    public void Cancel_Sale_SetsCancelledTrue()
    {
        var sale = new Sale();
        sale.Cancel();
        sale.IsCancelled.Should().BeTrue();
    }
}
