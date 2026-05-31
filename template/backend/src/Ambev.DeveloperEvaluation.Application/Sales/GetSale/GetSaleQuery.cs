using MediatR;
using Ambev.DeveloperEvaluation.Application.Sales.Common;

namespace Ambev.DeveloperEvaluation.Application.Sales.GetSale;

public record GetSaleQuery(Guid Id) : IRequest<OperationResult<GetSaleResult>>;
