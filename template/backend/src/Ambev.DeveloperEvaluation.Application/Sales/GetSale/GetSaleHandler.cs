using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Application.Sales.Common;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.GetSale;

public class GetSaleHandler : IRequestHandler<GetSaleQuery, OperationResult<GetSaleResult>>
{
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleCacheService _cache;

    public GetSaleHandler(ISaleRepository saleRepository, ISaleCacheService cache)
    {
        _saleRepository = saleRepository;
        _cache = cache;
    }

    public async Task<OperationResult<GetSaleResult>> Handle(GetSaleQuery query, CancellationToken cancellationToken)
    {
        var cached = await _cache.GetAsync(query.Id, cancellationToken);
        if (cached is not null)
        {
            return OperationResult<GetSaleResult>.Success(cached);
        }

        var sale = await _saleRepository.GetByIdAsync(query.Id, cancellationToken);
        if (sale == null)
        {
            return OperationResult<GetSaleResult>.FailureNotFound($"Sale with ID {query.Id} not found.");
        }

        var result = new GetSaleResult
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
        };

        await _cache.SetAsync(query.Id, result, cancellationToken);
        return OperationResult<GetSaleResult>.Success(result);
    }
}
