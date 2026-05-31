using MediatR;
using Ambev.DeveloperEvaluation.Application.Sales.Common;

namespace Ambev.DeveloperEvaluation.Application.Sales.GetSales;

public record GetSalesQuery(int Page = 1, int PageSize = 10, string? OrderBy = null)
    : IRequest<OperationResult<GetSalesResult>>;
