using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales.UpdateSale;

public class UpdateSaleResponse
{
    public Guid Id { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public bool IsCancelled { get; set; }
    public List<UpdateSaleItemResponse> Items { get; set; } = new();

    public static implicit operator UpdateSaleResponse(UpdateSaleResult r) => new()
    {
        Id = r.Id,
        SaleNumber = r.SaleNumber,
        SaleDate = r.SaleDate,
        CustomerId = r.CustomerId,
        CustomerName = r.CustomerName,
        BranchId = r.BranchId,
        BranchName = r.BranchName,
        TotalAmount = r.TotalAmount,
        IsCancelled = r.IsCancelled,
        Items = r.Items.Select(i => new UpdateSaleItemResponse
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Discount = i.Discount,
            TotalAmount = i.TotalAmount,
            IsCancelled = i.IsCancelled
        }).ToList()
    };
}
