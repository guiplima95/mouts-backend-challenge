using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Application.Sales.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ambev.DeveloperEvaluation.Application.Sales.CreateSale;

public class CreateSaleHandler : IRequestHandler<CreateSaleCommand, OperationResult<CreateSaleResult>>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IEventStoreRepository _eventStore;
    private readonly ILogger<CreateSaleHandler> _logger;

    public CreateSaleHandler(ISaleRepository saleRepository, IEventStoreRepository eventStore, ILogger<CreateSaleHandler> logger)
    {
        _saleRepository = saleRepository;
        _eventStore = eventStore;
        _logger = logger;
    }

    public async Task<OperationResult<CreateSaleResult>> Handle(CreateSaleCommand command, CancellationToken cancellationToken)
    {
        var validator = new CreateSaleValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return OperationResult<CreateSaleResult>.FailureValidation(validationResult.Errors.Select(e => e.ErrorMessage));
        }

        var sale = new Sale
        {
            SaleNumber = command.SaleNumber,
            SaleDate = command.SaleDate,
            CustomerId = command.CustomerId,
            CustomerName = command.CustomerName,
            BranchId = command.BranchId,
            BranchName = command.BranchName
        };

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
                return OperationResult<CreateSaleResult>.FailureBusinessRule(businessRuleError);
            }

            sale.Items.Add(item);
        }

        sale.CalculateTotalAmount();

        var created = await _saleRepository.CreateAsync(sale, cancellationToken);

        var saleCreatedEvent = new SaleCreatedEvent(created);
        await _eventStore.AppendAsync(saleCreatedEvent, cancellationToken);
        _logger.LogInformation("[Event:SaleCreated] SaleId={SaleId}, SaleNumber={SaleNumber}, Customer={CustomerName}",
            saleCreatedEvent.Sale.Id, saleCreatedEvent.Sale.SaleNumber, saleCreatedEvent.Sale.CustomerName);

        return OperationResult<CreateSaleResult>.Success(new CreateSaleResult
        {
            Id = created.Id,
            SaleNumber = created.SaleNumber,
            SaleDate = created.SaleDate,
            CustomerId = created.CustomerId,
            CustomerName = created.CustomerName,
            BranchId = created.BranchId,
            BranchName = created.BranchName,
            TotalAmount = created.TotalAmount,
            IsCancelled = created.IsCancelled,
            Items = created.Items.Select(i => new CreateSaleItemResult
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
