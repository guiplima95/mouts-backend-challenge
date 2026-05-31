using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales.UpdateSale;

public class UpdateSaleRequest
{
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public List<UpdateSaleItemRequest> Items { get; set; } = new();

    public static implicit operator UpdateSaleCommand(UpdateSaleRequest r) => new()
    {
        SaleNumber = r.SaleNumber,
        SaleDate = r.SaleDate,
        CustomerId = r.CustomerId,
        CustomerName = r.CustomerName,
        BranchId = r.BranchId,
        BranchName = r.BranchName,
        Items = r.Items.Select(i => new UpdateSaleItemCommand
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice
        }).ToList()
    };
}
