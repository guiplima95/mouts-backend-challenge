using MediatR;
using Ambev.DeveloperEvaluation.Application.Sales.Common;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;

public record CancelSaleItemCommand(Guid SaleId, Guid ItemId) : IRequest<OperationResult<CancelSaleItemResult>>;
