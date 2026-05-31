using MediatR;
using Ambev.DeveloperEvaluation.Application.Sales.Common;

namespace Ambev.DeveloperEvaluation.Application.Sales.DeleteSale;

public record DeleteSaleCommand(Guid Id) : IRequest<OperationResult<DeleteSaleResult>>;
