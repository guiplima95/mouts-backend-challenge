using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Application.Sales.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSale;

public class CancelSaleHandler : IRequestHandler<CancelSaleCommand, OperationResult<CancelSaleResult>>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IEventStoreRepository _eventStore;
    private readonly ISaleCacheService _cache;
    private readonly ILogger<CancelSaleHandler> _logger;

    public CancelSaleHandler(ISaleRepository saleRepository, IEventStoreRepository eventStore, ISaleCacheService cache, ILogger<CancelSaleHandler> logger)
    {
        _saleRepository = saleRepository;
        _eventStore = eventStore;
        _cache = cache;
        _logger = logger;
    }

    public async Task<OperationResult<CancelSaleResult>> Handle(CancelSaleCommand command, CancellationToken cancellationToken)
    {
        var sale = await _saleRepository.GetByIdAsync(command.Id, cancellationToken);
        if (sale == null)
        {
            return OperationResult<CancelSaleResult>.FailureNotFound($"Sale with ID {command.Id} not found.");
        }

        if (sale.IsCancelled)
        {
            return OperationResult<CancelSaleResult>.FailureConflict("Sale is already cancelled.");
        }

        sale.Cancel();
        await _saleRepository.UpdateAsync(sale, cancellationToken);

        var saleCancelledEvent = new SaleCancelledEvent(sale);
        await _eventStore.AppendAsync(saleCancelledEvent, cancellationToken);
        await _cache.RemoveAsync(sale.Id, cancellationToken);
        _logger.LogInformation("[Event:SaleCancelled] SaleId={SaleId}, SaleNumber={SaleNumber}",
            saleCancelledEvent.Sale.Id, saleCancelledEvent.Sale.SaleNumber);

        return OperationResult<CancelSaleResult>.Success(new CancelSaleResult(true));
    }
}
