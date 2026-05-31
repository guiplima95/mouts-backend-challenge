using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales.CreateSale;

public class CreateSaleRequest
{
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public List<CreateSaleItemRequest> Items { get; set; } = new();

    public static implicit operator CreateSaleCommand(CreateSaleRequest r) => new()
    {
        SaleNumber = r.SaleNumber,
        SaleDate = r.SaleDate,
        CustomerId = r.CustomerId,
        CustomerName = r.CustomerName,
        BranchId = r.BranchId,
        BranchName = r.BranchName,
        Items = r.Items.Select(i => new CreateSaleItemCommand
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice
        }).ToList()
    };
}
