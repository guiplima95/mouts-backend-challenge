using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Application.Sales.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;

public class CancelSaleItemHandler : IRequestHandler<CancelSaleItemCommand, OperationResult<CancelSaleItemResult>>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IEventStoreRepository _eventStore;
    private readonly ISaleCacheService _cache;
    private readonly ILogger<CancelSaleItemHandler> _logger;

    public CancelSaleItemHandler(ISaleRepository saleRepository, IEventStoreRepository eventStore, ISaleCacheService cache, ILogger<CancelSaleItemHandler> logger)
    {
        _saleRepository = saleRepository;
        _eventStore = eventStore;
        _cache = cache;
        _logger = logger;
    }

    public async Task<OperationResult<CancelSaleItemResult>> Handle(CancelSaleItemCommand command, CancellationToken cancellationToken)
    {
        var sale = await _saleRepository.GetByIdAsync(command.SaleId, cancellationToken);
        if (sale == null)
        {
            return OperationResult<CancelSaleItemResult>.FailureNotFound($"Sale with ID {command.SaleId} not found.");
        }

        var cancelError = sale.CancelItem(command.ItemId);
        if (cancelError is not null)
        {
            return OperationResult<CancelSaleItemResult>.FailureBusinessRule(cancelError);
        }

        await _saleRepository.UpdateAsync(sale, cancellationToken);

        var itemCancelledEvent = new ItemCancelledEvent(sale, command.ItemId);
        await _eventStore.AppendAsync(itemCancelledEvent, cancellationToken);
        await _cache.RemoveAsync(sale.Id, cancellationToken);
        _logger.LogInformation("[Event:ItemCancelled] SaleId={SaleId}, ItemId={ItemId}",
            itemCancelledEvent.Sale.Id, itemCancelledEvent.ItemId);

        return OperationResult<CancelSaleItemResult>.Success(new CancelSaleItemResult(true));
    }
}
