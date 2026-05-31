using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Application.Sales.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ambev.DeveloperEvaluation.Application.Sales.DeleteSale;

public class DeleteSaleHandler : IRequestHandler<DeleteSaleCommand, OperationResult<DeleteSaleResult>>
{
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleCacheService _cache;
    private readonly ILogger<DeleteSaleHandler> _logger;

    public DeleteSaleHandler(ISaleRepository saleRepository, ISaleCacheService cache, ILogger<DeleteSaleHandler> logger)
    {
        _saleRepository = saleRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<OperationResult<DeleteSaleResult>> Handle(DeleteSaleCommand command, CancellationToken cancellationToken)
    {
        var deleted = await _saleRepository.DeleteAsync(command.Id, cancellationToken);
        if (!deleted)
        {
            return OperationResult<DeleteSaleResult>.FailureNotFound($"Sale with ID {command.Id} not found.");
        }

        await _cache.RemoveAsync(command.Id, cancellationToken);
        _logger.LogInformation("[Sale:Deleted] SaleId={SaleId}", command.Id);

        return OperationResult<DeleteSaleResult>.Success(new DeleteSaleResult(true));
    }
}
