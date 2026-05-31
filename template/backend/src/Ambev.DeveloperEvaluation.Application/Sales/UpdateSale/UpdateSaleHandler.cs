using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Application.Sales.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;

public class UpdateSaleHandler : IRequestHandler<UpdateSaleCommand, OperationResult<UpdateSaleResult>>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IEventStoreRepository _eventStore;
    private readonly ISaleCacheService _cache;
    private readonly ILogger<UpdateSaleHandler> _logger;

    public UpdateSaleHandler(ISaleRepository saleRepository, IEventStoreRepository eventStore, ISaleCacheService cache, ILogger<UpdateSaleHandler> logger)
    {
        _saleRepository = saleRepository;
        _eventStore = eventStore;
        _cache = cache;
        _logger = logger;
    }

    public async Task<OperationResult<UpdateSaleResult>> Handle(UpdateSaleCommand command, CancellationToken cancellationToken)
    {
        var validator = new UpdateSaleValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return OperationResult<UpdateSaleResult>.FailureValidation(validationResult.Errors.Select(e => e.ErrorMessage));
        }

        var sale = await _saleRepository.GetByIdAsync(command.Id, cancellationToken);
        if (sale == null)
        {
            return OperationResult<UpdateSaleResult>.FailureNotFound($"Sale with ID {command.Id} not found.");
        }

        if (sale.IsCancelled)
        {
            return OperationResult<UpdateSaleResult>.FailureConflict("Cannot update a cancelled sale.");
        }

        sale.SaleNumber = command.SaleNumber;
        sale.SaleDate = command.SaleDate;
        sale.CustomerId = command.CustomerId;
        sale.CustomerName = command.CustomerName;
        sale.BranchId = command.BranchId;
        sale.BranchName = command.BranchName;
        sale.UpdatedAt = DateTime.UtcNow;

        sale.Items.Clear();
        foreach (var itemCmd in command.Items)
        {
            var item = new SaleItem
            {
                ProductId = itemCmd.ProductId,
                ProductName = itemCmd.ProductName,
                Quantity = itemCmd.Quantity,
                UnitPrice = itemCmd.UnitPrice
            };

            var businessRuleError = item.ApplyBusinessRules();
            if (businessRuleError is not null)
            {
                return OperationResult<UpdateSaleResult>.FailureBusinessRule(businessRuleError);
            }

            sale.Items.Add(item);
        }

        sale.CalculateTotalAmount();

        var updated = await _saleRepository.UpdateAsync(sale, cancellationToken);

        var saleModifiedEvent = new SaleModifiedEvent(updated);
        await _eventStore.AppendAsync(saleModifiedEvent, cancellationToken);
        await _cache.RemoveAsync(updated.Id, cancellationToken);
        _logger.LogInformation("[Event:SaleModified] SaleId={SaleId}, SaleNumber={SaleNumber}",
            saleModifiedEvent.Sale.Id, saleModifiedEvent.Sale.SaleNumber);

        return OperationResult<UpdateSaleResult>.Success(new UpdateSaleResult
        {
            Id = updated.Id,
            SaleNumber = updated.SaleNumber,
            SaleDate = updated.SaleDate,
            CustomerId = updated.CustomerId,
            CustomerName = updated.CustomerName,
            BranchId = updated.BranchId,
            BranchName = updated.BranchName,
            TotalAmount = updated.TotalAmount,
            IsCancelled = updated.IsCancelled,
            Items = updated.Items.Select(i => new UpdateSaleItemResult
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
        });
    }
}
