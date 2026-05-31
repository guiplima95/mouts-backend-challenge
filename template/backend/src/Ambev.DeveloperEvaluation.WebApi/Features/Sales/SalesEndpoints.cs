using Ambev.DeveloperEvaluation.Application.Sales.CancelSale;
using Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;
using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.DeleteSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSales;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.Common.Validation;
using Ambev.DeveloperEvaluation.WebApi.Common;
using Ambev.DeveloperEvaluation.WebApi.Features.Sales.CreateSale;
using Ambev.DeveloperEvaluation.WebApi.Features.Sales.GetSale;
using Ambev.DeveloperEvaluation.WebApi.Features.Sales.UpdateSale;
using MediatR;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

public static class SalesEndpoints
{
    public static IEndpointRouteBuilder MapSalesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sales").WithTags("Sales");

        group.MapPost("/", CreateSale)
            .WithName("CreateSale")
            .WithSummary("Create a new sale")
            .Produces<ApiResponseWithData<CreateSaleResponse>>(StatusCodes.Status201Created)
            .Produces<ApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse>(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{id:guid}", GetSale)
            .WithName("GetSale")
            .WithSummary("Get a sale by ID")
            .Produces<ApiResponseWithData<GetSaleResponse>>(StatusCodes.Status200OK)
            .Produces<ApiResponse>(StatusCodes.Status404NotFound);

        group.MapGet("/", GetSales)
            .WithName("GetSales")
            .WithSummary("List sales with pagination and ordering")
            .Produces<ApiResponseWithData<GetSalesResult>>(StatusCodes.Status200OK);

        group.MapPut("/{id:guid}", UpdateSale)
            .WithName("UpdateSale")
            .WithSummary("Update an existing sale")
            .Produces<ApiResponseWithData<UpdateSaleResponse>>(StatusCodes.Status200OK)
            .Produces<ApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiResponse>(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/{id:guid}", DeleteSale)
            .WithName("DeleteSale")
            .WithSummary("Delete a sale")
            .Produces<ApiResponse>(StatusCodes.Status200OK)
            .Produces<ApiResponse>(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/cancel", CancelSale)
            .WithName("CancelSale")
            .WithSummary("Cancel a sale")
            .Produces<ApiResponse>(StatusCodes.Status200OK)
            .Produces<ApiResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse>(StatusCodes.Status409Conflict);

        group.MapPatch("/{saleId:guid}/items/{itemId:guid}/cancel", CancelSaleItem)
            .WithName("CancelSaleItem")
            .WithSummary("Cancel a specific item in a sale")
            .Produces<ApiResponse>(StatusCodes.Status200OK)
            .Produces<ApiResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse>(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static async Task<IResult> CreateSale(CreateSaleRequest request, IMediator mediator, CancellationToken cancellationToken)
    {
        var validator = new CreateSaleRequestValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.BadRequest(validationResult.Errors.First().ErrorMessage);
        }

        CreateSaleCommand command = request;
        var result = await mediator.Send(command, cancellationToken);
        if (!result.IsSuccess || result.Data == null)
        {
            return ToFailureResult(result);
        }

        CreateSaleResponse response = result.Data;
        return Results.Created(string.Empty, new ApiResponseWithData<CreateSaleResponse>
        {
            Success = true,
            Message = "Sale created successfully",
            Data = response
        });
    }

    private static async Task<IResult> GetSale(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSaleQuery(id), cancellationToken);
        if (!result.IsSuccess || result.Data == null)
        {
            return ToFailureResult(result);
        }

        GetSaleResponse response = result.Data;
        return Results.Ok(new ApiResponseWithData<GetSaleResponse>
        {
            Success = true,
            Message = "Sale retrieved successfully",
            Data = response
        });
    }

    private static async Task<IResult> GetSales(IMediator mediator, CancellationToken cancellationToken, int _page = 1, int _size = 10, string? _order = null)
    {
        var result = await mediator.Send(new GetSalesQuery(_page, _size, _order), cancellationToken);
        if (!result.IsSuccess || result.Data == null)
        {
            return ToFailureResult(result);
        }

        return Results.Ok(new ApiResponseWithData<GetSalesResult>
        {
            Success = true,
            Message = "Sales retrieved successfully",
            Data = result.Data
        });
    }

    private static async Task<IResult> UpdateSale(Guid id, UpdateSaleRequest request, IMediator mediator, CancellationToken cancellationToken)
    {
        var validator = new UpdateSaleRequestValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.BadRequest(validationResult.Errors.First().ErrorMessage);
        }

        UpdateSaleCommand command = request;
        command.Id = id;

        var result = await mediator.Send(command, cancellationToken);
        if (!result.IsSuccess || result.Data == null)
        {
            return ToFailureResult(result);
        }

        UpdateSaleResponse response = result.Data;
        return Results.Ok(new ApiResponseWithData<UpdateSaleResponse>
        {
            Success = true,
            Message = "Sale updated successfully",
            Data = response
        });
    }

    private static async Task<IResult> DeleteSale(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteSaleCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return ToFailureResult(result);
        }

        return Results.Ok(new ApiResponse { Success = true, Message = "Sale deleted successfully" });
    }

    private static async Task<IResult> CancelSale(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CancelSaleCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return ToFailureResult(result);
        }

        return Results.Ok(new ApiResponse { Success = true, Message = "Sale cancelled successfully" });
    }

    private static async Task<IResult> CancelSaleItem(Guid saleId, Guid itemId, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CancelSaleItemCommand(saleId, itemId), cancellationToken);
        if (!result.IsSuccess)
        {
            return ToFailureResult(result);
        }

        return Results.Ok(new ApiResponse { Success = true, Message = "Sale item cancelled successfully" });
    }

    private static IResult ToFailureResult<T>(OperationResult<T> result)
    {
        var statusCode = result.Notifications.FirstOrDefault()?.Type switch
        {
            NotificationType.Validation => StatusCodes.Status400BadRequest,
            NotificationType.NotFound => StatusCodes.Status404NotFound,
            NotificationType.Conflict => StatusCodes.Status409Conflict,
            NotificationType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status400BadRequest
        };

        var message = result.Notifications.FirstOrDefault()?.Message ?? "Request failed.";
        var errors = result.Notifications.Select(notification => new ValidationErrorDetail
        {
            Error = notification.Type.ToString(),
            Detail = notification.Message
        });

        var response = new ApiResponse
        {
            Success = false,
            Message = message,
            Errors = errors
        };

        return Results.Json(response, statusCode: statusCode);
    }
}
