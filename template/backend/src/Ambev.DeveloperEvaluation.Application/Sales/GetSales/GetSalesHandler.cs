using Ambev.DeveloperEvaluation.Application.Sales.GetSale;
using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.GetSales;

public class GetSalesHandler : IRequestHandler<GetSalesQuery, OperationResult<GetSalesResult>>
{
    private readonly ISaleRepository _saleRepository;

    public GetSalesHandler(ISaleRepository saleRepository)
    {
        _saleRepository = saleRepository;
    }

    public async Task<OperationResult<GetSalesResult>> Handle(GetSalesQuery query, CancellationToken cancellationToken)
    {
        var totalCount = await _saleRepository.GetCountAsync(cancellationToken);
        var sales = await _saleRepository.GetAllAsync(query.Page, query.PageSize, query.OrderBy, cancellationToken);
        var list = sales.ToList();

        return OperationResult<GetSalesResult>.Success(new GetSalesResult
        {
            Sales = list.Select(sale => new GetSaleResult
            {
                Id = sale.Id,
                SaleNumber = sale.SaleNumber,
                SaleDate = sale.SaleDate,
                CustomerId = sale.CustomerId,
                CustomerName = sale.CustomerName,
                BranchId = sale.BranchId,
                BranchName = sale.BranchName,
                TotalAmount = sale.TotalAmount,
                IsCancelled = sale.IsCancelled,
                Items = sale.Items.Select(i => new GetSaleItemResult
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
            }),
            Total = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        });
    }
}
