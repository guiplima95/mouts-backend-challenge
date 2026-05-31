using Ambev.DeveloperEvaluation.Application.Sales.GetSale;

namespace Ambev.DeveloperEvaluation.Application.Sales.Common;

public interface ISaleCacheService
{
    Task<GetSaleResult?> GetAsync(Guid saleId, CancellationToken cancellationToken = default);
    Task SetAsync(Guid saleId, GetSaleResult result, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid saleId, CancellationToken cancellationToken = default);
}
