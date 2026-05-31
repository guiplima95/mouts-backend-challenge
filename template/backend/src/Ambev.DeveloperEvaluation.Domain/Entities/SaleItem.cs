using Ambev.DeveloperEvaluation.Domain.Common;

namespace Ambev.DeveloperEvaluation.Domain.Entities;

public class SaleItem : BaseEntity
{
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public bool IsCancelled { get; set; }

    public string? ApplyBusinessRules()
    {
        if (Quantity > 20)
        {
            return "Cannot sell more than 20 identical items.";
        }

        Discount = Quantity switch
        {
            >= 10 => UnitPrice * Quantity * 0.20m,
            >= 4 => UnitPrice * Quantity * 0.10m,
            _ => 0m
        };

        TotalAmount = (UnitPrice * Quantity) - Discount;
        return null;
    }

    public void Cancel()
    {
        IsCancelled = true;
    }
}
